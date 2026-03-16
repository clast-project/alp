using System.Runtime.CompilerServices;

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
        double rounded = scaled + AlpConstants.MagicNumber - AlpConstants.MagicNumber;
        return (long)rounded;
    }

    /// <summary>
    /// Checks whether a value can potentially be encoded (not NaN, Inf, or out of int64 range).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanEncode(double value, int exponent, int factor)
    {
        if (!double.IsFinite(value))
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
    /// </summary>
    public static (int Exponent, int Factor) FindBestFactorExponent(ReadOnlySpan<double> samples)
    {
        int bestExponent = 0;
        int bestFactor = 0;
        int bestExceptions = samples.Length + 1;

        for (int e = 0; e <= AlpConstants.MaxExponent; e++)
        {
            int maxF = Math.Min(e, AlpConstants.MaxFactorIndex);
            double expMul = AlpConstants.ExpArray[e];

            for (int f = 0; f <= maxF; f++)
            {
                double fracMul = AlpConstants.FracArray[f];
                long factMul = AlpConstants.FactArray[f];
                double fracE = AlpConstants.FracArray[e];

                int exceptions = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    double v = samples[i];

                    if (!double.IsFinite(v))
                    {
                        if (++exceptions >= bestExceptions)
                            break;
                        continue;
                    }

                    double scaled = v * expMul * fracMul;
                    if (scaled <= AlpConstants.EncodingLowerLimit || scaled >= AlpConstants.EncodingUpperLimit)
                    {
                        if (++exceptions >= bestExceptions)
                            break;
                        continue;
                    }

                    double rounded = scaled + AlpConstants.MagicNumber - AlpConstants.MagicNumber;
                    double decoded = (long)rounded * factMul * fracE;

                    if (decoded != v)
                    {
                        if (++exceptions >= bestExceptions)
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
    public static AlpEncodedData Encode(ReadOnlySpan<double> values, int exponent, int factor)
    {
        long[] encoded = new long[values.Length];

        double expMul = AlpConstants.ExpArray[exponent];
        double fracMul = AlpConstants.FracArray[factor];
        long factMul = AlpConstants.FactArray[factor];
        double fracE = AlpConstants.FracArray[exponent];

        // First pass: encode all values, count exceptions, find a fill value
        long fillValue = 0;
        bool hasFill = false;
        int exceptionCount = 0;

        for (int i = 0; i < values.Length; i++)
        {
            double v = values[i];

            if (!double.IsFinite(v))
            {
                exceptionCount++;
                continue;
            }

            double scaled = v * expMul * fracMul;
            if (scaled <= AlpConstants.EncodingLowerLimit || scaled >= AlpConstants.EncodingUpperLimit)
            {
                exceptionCount++;
                continue;
            }

            double rounded = scaled + AlpConstants.MagicNumber - AlpConstants.MagicNumber;
            long enc = (long)rounded;
            double decoded = enc * factMul * fracE;

            if (decoded != v)
            {
                exceptionCount++;
            }
            else
            {
                encoded[i] = enc;
                if (!hasFill)
                {
                    fillValue = enc;
                    hasFill = true;
                }
            }
        }

        // Fast path: no exceptions
        if (exceptionCount == 0)
        {
            return new AlpEncodedData
            {
                EncodedValues = encoded,
                Exponent = exponent,
                Factor = factor,
                ExceptionValues = [],
                ExceptionPositions = [],
            };
        }

        // Second pass: collect exceptions and fill their slots
        double[] exceptionValues = new double[exceptionCount];
        int[] exceptionPositions = new int[exceptionCount];
        int ei = 0;

        for (int i = 0; i < values.Length; i++)
        {
            double v = values[i];

            if (!double.IsFinite(v))
            {
                exceptionValues[ei] = v;
                exceptionPositions[ei] = i;
                encoded[i] = fillValue;
                ei++;
                continue;
            }

            double scaled = v * expMul * fracMul;
            if (scaled <= AlpConstants.EncodingLowerLimit || scaled >= AlpConstants.EncodingUpperLimit)
            {
                exceptionValues[ei] = v;
                exceptionPositions[ei] = i;
                encoded[i] = fillValue;
                ei++;
                continue;
            }

            double rounded = scaled + AlpConstants.MagicNumber - AlpConstants.MagicNumber;
            long enc = (long)rounded;
            double decoded = enc * factMul * fracE;

            if (decoded != v)
            {
                exceptionValues[ei] = v;
                exceptionPositions[ei] = i;
                encoded[i] = fillValue;
                ei++;
            }
            else
            {
                encoded[i] = enc;
            }
        }

        return new AlpEncodedData
        {
            EncodedValues = encoded,
            Exponent = exponent,
            Factor = factor,
            ExceptionValues = exceptionValues,
            ExceptionPositions = exceptionPositions,
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
