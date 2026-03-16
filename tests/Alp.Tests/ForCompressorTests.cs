namespace Alp.Tests;

public class ForCompressorTests
{
    [Fact]
    public void Encode_EmptyInput()
    {
        var (reference, bitWidth, deltas) = ForCompressor.Encode([]);
        Assert.Equal(0, reference);
        Assert.Equal(0, bitWidth);
        Assert.Empty(deltas);
    }

    [Fact]
    public void Encode_AllIdentical()
    {
        long[] values = [42, 42, 42, 42];
        var (reference, bitWidth, deltas) = ForCompressor.Encode(values);

        Assert.Equal(42, reference);
        Assert.Equal(0, bitWidth);
        Assert.All(deltas, d => Assert.Equal(0UL, d));
    }

    [Fact]
    public void Encode_Sequential()
    {
        long[] values = [10, 11, 12, 13];
        var (reference, bitWidth, deltas) = ForCompressor.Encode(values);

        Assert.Equal(10, reference);
        Assert.Equal(2, bitWidth); // range 3 needs 2 bits
        Assert.Equal(new ulong[] { 0, 1, 2, 3 }, deltas);
    }

    [Fact]
    public void Encode_NegativeValues()
    {
        long[] values = [-100, -50, -75];
        var (reference, bitWidth, deltas) = ForCompressor.Encode(values);

        Assert.Equal(-100, reference);
        Assert.Equal(new ulong[] { 0, 50, 25 }, deltas);
    }

    [Fact]
    public void Encode_MixedPositiveNegative()
    {
        long[] values = [-10, 0, 10];
        var (reference, bitWidth, deltas) = ForCompressor.Encode(values);

        Assert.Equal(-10, reference);
        Assert.Equal(5, bitWidth); // range 20 needs 5 bits
        Assert.Equal(new ulong[] { 0, 10, 20 }, deltas);
    }

    [Fact]
    public void Encode_SingleValue()
    {
        long[] values = [999];
        var (reference, bitWidth, deltas) = ForCompressor.Encode(values);

        Assert.Equal(999, reference);
        Assert.Equal(0, bitWidth);
        Assert.Equal(new ulong[] { 0 }, deltas);
    }

    [Fact]
    public void RoundTrip()
    {
        long[] values = [100, -50, 200, 0, -200, 150];
        var (reference, bitWidth, deltas) = ForCompressor.Encode(values);

        long[] decoded = new long[values.Length];
        ForCompressor.Decode(deltas, reference, decoded);

        Assert.Equal(values, decoded);
    }

    [Fact]
    public void RoundTrip_LargeRange()
    {
        long[] values = [long.MinValue / 2, long.MaxValue / 2];
        var (reference, bitWidth, deltas) = ForCompressor.Encode(values);

        Assert.Equal(63, bitWidth);

        long[] decoded = new long[values.Length];
        ForCompressor.Decode(deltas, reference, decoded);
        Assert.Equal(values, decoded);
    }
}
