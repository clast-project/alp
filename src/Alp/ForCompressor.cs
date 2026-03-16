using System.Numerics;

namespace Alp;

/// <summary>
/// Frame-of-Reference (FOR) compression: subtracts a reference (minimum) value
/// from all elements, producing small unsigned deltas that can be bit-packed.
/// </summary>
public static class ForCompressor
{
    /// <summary>
    /// FOR-encodes a span of long values by subtracting the minimum.
    /// Returns the reference value, the bit-width needed, and the unsigned deltas.
    /// </summary>
    public static (long Reference, int BitWidth, ulong[] Deltas) Encode(ReadOnlySpan<long> values)
    {
        if (values.Length == 0)
            return (0, 0, []);

        long min = values[0];
        long max = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] < min) min = values[i];
            if (values[i] > max) max = values[i];
        }

        ulong range = (ulong)(max - min);
        int bitWidth = range == 0 ? 0 : 64 - BitOperations.LeadingZeroCount(range);

        ulong[] deltas = new ulong[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            deltas[i] = (ulong)(values[i] - min);
        }

        return (min, bitWidth, deltas);
    }

    /// <summary>
    /// FOR-decodes unsigned deltas back to long values by adding the reference.
    /// </summary>
    public static void Decode(ReadOnlySpan<ulong> deltas, long reference, Span<long> destination)
    {
        for (int i = 0; i < deltas.Length; i++)
        {
            destination[i] = (long)deltas[i] + reference;
        }
    }
}
