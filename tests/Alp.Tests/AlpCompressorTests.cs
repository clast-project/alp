namespace Alp.Tests;

public class AlpCompressorTests
{
    [Fact]
    public void RoundTrip_SimpleDecimals()
    {
        double[] original = [1.23, 4.56, 7.89, 10.11, 12.13];
        AssertRoundTrip(original);
    }

    [Fact]
    public void RoundTrip_Integers()
    {
        double[] original = [1, 2, 3, 100, 200, -50, 0];
        AssertRoundTrip(original);
    }

    [Fact]
    public void RoundTrip_Prices()
    {
        double[] original = [99.99, 100.01, 42.50, 0.01, 1234.56];
        AssertRoundTrip(original);
    }

    [Fact]
    public void RoundTrip_SmallDecimals()
    {
        double[] original = [0.001, 0.002, 0.003, 0.010, 0.100];
        AssertRoundTrip(original);
    }

    [Fact]
    public void RoundTrip_NegativeValues()
    {
        double[] original = [-1.5, -2.75, -100.0, -0.01];
        AssertRoundTrip(original);
    }

    [Fact]
    public void RoundTrip_MixedWithSpecialValues()
    {
        double[] original = [1.5, double.NaN, 3.0, double.PositiveInfinity, -2.5, double.NegativeInfinity];
        byte[] compressed = AlpCompressor.Compress(original);
        double[] decoded = AlpCompressor.Decompress(compressed);

        Assert.Equal(1.5, decoded[0]);
        Assert.True(double.IsNaN(decoded[1]));
        Assert.Equal(3.0, decoded[2]);
        Assert.Equal(double.PositiveInfinity, decoded[3]);
        Assert.Equal(-2.5, decoded[4]);
        Assert.Equal(double.NegativeInfinity, decoded[5]);
    }

    [Fact]
    public void RoundTrip_LargeDataset()
    {
        var rng = new Random(42);
        double[] original = new double[1024];
        for (int i = 0; i < original.Length; i++)
            original[i] = Math.Round(rng.NextDouble() * 1000, 3);

        AssertRoundTrip(original);
    }

    [Fact]
    public void RoundTrip_AllZeros()
    {
        double[] original = new double[100];
        AssertRoundTrip(original);
    }

    [Fact]
    public void RoundTrip_SingleValue()
    {
        double[] original = [3.14];
        AssertRoundTrip(original);
    }

    [Fact]
    public void RoundTrip_Empty()
    {
        byte[] compressed = AlpCompressor.Compress([]);
        double[] decoded = AlpCompressor.Decompress(compressed);
        Assert.Empty(decoded);
    }

    [Fact]
    public void RoundTrip_AllNaN()
    {
        double[] original = [double.NaN, double.NaN, double.NaN];
        byte[] compressed = AlpCompressor.Compress(original);
        double[] decoded = AlpCompressor.Decompress(compressed);

        Assert.Equal(original.Length, decoded.Length);
        Assert.All(decoded, v => Assert.True(double.IsNaN(v)));
    }

    [Fact]
    public void RoundTrip_TemperatureData()
    {
        double[] original = [20.5, 21.3, 19.8, 22.1, 20.0, 18.7, 23.4, 21.9];
        AssertRoundTrip(original);
    }

    [Fact]
    public void Compress_AchievesCompression_Prices()
    {
        // Use integer-cent prices to avoid floating-point representation exceptions
        var rng = new Random(42);
        double[] prices = new double[1024];
        for (int i = 0; i < prices.Length; i++)
            prices[i] = rng.Next(5000, 10000) * 0.01;

        byte[] compressed = AlpCompressor.Compress(prices);
        int uncompressedSize = prices.Length * sizeof(double); // 8192 bytes
        double ratio = (double)compressed.Length / uncompressedSize;

        // Range ~5000 cents needs ~13 bits. 1024*13/8 = 1664 + 24 header ≈ 1688 ≈ 21%
        Assert.True(ratio < 0.40,
            $"Compression ratio {ratio:P1} ({compressed.Length} / {uncompressedSize}) is worse than expected");
    }

    [Fact]
    public void Compress_AchievesCompression_Integers()
    {
        double[] values = new double[1024];
        for (int i = 0; i < values.Length; i++)
            values[i] = i; // 0..1023

        byte[] compressed = AlpCompressor.Compress(values);
        int uncompressedSize = values.Length * sizeof(double);
        double ratio = (double)compressed.Length / uncompressedSize;

        // Range 0..1023 needs 10 bits. 1024*10/8 = 1280 + 24 = 1304 bytes ≈ 16%
        Assert.True(ratio < 0.25,
            $"Compression ratio {ratio:P1} ({compressed.Length} / {uncompressedSize}) is worse than expected");
    }

    [Fact]
    public void Compress_AllIdentical_MinimalSize()
    {
        double[] values = new double[1024];
        Array.Fill(values, 42.0);

        byte[] compressed = AlpCompressor.Compress(values);

        // All identical → bitWidth=0 → no packed data, just 24-byte header
        Assert.Equal(24, compressed.Length);
    }

    [Fact]
    public void Decompress_InvalidMagic_Throws()
    {
        byte[] bad = new byte[24];
        Assert.Throws<ArgumentException>(() => AlpCompressor.Decompress(bad));
    }

    [Fact]
    public void Decompress_TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => AlpCompressor.Decompress(new byte[10]));
    }

    [Fact]
    public void Header_ContainsCorrectMagic()
    {
        double[] values = [1.0, 2.0, 3.0];
        byte[] compressed = AlpCompressor.Compress(values);

        // "ALP1" = 0x414C5031 little-endian
        Assert.Equal(0x31, compressed[0]); // '1'
        Assert.Equal(0x50, compressed[1]); // 'P'
        Assert.Equal(0x4C, compressed[2]); // 'L'
        Assert.Equal(0x41, compressed[3]); // 'A'
        Assert.Equal(1, compressed[4]);    // version
    }

    private static void AssertRoundTrip(double[] original)
    {
        byte[] compressed = AlpCompressor.Compress(original);
        double[] decoded = AlpCompressor.Decompress(compressed);
        Assert.Equal(original, decoded);
    }
}
