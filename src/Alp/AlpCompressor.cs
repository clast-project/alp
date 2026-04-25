// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Buffers;
using System.Buffers.Binary;

namespace Clast.Alp;

/// <summary>
/// Complete ALP compression pipeline: ALP encode → FOR → bit-pack → serialize.
/// </summary>
/// <remarks>
/// Binary format (little-endian):
/// <code>
/// [0..3]    uint32  Magic (0x414C5031 = "ALP1")
/// [4]       byte    Version (1)
/// [5]       byte    Exponent
/// [6]       byte    Factor
/// [7]       byte    BitWidth
/// [8..15]   int64   FOR reference (min encoded value)
/// [16..19]  uint32  ValueCount
/// [20..21]  ushort  ExceptionCount
/// [22..23]  ushort  Reserved (0)
/// [24..]    byte[]  Bit-packed FOR deltas
/// [..]      ushort[]  Exception positions
/// [..]      double[]  Exception values
/// </code>
/// </remarks>
public static class AlpCompressor
{
    private const uint Magic = 0x414C5031; // "ALP1"
    private const byte Version = 1;
    private const int HeaderSize = 24;

    /// <summary>
    /// Compresses an array of doubles into a newly-allocated byte array.
    /// </summary>
    /// <remarks>
    /// The ALP1 binary format limits a single batch to <c>ushort.MaxValue</c> (65,535) values;
    /// callers compressing larger inputs must split them into multiple batches.
    /// </remarks>
    public static byte[] Compress(ReadOnlySpan<double> values)
    {
        ValidateBatchSize(values.Length);

        if (values.Length == 0)
        {
            byte[] empty = new byte[HeaderSize];
            WriteEmptyHeader(empty);
            return empty;
        }

        long[] encodedRented = ArrayPool<long>.Shared.Rent(values.Length);
        ulong[] deltasRented = ArrayPool<ulong>.Shared.Rent(values.Length);
        try
        {
            EncodeAndPackForFor(values, encodedRented, deltasRented,
                out int exponent, out int factor, out long reference, out int bitWidth,
                out int[] exceptionPositions, out double[] exceptionValues);

            int totalSize = ComputeCompressedSize(values.Length, bitWidth, exceptionPositions.Length);
            byte[] output = new byte[totalSize];
            WriteCompressed(output, values.Length, exponent, factor, reference, bitWidth,
                deltasRented.AsSpan(0, values.Length), exceptionPositions, exceptionValues);
            return output;
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(deltasRented);
            ArrayPool<long>.Shared.Return(encodedRented);
        }
    }

