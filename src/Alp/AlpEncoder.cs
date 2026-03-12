using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Alp;

/// <summary>
/// ALP (Adaptive Lossless floating-Point) encoder.
/// Encodes double-precision floating-point values as int64 integers
/// by multiplying by powers of 10 and rounding.
/// </summary>
public static class AlpEncoder
{
    /// <summary>
    /// Encodes a single double value as an int64 using the given exponent and factor indices.
    /// Uses the "magic number" fast-rounding trick.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long EncodeValue(double value, int exponent, int factor)
    {
        double scaled = value * AlpConstants.ExpArray[exponent] * AlpConstants.FracArray[factor];
        // Fast round via magic number trick (banker's rounding)
        double rounded = scaled + AlpConstants.MagicNumber - AlpConstants.MagicNumber;
        return (long)rounded;
    }

    /// <summary>
    /// Checks whether a value can potentially be encoded (not NaN, Inf, or out of int64 range).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanEncode(double value, int exponent, int factor)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return false;

        double scaled = value * AlpConstants.ExpArray[exponent] * AlpConstants.FracArray[factor];
        return scaled > AlpConstants.EncodingLowerLimit && scaled < AlpConstants.EncodingUpperLimit;
    }

    /// <summary>
    /// Checks whether encoding and then decoding a value produces the exact original value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RoundTrips(double value, int exponent, int factor)
    {
        if (!CanEncode(value, exponent, factor))
            return false;

        long encoded = EncodeValue(value, exponent, factor);
        double decoded = AlpDecoder.DecodeValue(encoded, exponent, factor);
        return decoded == value;
    }

    /// <summary>
    /// Finds the best (exponent, factor) combination for a sample of values.
    /// The best combination minimizes the number of exceptions (values that don't round-trip).
    /// Among combinations with equal exception counts, prefers smaller (e - f) to reduce bit-width.
    /// </summary>
    /// <returns>The best (exponent, factor) pair.</returns>
    public static (int Exponent, int Factor) FindBestFactorExponent(ReadOnlySpan<double> samples)
    {
        int bestExponent = 0;
        int bestFactor = 0;
        int bestExceptions = samples.Length + 1;

        for (int e = 0; e <= AlpConstants.MaxExponent; e++)
        {
            // Factor is bounded by e and by MaxFactorIndex (largest int64 power of 10)
            int maxF = Math.Min(e, AlpConstants.MaxFactorIndex);
            for (int f = 0; f <= maxF; f++)
            {
                int exceptions = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (!RoundTrips(samples[i], e, f))
                    {
                        exceptions++;
                        // Early exit: this combination is already worse
                        if (exceptions >= bestExceptions)
                            break;
                    }
                }

                if (exceptions < bestExceptions)
                {
                    bestExceptions = exceptions;
                    bestExponent = e;
                    bestFactor = f;

                    if (bestExceptions == 0)
                        return (bestExponent, bestFactor);
                }
            }
        }

        return (bestExponent, bestFactor);
    }

    /// <summary>
    /// Encodes an array of double values as int64 values using the given exponent and factor.
    /// Values that do not round-trip are recorded as exceptions.
    /// </summary>
    /// <param name="values">Input double values.</param>
    /// <param name="exponent">The exponent index to use.</param>
    /// <param name="factor">The factor index to use.</param>
    /// <returns>An <see cref="AlpEncodedData"/> containing the encoded integers and any exceptions.</returns>
    public static AlpEncodedData Encode(ReadOnlySpan<double> values, int exponent, int factor)
    {
        long[] encoded = new long[values.Length];
        var exceptionValues = new List<double>();
        var exceptionPositions = new List<int>();

        // First pass: find a non-exception value to use as fill for exception slots
        long fillValue = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (RoundTrips(values[i], exponent, factor))
            {
                fillValue = EncodeValue(values[i], exponent, factor);
                break;
            }
        }

        // Second pass: encode all values
        for (int i = 0; i < values.Length; i++)
        {
            if (RoundTrips(values[i], exponent, factor))
            {
                encoded[i] = EncodeValue(values[i], exponent, factor);
            }
            else
            {
                // Exception: store the original value and fill the encoded slot
                exceptionValues.Add(values[i]);
                exceptionPositions.Add(i);
                encoded[i] = fillValue;
            }
        }

        return new AlpEncodedData
        {
            EncodedValues = encoded,
            Exponent = exponent,
            Factor = factor,
            ExceptionValues = exceptionValues.ToArray(),
            ExceptionPositions = exceptionPositions.ToArray(),
        };
    }

    /// <summary>
    /// Convenience method: finds the best (exponent, factor) and encodes the values.
    /// </summary>
    public static AlpEncodedData Encode(ReadOnlySpan<double> values)
    {
        var (exponent, factor) = FindBestFactorExponent(values);
        return Encode(values, exponent, factor);
    }
}
