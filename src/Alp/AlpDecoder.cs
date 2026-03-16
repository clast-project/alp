using System.Runtime.CompilerServices;

namespace Alp;

/// <summary>
/// ALP decoder: reconstructs double values from encoded int64 integers.
/// </summary>
public static class AlpDecoder
{
    /// <summary>
    /// Decodes a single int64 encoded value back to a double.
    /// Formula: encoded * FACT_ARR[factor] * FRAC_ARR[exponent]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DecodeValue(long encoded, int exponent, int factor)
    {
        return encoded * AlpConstants.FactArray[factor] * AlpConstants.FracArray[exponent];
    }

    /// <summary>
    /// Decodes all values from an <see cref="AlpEncodedData"/>, patching in exceptions.
    /// </summary>
    public static double[] Decode(AlpEncodedData data)
    {
        double[] result = new double[data.EncodedValues.Length];
        long[] encodedValues = data.EncodedValues;
        long factMul = AlpConstants.FactArray[data.Factor];
        double fracE = AlpConstants.FracArray[data.Exponent];

        for (int i = 0; i < encodedValues.Length; i++)
        {
            result[i] = encodedValues[i] * factMul * fracE;
        }

        int[] positions = data.ExceptionPositions;
        double[] exceptions = data.ExceptionValues;
        for (int i = 0; i < positions.Length; i++)
        {
            result[positions[i]] = exceptions[i];
        }

        return result;
    }
}