    /// <summary>
    /// Compresses an array of doubles into a caller-supplied <see cref="IBufferWriter{T}"/>.
    /// Returns the number of bytes written.
    /// </summary>
    public static int Compress(ReadOnlySpan<double> values, IBufferWriter<byte> writer)
    {
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));
        ValidateBatchSize(values.Length);

        if (values.Length == 0)
        {
            Span<byte> emptyDest = writer.GetSpan(HeaderSize);
            WriteEmptyHeader(emptyDest);
            writer.Advance(HeaderSize);
            return HeaderSize;
        }

        long[] encodedRented = ArrayPool<long>.Shared.Rent(values.Length);
        ulong[] deltasRented = ArrayPool<ulong>.Shared.Rent(values.Length);
        try
        {
            EncodeAndPackForFor(values, encodedRented, deltasRented,
                out int exponent, out int factor, out long reference, out int bitWidth,
                out int[] exceptionPositions, out double[] exceptionValues);

            int totalSize = ComputeCompressedSize(values.Length, bitWidth, exceptionPositions.Length);
            Span<byte> destination = writer.GetSpan(totalSize);
            WriteCompressed(destination, values.Length, exponent, factor, reference, bitWidth,
                deltasRented.AsSpan(0, values.Length), exceptionPositions, exceptionValues);
            writer.Advance(totalSize);
            return totalSize;
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(deltasRented);
            ArrayPool<long>.Shared.Return(encodedRented);
        }
    }

    /// <summary>
    /// Returns the number of double values that <see cref="Decompress(ReadOnlySpan{byte})"/>
    /// would produce for the given compressed data.
    /// </summary>
    public static int GetDecompressedLength(ReadOnlySpan<byte> compressed)
    {
        ValidateHeader(compressed);
        return (int)BinaryPrimitives.ReadUInt32LittleEndian(compressed[16..]);
    }

    /// <summary>
    /// Decompresses bytes back to a newly-allocated double array.
    /// </summary>
    public static double[] Decompress(ReadOnlySpan<byte> compressed)
    {
        int valueCount = GetDecompressedLength(compressed);
        if (valueCount == 0)
            return [];

        double[] result = new double[valueCount];
        DecompressCore(compressed, valueCount, result);
        return result;
    }

    /// <summary>
    /// Decompresses bytes into a caller-supplied destination span.
    /// Returns the number of doubles written. Use
    /// <see cref="GetDecompressedLength(ReadOnlySpan{byte})"/> to size the destination.
    /// </summary>
    public static int Decompress(ReadOnlySpan<byte> compressed, Span<double> destination)
    {
        int valueCount = GetDecompressedLength(compressed);
        if (destination.Length < valueCount)
            throw new ArgumentException(
                $"Destination is too small: needs {valueCount}, got {destination.Length}.",
                nameof(destination));

        if (valueCount > 0)
            DecompressCore(compressed, valueCount, destination);
        return valueCount;
    }

    private static void EncodeAndPackForFor(
        ReadOnlySpan<double> values, long[] encodedBuffer, ulong[] deltasBuffer,
        out int exponent, out int factor, out long reference, out int bitWidth,
        out int[] exceptionPositions, out double[] exceptionValues)
    {
        (exponent, factor) = PickFactorExponent(values);
        AlpEncoder.EncodeInto(values, exponent, factor,
            encodedBuffer.AsSpan(0, values.Length),
            out exceptionPositions, out exceptionValues);
        ForCompressor.EncodeInto(
            encodedBuffer.AsSpan(0, values.Length),
            deltasBuffer.AsSpan(0, values.Length),
            out reference, out bitWidth);
    }

    // Search the full input for small batches; for large batches, sample 32 strided
    // elements. The chosen (exponent, factor) may classify a few extra values as
    // exceptions versus a full search, but exceptions are stored verbatim so
    // round-trip correctness is preserved.
    private static (int Exponent, int Factor) PickFactorExponent(ReadOnlySpan<double> values)
    {
        const int SampleSize = 32;

        if (values.Length <= SampleSize)
            return AlpEncoder.FindBestFactorExponent(values);

        Span<double> samples = stackalloc double[SampleSize];
        int stride = values.Length / SampleSize;
        for (int i = 0; i < SampleSize; i++)
            samples[i] = values[i * stride];
        return AlpEncoder.FindBestFactorExponent(samples);
    }

    private static void DecompressCore(ReadOnlySpan<byte> compressed, int valueCount, Span<double> destination)
    {
        int exponent = compressed[5];
        int factor = compressed[6];
        int bitWidth = compressed[7];
        long reference = BinaryPrimitives.ReadInt64LittleEndian(compressed[8..]);
        int exceptionCount = BinaryPrimitives.ReadUInt16LittleEndian(compressed[20..]);
        int packedSize = BitPacker.GetPackedSize(valueCount, bitWidth);

        ulong[] deltasRented = ArrayPool<ulong>.Shared.Rent(valueCount);
        long[] encodedRented = ArrayPool<long>.Shared.Rent(valueCount);
        try
        {
            if (packedSize > 0)
            {
                BitPacker.Unpack(compressed[HeaderSize..], bitWidth, valueCount,
                    deltasRented.AsSpan(0, valueCount));
            }
            ForCompressor.Decode(
                deltasRented.AsSpan(0, valueCount), reference,
                encodedRented.AsSpan(0, valueCount));

            int offset = HeaderSize + packedSize;
            int[] exceptionPositions = new int[exceptionCount];
            double[] exceptionValues = new double[exceptionCount];
            for (int i = 0; i < exceptionCount; i++)
            {
                exceptionPositions[i] = BinaryPrimitives.ReadUInt16LittleEndian(compressed[offset..]);
                offset += sizeof(ushort);
            }
            for (int i = 0; i < exceptionCount; i++)
            {
                exceptionValues[i] = Polyfill.ReadDoubleLittleEndian(compressed[offset..]);
                offset += sizeof(double);
            }

            AlpDecoder.DecodeInto(
                encodedRented.AsSpan(0, valueCount), exponent, factor,
                exceptionPositions, exceptionValues,
                destination.Slice(0, valueCount));
        }
        finally
        {
            ArrayPool<long>.Shared.Return(encodedRented);
            ArrayPool<ulong>.Shared.Return(deltasRented);
        }
    }

    private static int ComputeCompressedSize(int valueCount, int bitWidth, int exceptionCount) =>
        HeaderSize
            + BitPacker.GetPackedSize(valueCount, bitWidth)
            + exceptionCount * (sizeof(ushort) + sizeof(double));

    private static void WriteCompressed(Span<byte> destination, int valueCount,
        int exponent, int factor, long reference, int bitWidth,
        ReadOnlySpan<ulong> deltas, int[] exceptionPositions, double[] exceptionValues)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, Magic);
        destination[4] = Version;
        destination[5] = (byte)exponent;
        destination[6] = (byte)factor;
        destination[7] = (byte)bitWidth;
        BinaryPrimitives.WriteInt64LittleEndian(destination[8..], reference);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], (uint)valueCount);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[20..], (ushort)exceptionPositions.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[22..], 0); // reserved

        int packedSize = BitPacker.GetPackedSize(valueCount, bitWidth);
        if (packedSize > 0)
        {
            BitPacker.Pack(deltas, bitWidth, destination[HeaderSize..]);
        }

        int offset = HeaderSize + packedSize;
        int exceptionCount = exceptionPositions.Length;
        for (int i = 0; i < exceptionCount; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination[offset..], (ushort)exceptionPositions[i]);
            offset += sizeof(ushort);
        }
        for (int i = 0; i < exceptionCount; i++)
        {
            Polyfill.WriteDoubleLittleEndian(destination[offset..], exceptionValues[i]);
            offset += sizeof(double);
        }
    }

    private static void ValidateBatchSize(int valueCount)
    {
        if (valueCount > ushort.MaxValue)
            throw new ArgumentException(
                $"ALP1 supports at most {ushort.MaxValue} values per batch; got {valueCount}.",
                "values");
    }

    private static void ValidateHeader(ReadOnlySpan<byte> compressed)
    {
        if (compressed.Length < HeaderSize)
            throw new ArgumentException("Data is too short to contain a valid ALP header.");

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(compressed);
        if (magic != Magic)
            throw new ArgumentException($"Invalid magic number: 0x{magic:X8}");

        byte version = compressed[4];
        if (version != Version)
            throw new ArgumentException($"Unsupported version: {version}");
    }

    private static void WriteEmptyHeader(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, Magic);
        destination[4] = Version;
        destination[5] = 0; // exponent
        destination[6] = 0; // factor
        destination[7] = 0; // bitWidth
        BinaryPrimitives.WriteInt64LittleEndian(destination[8..], 0L); // reference
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], 0u); // valueCount
        BinaryPrimitives.WriteUInt16LittleEndian(destination[20..], 0); // exceptionCount
        BinaryPrimitives.WriteUInt16LittleEndian(destination[22..], 0); // reserved
    }
}
