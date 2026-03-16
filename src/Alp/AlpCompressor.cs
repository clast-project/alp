using System.Buffers.Binary;

namespace Alp;

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
    /// Compresses an array of doubles into a compact byte representation.
    /// </summary>
    public static byte[] Compress(ReadOnlySpan<double> values)
    {
        if (values.Length == 0)
            return WriteHeader(0, 0, 0, 0, 0, 0);

        // Step 1: ALP encode (doubles → int64s + exceptions)
        var alpData = AlpEncoder.Encode(values);

        // Step 2: FOR encode (int64s → unsigned deltas)
        var (reference, bitWidth, deltas) = ForCompressor.Encode(alpData.EncodedValues);

        // Step 3: Calculate sizes and allocate
        int packedSize = BitPacker.GetPackedSize(values.Length, bitWidth);
        int exceptionCount = alpData.ExceptionCount;
        int totalSize = HeaderSize
            + packedSize
            + exceptionCount * sizeof(ushort)
            + exceptionCount * sizeof(double);

        byte[] output = new byte[totalSize];
        var span = output.AsSpan();

        // Step 4: Write header
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
        span[4] = Version;
        span[5] = (byte)alpData.Exponent;
        span[6] = (byte)alpData.Factor;
        span[7] = (byte)bitWidth;
        BinaryPrimitives.WriteInt64LittleEndian(span[8..], reference);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], (uint)values.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], (ushort)exceptionCount);
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], 0); // reserved

        // Step 5: Bit-pack FOR deltas
        if (packedSize > 0)
        {
            BitPacker.Pack(deltas, bitWidth, span[HeaderSize..]);
        }

        // Step 6: Write exceptions
        int offset = HeaderSize + packedSize;
        for (int i = 0; i < exceptionCount; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], (ushort)alpData.ExceptionPositions[i]);
            offset += sizeof(ushort);
        }
        for (int i = 0; i < exceptionCount; i++)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(span[offset..], alpData.ExceptionValues[i]);
            offset += sizeof(double);
        }

        return output;
    }

    /// <summary>
    /// Decompresses bytes back to the original double array.
    /// </summary>
    public static double[] Decompress(ReadOnlySpan<byte> compressed)
    {
        if (compressed.Length < HeaderSize)
            throw new ArgumentException("Data is too short to contain a valid ALP header.");

        // Step 1: Read and validate header
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(compressed);
        if (magic != Magic)
            throw new ArgumentException($"Invalid magic number: 0x{magic:X8}");

        byte version = compressed[4];
        if (version != Version)
            throw new ArgumentException($"Unsupported version: {version}");

        int exponent = compressed[5];
        int factor = compressed[6];
        int bitWidth = compressed[7];
        long reference = BinaryPrimitives.ReadInt64LittleEndian(compressed[8..]);
        int valueCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(compressed[16..]);
        int exceptionCount = BinaryPrimitives.ReadUInt16LittleEndian(compressed[20..]);

        if (valueCount == 0)
            return [];

        // Step 2: Unpack FOR deltas
        int packedSize = BitPacker.GetPackedSize(valueCount, bitWidth);
        ulong[] deltas = new ulong[valueCount];
        if (packedSize > 0)
        {
            BitPacker.Unpack(compressed[HeaderSize..], bitWidth, valueCount, deltas);
        }

        // Step 3: FOR decode (deltas → encoded int64s)
        long[] encodedValues = new long[valueCount];
        ForCompressor.Decode(deltas, reference, encodedValues);

        // Step 4: Read exceptions
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
            exceptionValues[i] = BinaryPrimitives.ReadDoubleLittleEndian(compressed[offset..]);
            offset += sizeof(double);
        }

        // Step 5: ALP decode (int64s → doubles, with exception patching)
        var alpData = new AlpEncodedData
        {
            EncodedValues = encodedValues,
            Exponent = exponent,
            Factor = factor,
            ExceptionValues = exceptionValues,
            ExceptionPositions = exceptionPositions,
        };

        return AlpDecoder.Decode(alpData);
    }

    private static byte[] WriteHeader(int exponent, int factor, int bitWidth, long reference,
        int valueCount, int exceptionCount)
    {
        byte[] output = new byte[HeaderSize];
        var span = output.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
        span[4] = Version;
        span[5] = (byte)exponent;
        span[6] = (byte)factor;
        span[7] = (byte)bitWidth;
        BinaryPrimitives.WriteInt64LittleEndian(span[8..], reference);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], (uint)valueCount);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], (ushort)exceptionCount);
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], 0);
        return output;
    }
}
