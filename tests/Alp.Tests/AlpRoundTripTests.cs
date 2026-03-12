namespace Alp.Tests;

public class AlpRoundTripTests
{
    [Fact]
    public void RoundTrip_SimpleDecimals()
    {
        double[] original = [1.23, 4.56, 7.89, 10.11, 12.13];
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_Integers()
    {
        double[] original = [1, 2, 3, 100, 200, -50, 0];
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_Prices()
    {
        // Common financial data: prices with 2 decimal places
        double[] original = [99.99, 100.01, 42.50, 0.01, 1234.56];
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
        Assert.Equal(0, encoded.ExceptionCount);
    }

    [Fact]
    public void RoundTrip_SmallDecimals()
    {
        double[] original = [0.001, 0.002, 0.003, 0.010, 0.100];
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_NegativeValues()
    {
        double[] original = [-1.5, -2.75, -100.0, -0.01];
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_MixedWithSpecialValues()
    {
        double[] original = [1.5, double.NaN, 3.0, double.PositiveInfinity, -2.5, double.NegativeInfinity];
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

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
        // Simulate a realistic dataset: sensor readings with 3 decimal places
        var random = new Random(42);
        double[] original = new double[1024];
        for (int i = 0; i < original.Length; i++)
        {
            original[i] = Math.Round(random.NextDouble() * 1000, 3);
        }

        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        // All values must round-trip (exceptions are patched back)
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_AllZeros()
    {
        double[] original = new double[100]; // all 0.0
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_SingleValue()
    {
        double[] original = [3.14];
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_HighPrecisionDecimals()
    {
        double[] original = [1.123456, 2.654321, 3.111111, 4.999999];
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_TemperatureData()
    {
        // Realistic temperature readings
        double[] original = [20.5, 21.3, 19.8, 22.1, 20.0, 18.7, 23.4, 21.9];
        var encoded = AlpEncoder.Encode(original.AsSpan());
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
        Assert.Equal(0, encoded.ExceptionCount);
    }
}
