// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

namespace Clast.Alp;

/// <summary>
/// ALP (Adaptive Lossless floating-Point) encoder.
/// Encodes double-precision floating-point values as int64 integers
/// by multiplying by powers of 10 and rounding.
/// </summary>
public static class AlpEncoder
{
    /// <summary>
    /// Encodes a single double value as an int64 using the given exponent and factor indices.
    /// Both <paramref name="exponent"/> and <paramref name="factor"/> must be in [0, 23].
    /// Uses the "magic number" fast-rounding trick.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long EncodeValue(double value, int exponent, int factor)
    {
        if ((uint)exponent > AlpConstants.MaxExponent)
            throw new ArgumentOutOfRangeException(nameof(exponent), exponent, "Must be in [0, 23].");
        if ((uint)factor > AlpConstants.MaxFactor)
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Must be in [0, 23].");

        double scaled = value * AlpConstants.ExpArray[exponent] * AlpConstants.FracArray[factor];
        double rounded = scaled + AlpConstants.MagicNumber - AlpConstants.MagicNumber;
        return (long)rounded;
    }

    /// <summary>
    /// Checks whether a value can potentially be encoded (not NaN, Inf, or out of int64 range).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanEncode(double value, int exponent, int factor)
    {
        if (!Polyfill.IsFinite(value))
            return false;

        double scaled = value * AlpConstants.ExpArray[exponent] * AlpConstants.FracArray[factor];
        return scaled > AlpConstants.EncodingLowerLimit && scaled < AlpConstants.EncodingUpperLimit;
    }

    /// <summary>
    /// Checks whether encoding and then decoding a value produces the exact original value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool RoundTrips(double value, int exponent, int factor)
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
            double fracE = AlpConstants.FracArray[e];

            for (int f = 0; f <= maxF; f++)
            {
                double fracMul = AlpConstants.FracArray[f];
                long factMul = AlpConstants.FactArray[f];

                // Hoist the products that don't depend on the per-sample value.
                double expFracMul = expMul * fracMul;
                double factFracE = (double)factMul * fracE;

                int exceptions = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    double v = samples[i];

                    if (!Polyfill.IsFinite(v))
                    {
                        if (++exceptions >= bestExceptions)
                            break;
                        continue;
                    }

                    double scaled = v * expFracMul;
                    if (scaled <= AlpConstants.EncodingLowerLimit || scaled >= AlpConstants.EncodingUpperLimit)
                    {
                        if (++exceptions >= bestExceptions)
                            break;
                        continue;
                    }

                    double rounded = scaled + AlpConstants.MagicNumber - AlpConstants.MagicNumber;
                    double decoded = (long)rounded * factFracE;

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
    /// Encodes <paramref name="values"/> into <paramref name="destination"/> using the given
    /// exponent and factor. Values that do not round-trip are recorded as exceptions and
    /// their slots in <paramref name="destination"/> are filled with a sentinel encoded value.
    /// Only the first <c>values.Length</c> entries of <paramref name="destination"/> are written.
    /// </summary>
    internal static void EncodeInto(
        ReadOnlySpan<double> values, int exponent, int factor,
        Span<long> destination,
        out int[] exceptionPositions, out double[] exceptionValues)
    {
        double expMul = AlpConstants.ExpArray[exponent];
        double fracMul = AlpConstants.FracArray[factor];
        long factMul = AlpConstants.FactArray[factor];
        double fracE = AlpConstants.FracArray[exponent];

        long fillValue = 0;
        bool hasFill = false;
        int exceptionCount = 0;

        for (int i = 0; i < values.Length; i++)
        {
            double v = values[i];

            if (!Polyfill.IsFinite(v))
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
                destination[i] = enc;
                if (!hasFill)
                {
                    fillValue = enc;
                    hasFill = true;
                }
            }
        }

        if (exceptionCount == 0)
        {
            exceptionPositions = [];
            exceptionValues = [];
            return;
        }

        exceptionPositions = new int[exceptionCount];
        exceptionValues = new double[exceptionCount];
        int ei = 0;

        for (int i = 0; i < values.Length; i++)
        {
            double v = values[i];

            if (!Polyfill.IsFinite(v))
            {
                exceptionValues[ei] = v;
                exceptionPositions[ei] = i;
                destination[i] = fillValue;
                ei++;
                continue;
            }

            double scaled = v * expMul * fracMul;
            if (scaled <= AlpConstants.EncodingLowerLimit || scaled >= AlpConstants.EncodingUpperLimit)
            {
                exceptionValues[ei] = v;
                exceptionPositions[ei] = i;
                destination[i] = fillValue;
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
                destination[i] = fillValue;
                ei++;
            }
            else
            {
                destination[i] = enc;
            }
        }
    }

    /// <summary>
    /// Encodes an array of double values as int64 values using the given exponent and factor.
    /// Values that do not round-trip are recorded as exceptions.
    /// </summary>
    internal static AlpEncodedData Encode(ReadOnlySpan<double> values, int exponent, int factor)
    {
        long[] encoded = new long[values.Length];
        EncodeInto(values, exponent, factor, encoded,
            out int[] exceptionPositions, out double[] exceptionValues);
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
    internal static AlpEncodedData Encode(ReadOnlySpan<double> values)
    {
        var (exponent, factor) = FindBestFactorExponent(values);
        return Encode(values, exponent, factor);
    }
}
