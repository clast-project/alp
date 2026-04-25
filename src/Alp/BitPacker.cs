// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Clast.Alp;

/// <summary>
/// Packs and unpacks arrays of unsigned 64-bit values using a fixed bit-width per element.
/// </summary>
internal static class BitPacker
{
    /// <summary>
    /// Returns the number of bytes needed to pack <paramref name="count"/> values
    /// at <paramref name="bitWidth"/> bits each.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPackedSize(int count, int bitWidth)
    {
        if (bitWidth == 0 || count == 0)
            return 0;
        return (int)(((long)count * bitWidth + 7) / 8);
    }

    /// <summary>
    /// Packs <paramref name="values"/> into <paramref name="destination"/>,
    /// using <paramref name="bitWidth"/> bits per element.
    /// </summary>
    public static void Pack(ReadOnlySpan<ulong> values, int bitWidth, Span<byte> destination)
    {
        if (bitWidth == 0 || values.Length == 0)
            return;

        destination.Clear();

        if (bitWidth == 64)
        {
            Pack64(values, destination);
            return;
        }

        ulong mask = (1UL << bitWidth) - 1;
        int bitOffset = 0;

        for (int i = 0; i < values.Length; i++)
        {
            ulong val = values[i] & mask;
            int bytePos = bitOffset >> 3;
            int bitPos = bitOffset & 7;

            // Write the value starting at the current bit position.
            // A value of bitWidth bits starting at bitPos within a byte can span
            // up to ceil((bitPos + bitWidth) / 8) bytes, at most 9 for bitWidth=63 + bitPos=7.
            int bitsRemaining = bitWidth;
            int shift = 0;

            while (bitsRemaining > 0)
            {
                int bitsInThisByte = Math.Min(8 - bitPos, bitsRemaining);
                byte chunk = (byte)((val >> shift) & ((1U << bitsInThisByte) - 1));
                destination[bytePos] |= (byte)(chunk << bitPos);

                shift += bitsInThisByte;
                bitsRemaining -= bitsInThisByte;
                bytePos++;
                bitPos = 0;
            }

            bitOffset += bitWidth;
        }
    }

    /// <summary>
    /// Unpacks <paramref name="count"/> values of <paramref name="bitWidth"/> bits each
    /// from <paramref name="source"/> into <paramref name="destination"/>.
    /// </summary>
    public static void Unpack(ReadOnlySpan<byte> source, int bitWidth, int count, Span<ulong> destination)
    {
        if (bitWidth == 0 || count == 0)
        {
            destination[..count].Clear();
            return;
        }

        if (bitWidth == 64)
        {
            Unpack64(source, count, destination);
            return;
        }

        ulong mask = (1UL << bitWidth) - 1;
        int bitOffset = 0;

        for (int i = 0; i < count; i++)
        {
            int bytePos = bitOffset >> 3;
            int bitPos = bitOffset & 7;

            ulong val = 0;
            int bitsRemaining = bitWidth;
            int shift = 0;

            while (bitsRemaining > 0)
            {
                int bitsInThisByte = Math.Min(8 - bitPos, bitsRemaining);
                ulong chunk = (ulong)((source[bytePos] >> bitPos) & ((1 << bitsInThisByte) - 1));
                val |= chunk << shift;

                shift += bitsInThisByte;
                bitsRemaining -= bitsInThisByte;
                bytePos++;
                bitPos = 0;
            }

            destination[i] = val & mask;
            bitOffset += bitWidth;
        }
    }

    private static void Pack64(ReadOnlySpan<ulong> values, Span<byte> destination)
    {
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination[(i * 8)..], values[i]);
        }
    }

    private static void Unpack64(ReadOnlySpan<byte> source, int count, Span<ulong> destination)
    {
        for (int i = 0; i < count; i++)
        {
            destination[i] = BinaryPrimitives.ReadUInt64LittleEndian(source[(i * 8)..]);
        }
    }
}
