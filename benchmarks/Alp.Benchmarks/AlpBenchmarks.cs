// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Clast.Alp.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net472)]
public class AlpBenchmarks
{
    private double[] _decimals2 = null!;
    private double[] _decimals6 = null!;
    private double[] _integers = null!;
    private double[] _mixed = null!;

    private AlpEncodedData _encodedDecimals2 = null!;
    private AlpEncodedData _encodedDecimals6 = null!;
    private AlpEncodedData _encodedIntegers = null!;
    private AlpEncodedData _encodedMixed = null!;

    private byte[] _compressedDecimals2 = null!;
    private byte[] _compressedDecimals6 = null!;
    private byte[] _compressedIntegers = null!;
    private byte[] _compressedMixed = null!;

    // Pre-computed FOR inputs for isolated FOR benchmarks
    private long[] _forInputIntegers = null!;
    private long[] _forInputDecimals2 = null!;
    private (long Reference, int BitWidth, ulong[] Deltas) _forEncodedIntegers;
    private (long Reference, int BitWidth, ulong[] Deltas) _forEncodedDecimals2;

    [Params(1024, 8192)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);

        _decimals2 = new double[N];
        _decimals6 = new double[N];
        _integers = new double[N];
        _mixed = new double[N];

        for (int i = 0; i < N; i++)
        {
            _decimals2[i] = Math.Round(rng.NextDouble() * 1000, 2);
            _decimals6[i] = Math.Round(rng.NextDouble() * 1000, 6);
            _integers[i] = rng.Next(-10000, 10000);
            _mixed[i] = i % 10 == 0
                ? double.NaN
                : Math.Round(rng.NextDouble() * 100, 3);
        }

        _encodedDecimals2 = AlpEncoder.Encode(_decimals2);
        _encodedDecimals6 = AlpEncoder.Encode(_decimals6);
        _encodedIntegers = AlpEncoder.Encode(_integers);
        _encodedMixed = AlpEncoder.Encode(_mixed);

        _compressedDecimals2 = AlpCompressor.Compress(_decimals2);
        _compressedDecimals6 = AlpCompressor.Compress(_decimals6);
        _compressedIntegers = AlpCompressor.Compress(_integers);
        _compressedMixed = AlpCompressor.Compress(_mixed);

        // FOR inputs: the encoded long[] values from ALP
        _forInputIntegers = _encodedIntegers.EncodedValues;
        _forInputDecimals2 = _encodedDecimals2.EncodedValues;
        _forEncodedIntegers = ForCompressor.Encode(_forInputIntegers);
        _forEncodedDecimals2 = ForCompressor.Encode(_forInputDecimals2);
    }

    // --- FindBestFactorExponent ---

    [Benchmark]
    public (int, int) FindBest_Decimals2() => AlpEncoder.FindBestFactorExponent(_decimals2);

    [Benchmark]
    public (int, int) FindBest_Decimals6() => AlpEncoder.FindBestFactorExponent(_decimals6);

    [Benchmark]
    public (int, int) FindBest_Integers() => AlpEncoder.FindBestFactorExponent(_integers);

    // --- Encode ---

    [Benchmark]
    public object Encode_Decimals2() => AlpEncoder.Encode(_decimals2);

    [Benchmark]
    public object Encode_Decimals6() => AlpEncoder.Encode(_decimals6);

    [Benchmark]
    public object Encode_Integers() => AlpEncoder.Encode(_integers);

    [Benchmark]
    public object Encode_Mixed() => AlpEncoder.Encode(_mixed);

    // --- Decode ---

    [Benchmark]
    public double[] Decode_Decimals2() => AlpDecoder.Decode(_encodedDecimals2);

    [Benchmark]
    public double[] Decode_Decimals6() => AlpDecoder.Decode(_encodedDecimals6);

    [Benchmark]
    public double[] Decode_Integers() => AlpDecoder.Decode(_encodedIntegers);

    [Benchmark]
    public double[] Decode_Mixed() => AlpDecoder.Decode(_encodedMixed);

    // --- Compress (full pipeline) ---

    [Benchmark]
    public byte[] Compress_Decimals2() => AlpCompressor.Compress(_decimals2);

    [Benchmark]
    public byte[] Compress_Decimals6() => AlpCompressor.Compress(_decimals6);

    [Benchmark]
    public byte[] Compress_Integers() => AlpCompressor.Compress(_integers);

    [Benchmark]
    public byte[] Compress_Mixed() => AlpCompressor.Compress(_mixed);

    // --- Decompress (full pipeline) ---

    [Benchmark]
    public double[] Decompress_Decimals2() => AlpCompressor.Decompress(_compressedDecimals2);

    [Benchmark]
    public double[] Decompress_Decimals6() => AlpCompressor.Decompress(_compressedDecimals6);

    [Benchmark]
    public double[] Decompress_Integers() => AlpCompressor.Decompress(_compressedIntegers);

    [Benchmark]
    public double[] Decompress_Mixed() => AlpCompressor.Decompress(_compressedMixed);

    // --- FOR Encode (isolated) ---

    [Benchmark]
    public (long, int, ulong[]) ForEncode_Integers() => ForCompressor.Encode(_forInputIntegers);

    [Benchmark]
    public (long, int, ulong[]) ForEncode_Decimals2() => ForCompressor.Encode(_forInputDecimals2);

    // --- FOR Decode (isolated) ---

    [Benchmark]
    public long[] ForDecode_Integers()
    {
        long[] result = new long[_forEncodedIntegers.Deltas.Length];
        ForCompressor.Decode(_forEncodedIntegers.Deltas, _forEncodedIntegers.Reference, result);
        return result;
    }

    [Benchmark]
    public long[] ForDecode_Decimals2()
    {
        long[] result = new long[_forEncodedDecimals2.Deltas.Length];
        ForCompressor.Decode(_forEncodedDecimals2.Deltas, _forEncodedDecimals2.Reference, result);
        return result;
    }

    // --- Single value encode/decode (throughput) ---

    [Benchmark]
    public long EncodeValue_Single() => AlpEncoder.EncodeValue(1.23, 2, 0);

    [Benchmark]
    public double DecodeValue_Single() => AlpDecoder.DecodeValue(123, 2, 0);
}
