namespace Alp;

/// <summary>
/// Holds the result of ALP encoding: the encoded integer values,
/// the (exponent, factor) pair used, and any exception values that
/// could not be losslessly encoded.
/// </summary>
public class AlpEncodedData
{
    /// <summary>
    /// The encoded int64 values. Exception slots contain a fill value.
    /// </summary>
    public required long[] EncodedValues { get; init; }

    /// <summary>
    /// The exponent index used for encoding.
    /// </summary>
    public required int Exponent { get; init; }

    /// <summary>
    /// The factor index used for encoding.
    /// </summary>
    public required int Factor { get; init; }

    /// <summary>
    /// Original double values that could not be losslessly round-tripped.
    /// </summary>
    public required double[] ExceptionValues { get; init; }

    /// <summary>
    /// Positions (indices) of exception values in the original array.
    /// </summary>
    public required int[] ExceptionPositions { get; init; }

    /// <summary>
    /// Number of exceptions.
    /// </summary>
    public int ExceptionCount => ExceptionPositions.Length;
}
