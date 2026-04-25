# Clast.Alp

ALP (Adaptive Lossless floating-Point) compression for .NET. Compresses
arrays of `double` values into a compact byte representation, using a
multi-stage pipeline that exploits the fact that real-world floating-point
data (prices, sensor readings, scientific measurements) is often decimal
in origin and uses limited precision. Compression is fully lossless.

Part of the clast-project.

## How it works

The compression pipeline has four stages:

1. **ALP encode** — search for an `(exponent, factor)` pair such that
   `value * 10^(exponent - factor)` rounds to a small integer. The
   search picks the pair that produces the fewest *exceptions* — values
   that can't be losslessly recovered. Exceptions are stored verbatim and
   patched in on decode.
2. **Frame-of-Reference (FOR)** — subtract the minimum encoded integer
   so all deltas are non-negative.
3. **Bit-pack** — write each delta using exactly `ceil(log2(max + 1))`
   bits.
4. **Serialize** — emit a 24-byte little-endian header followed by the
   packed deltas and any exceptions. The full layout is documented in
   `src/Alp/AlpCompressor.cs`.

Decompression reverses each step.

Based on the ALP algorithm originally published by researchers at CWI
Amsterdam.

## Usage

```csharp
using Clast.Alp;

double[] prices = [99.99, 100.01, 42.50, 0.01, 1234.56];

byte[] compressed = AlpCompressor.Compress(prices);
double[] decoded   = AlpCompressor.Decompress(compressed);
// decoded.SequenceEqual(prices) == true
```

For pooled-buffer or pipeline scenarios, an `IBufferWriter<byte>` overload
avoids the output `byte[]` allocation:

```csharp
AlpCompressor.Compress(prices, writer);  // any IBufferWriter<byte>
```

For pre-allocated output buffers, use the `Span<double>` overload paired
with `GetDecompressedLength`:

```csharp
int n = AlpCompressor.GetDecompressedLength(compressed);
double[] buffer = ArrayPool<double>.Shared.Rent(n);
try
{
    AlpCompressor.Decompress(compressed, buffer);
    // ... consume buffer.AsSpan(0, n) ...
}
finally { ArrayPool<double>.Shared.Return(buffer); }
```

A few low-level utilities are also public for callers who want to
integrate ALP encoding into their own data formats without going
through the byte serialization:

- `AlpEncoder.FindBestFactorExponent(samples)` — returns the best
  `(exponent, factor)` pair for a sample, without performing the encoding
- `AlpEncoder.EncodeValue(value, exponent, factor)` — encode a single double to int64
- `AlpDecoder.DecodeValue(encoded, exponent, factor)` — decode a single int64 to double

The encode/FOR/bit-pack pipeline stages and the `AlpEncodedData`
intermediate form are internal implementation details.

## Target frameworks

The library targets `net8.0` and `netstandard2.0`. The netstandard2.0
build supplies polyfills for `BitOperations.LeadingZeroCount`,
`double.IsFinite`, and the `double` overloads of `BinaryPrimitives`, and
relies on `PolySharp` for `Index` / `Range` / `init` / `required`
attribute polyfills.

## Project layout

| Folder                       | Project           | Targets             |
|------------------------------|-------------------|---------------------|
| `src/Alp/`                   | `Clast.Alp`       | `net8.0`, `netstandard2.0` |
| `tests/Alp.Tests/`           | xunit test suite  | `net8.0`, `net472`  |
| `benchmarks/Alp.Benchmarks/` | BenchmarkDotNet   | `net8.0`, `net472`  |

The test and benchmark projects target `net472` in addition to `net8.0`
specifically to exercise the netstandard2.0 build of the library on the
.NET Framework runtime.

## Building, testing, benchmarking

```sh
dotnet build alp.sln
dotnet test tests/Alp.Tests
dotnet run -c Release --project benchmarks/Alp.Benchmarks
```

The benchmark project is configured with both a `net8.0` and a `net472`
runtime job, so a single invocation will compare performance across
both runtimes.

## License

Apache License, Version 2.0. See [LICENSE](LICENSE).
