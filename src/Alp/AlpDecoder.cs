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

        // Bulk decode all encoded integers
        for (int i = 0; i < data.EncodedValues.Length; i++)
        {
            result[i] = DecodeValue(data.EncodedValues[i], data.Exponent, data.Factor);
        }

        // Patch exceptions back in
        for (int i = 0; i < data.ExceptionPositions.Length; i++)
        {
            result[data.ExceptionPositions[i]] = data.ExceptionValues[i];
        }

        return result;
    }
}
