// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

namespace Clast.Alp;

/// <summary>
/// ALP decoder: reconstructs double values from encoded int64 integers.
/// </summary>
public static class AlpDecoder
{
    /// <summary>
    /// Decodes a single int64 encoded value back to a double.
    /// <paramref name="exponent"/> must be in [0, 23]; <paramref name="factor"/> must be in [0, 18].
    /// Formula: encoded * FACT_ARR[factor] * FRAC_ARR[exponent]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DecodeValue(long encoded, int exponent, int factor)
    {
        if ((uint)exponent > AlpConstants.MaxExponent)
            throw new ArgumentOutOfRangeException(nameof(exponent), exponent, "Must be in [0, 23].");
        if ((uint)factor > AlpConstants.MaxFactorIndex)
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Must be in [0, 18].");

        return encoded * AlpConstants.FactArray[factor] * AlpConstants.FracArray[exponent];
    }

    /// <summary>
    /// Decodes <paramref name="encodedValues"/> into <paramref name="destination"/> and patches
    /// the exception positions with the original double values.
    /// </summary>
    internal static void DecodeInto(
        ReadOnlySpan<long> encodedValues, int exponent, int factor,
        ReadOnlySpan<int> exceptionPositions, ReadOnlySpan<double> exceptionValues,
        Span<double> destination)
    {
        long factMul = AlpConstants.FactArray[factor];
        double fracE = AlpConstants.FracArray[exponent];

        for (int i = 0; i < encodedValues.Length; i++)
        {
            destination[i] = encodedValues[i] * factMul * fracE;
        }

        for (int i = 0; i < exceptionPositions.Length; i++)
        {
            destination[exceptionPositions[i]] = exceptionValues[i];
        }
    }

    /// <summary>
    /// Decodes all values from an <see cref="AlpEncodedData"/>, patching in exceptions.
    /// </summary>
    internal static double[] Decode(AlpEncodedData data)
    {
        double[] result = new double[data.EncodedValues.Length];
        DecodeInto(data.EncodedValues, data.Exponent, data.Factor,
            data.ExceptionPositions, data.ExceptionValues, result);
        return result;
    }
}
