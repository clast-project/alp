// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Numerics;

namespace Clast.Alp;

/// <summary>
/// Frame-of-Reference (FOR) compression: subtracts a reference (minimum) value
/// from all elements, producing small unsigned deltas that can be bit-packed.
/// </summary>
internal static class ForCompressor
{
    /// <summary>
    /// FOR-encodes <paramref name="values"/> into <paramref name="destination"/> by subtracting
    /// the minimum value. Only the first <c>values.Length</c> entries of
    /// <paramref name="destination"/> are written.
    /// </summary>
    internal static void EncodeInto(
        ReadOnlySpan<long> values, Span<ulong> destination,
        out long reference, out int bitWidth)
    {
        if (values.Length == 0)
        {
            reference = 0;
            bitWidth = 0;
            return;
        }

        long min = values[0];
        long max = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] < min) min = values[i];
            if (values[i] > max) max = values[i];
        }

        ulong range = (ulong)(max - min);
        bitWidth = range == 0 ? 0 : 64 - BitOperations.LeadingZeroCount(range);
        reference = min;

        for (int i = 0; i < values.Length; i++)
        {
            destination[i] = (ulong)(values[i] - min);
        }
    }

    /// <summary>
    /// FOR-encodes a span of long values by subtracting the minimum.
    /// Returns the reference value, the bit-width needed, and the unsigned deltas.
    /// </summary>
    internal static (long Reference, int BitWidth, ulong[] Deltas) Encode(ReadOnlySpan<long> values)
    {
        if (values.Length == 0)
            return (0, 0, []);
        ulong[] deltas = new ulong[values.Length];
        EncodeInto(values, deltas, out long reference, out int bitWidth);
        return (reference, bitWidth, deltas);
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
