namespace Alp.Tests;

public class BitPackerTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(8, 8, 8)]
    [InlineData(8, 16, 16)]
    [InlineData(8, 1, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(10, 3, 4)]   // 10*3=30 bits -> 4 bytes
    [InlineData(5, 7, 5)]    // 5*7=35 bits -> 5 bytes
    [InlineData(1024, 17, 2176)] // 1024*17=17408 bits -> 2176 bytes
    public void GetPackedSize_ReturnsCorrectSize(int count, int bitWidth, int expected)
    {
        Assert.Equal(expected, BitPacker.GetPackedSize(count, bitWidth));
    }

    [Fact]
    public void PackUnpack_BitWidth0_AllZeros()
    {
        ulong[] values = [42, 99, 100];
        int packedSize = BitPacker.GetPackedSize(values.Length, 0);
        Assert.Equal(0, packedSize);

        ulong[] unpacked = new ulong[values.Length];
        BitPacker.Unpack([], 0, values.Length, unpacked);
        Assert.All(unpacked, v => Assert.Equal(0UL, v));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(48)]
    [InlineData(63)]
    [InlineData(64)]
    public void PackUnpack_RoundTrip_VariousBitWidths(int bitWidth)
    {
        var rng = new Random(bitWidth);
        ulong mask = bitWidth == 64 ? ulong.MaxValue : (1UL << bitWidth) - 1;

        ulong[] values = new ulong[100];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = ((ulong)rng.NextInt64()) & mask;
        }

        int packedSize = BitPacker.GetPackedSize(values.Length, bitWidth);
        byte[] packed = new byte[packedSize];
        BitPacker.Pack(values, bitWidth, packed);

        ulong[] unpacked = new ulong[values.Length];
        BitPacker.Unpack(packed, bitWidth, values.Length, unpacked);

        Assert.Equal(values, unpacked);
    }

    [Fact]
    public void PackUnpack_SingleValue()
    {
        ulong[] values = [0b10110]; // 22, needs 5 bits
        byte[] packed = new byte[BitPacker.GetPackedSize(1, 5)];
        BitPacker.Pack(values, 5, packed);

        ulong[] unpacked = new ulong[1];
        BitPacker.Unpack(packed, 5, 1, unpacked);
        Assert.Equal(22UL, unpacked[0]);
    }

    [Fact]
    public void PackUnpack_MaxValues_BitWidth64()
    {
        ulong[] values = [ulong.MaxValue, 0, ulong.MaxValue, 1];
        byte[] packed = new byte[BitPacker.GetPackedSize(4, 64)];
        BitPacker.Pack(values, 64, packed);

        ulong[] unpacked = new ulong[4];
        BitPacker.Unpack(packed, 64, 4, unpacked);
        Assert.Equal(values, unpacked);
    }

    [Fact]
    public void Pack_MasksTobitWidth()
    {
        // Value exceeds bit width - should be masked
        ulong[] values = [0xFF]; // 255, but we pack with 4 bits
        byte[] packed = new byte[BitPacker.GetPackedSize(1, 4)];
        BitPacker.Pack(values, 4, packed);

        ulong[] unpacked = new ulong[1];
        BitPacker.Unpack(packed, 4, 1, unpacked);
        Assert.Equal(0xFUL, unpacked[0]); // only low 4 bits survive
    }

    [Fact]
    public void PackUnpack_LargeArray()
    {
        var rng = new Random(42);
        int bitWidth = 17;
        ulong mask = (1UL << bitWidth) - 1;

        ulong[] values = new ulong[1024];
        for (int i = 0; i < values.Length; i++)
            values[i] = ((ulong)rng.NextInt64()) & mask;

        byte[] packed = new byte[BitPacker.GetPackedSize(values.Length, bitWidth)];
        BitPacker.Pack(values, bitWidth, packed);

        ulong[] unpacked = new ulong[values.Length];
        BitPacker.Unpack(packed, bitWidth, values.Length, unpacked);
        Assert.Equal(values, unpacked);
    }
}
