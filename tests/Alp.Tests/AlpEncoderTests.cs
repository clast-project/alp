namespace Alp.Tests;

public class AlpEncoderTests
{
    [Theory]
    [InlineData(1.23, 2, 0, 123)]
    [InlineData(0.5, 1, 0, 5)]
    [InlineData(42.0, 0, 0, 42)]
    [InlineData(-3.14, 2, 0, -314)]
    [InlineData(0.001, 3, 0, 1)]
    [InlineData(100.0, 0, 0, 100)]
    [InlineData(1234.5678, 4, 0, 12345678)]
    public void EncodeValue_ProducesExpectedInteger(double value, int exponent, int factor, long expected)
    {
        long encoded = AlpEncoder.EncodeValue(value, exponent, factor);
        Assert.Equal(expected, encoded);
    }

    [Theory]
    [InlineData(1.23, 2, 0)]
    [InlineData(0.5, 1, 0)]
    [InlineData(42.0, 0, 0)]
    [InlineData(-3.14, 2, 0)]
    [InlineData(0.001, 3, 0)]
    [InlineData(100.0, 0, 0)]
    [InlineData(1234.5678, 4, 0)]
    [InlineData(0.0, 0, 0)]
    [InlineData(-0.5, 1, 0)]
    public void RoundTrips_ReturnsTrueForDecimalValues(double value, int exponent, int factor)
    {
        Assert.True(AlpEncoder.RoundTrips(value, exponent, factor));
    }

    [Fact]
    public void RoundTrips_ReturnsFalseForNaN()
    {
        Assert.False(AlpEncoder.RoundTrips(double.NaN, 0, 0));
    }

    [Fact]
    public void RoundTrips_ReturnsFalseForInfinity()
    {
        Assert.False(AlpEncoder.RoundTrips(double.PositiveInfinity, 0, 0));
        Assert.False(AlpEncoder.RoundTrips(double.NegativeInfinity, 0, 0));
    }

    [Fact]
    public void CanEncode_ReturnsFalseForSpecialValues()
    {
        Assert.False(AlpEncoder.CanEncode(double.NaN, 0, 0));
        Assert.False(AlpEncoder.CanEncode(double.PositiveInfinity, 0, 0));
        Assert.False(AlpEncoder.CanEncode(double.NegativeInfinity, 0, 0));
    }

    [Fact]
    public void FindBestFactorExponent_SimpleDecimals()
    {
        double[] values = [1.23, 4.56, 7.89, 0.12, 3.45];
        var (exponent, factor) = AlpEncoder.FindBestFactorExponent(values);

        // All values should round-trip with the chosen combination
        foreach (double v in values)
        {
            Assert.True(AlpEncoder.RoundTrips(v, exponent, factor),
                $"Value {v} does not round-trip with e={exponent}, f={factor}");
        }
    }

    [Fact]
    public void FindBestFactorExponent_Integers()
    {
        double[] values = [1.0, 2.0, 3.0, 100.0, -50.0];
        var (exponent, factor) = AlpEncoder.FindBestFactorExponent(values);

        // Integers need no scaling: e=0, f=0
        Assert.Equal(0, exponent);
        Assert.Equal(0, factor);
    }

    [Fact]
    public void FindBestFactorExponent_MixedPrecision()
    {
        // Mix of 1 and 2 decimal places - should pick e=2 to handle both
        double[] values = [1.1, 2.22, 3.3, 4.44];
        var (exponent, factor) = AlpEncoder.FindBestFactorExponent(values);

        // Verify all values round-trip with the chosen combination
        foreach (double v in values)
        {
            Assert.True(AlpEncoder.RoundTrips(v, exponent, factor),
                $"Value {v} does not round-trip with e={exponent}, f={factor}");
        }
    }

    [Fact]
    public void FindBestFactorExponent_HighPrecision()
    {
        double[] values = [1.123456, 2.654321, 3.111111];
        var (exponent, factor) = AlpEncoder.FindBestFactorExponent(values);

        foreach (double v in values)
        {
            Assert.True(AlpEncoder.RoundTrips(v, exponent, factor),
                $"Value {v} does not round-trip with e={exponent}, f={factor}");
        }
    }

    [Fact]
    public void Encode_AllValuesRoundTrip()
    {
        double[] values = [1.5, 2.5, 3.5, 4.5, 5.5];
        var encoded = AlpEncoder.Encode(values, 1, 0);

        Assert.Equal(0, encoded.ExceptionCount);
        Assert.Equal([15, 25, 35, 45, 55], encoded.EncodedValues);
    }

    [Fact]
    public void Encode_WithExceptions()
    {
        // NaN cannot be encoded and becomes an exception
        double[] values = [1.5, double.NaN, 3.5];
        var encoded = AlpEncoder.Encode(values, 1, 0);

        Assert.Equal(1, encoded.ExceptionCount);
        Assert.Equal(1, encoded.ExceptionPositions[0]);
        Assert.True(double.IsNaN(encoded.ExceptionValues[0]));
    }

    [Fact]
    public void Encode_AutoSelectsParameters()
    {
        double[] values = [10.12, 20.34, 30.56, 40.78, 50.90];
        var encoded = AlpEncoder.Encode(values.AsSpan());

        Assert.Equal(0, encoded.ExceptionCount);
        // Verify round-trip rather than specific parameter values
        double[] decoded = AlpDecoder.Decode(encoded);
        Assert.Equal(values, decoded);
    }

    [Fact]
    public void EncodeValue_WithNonZeroFactor()
    {
        // e=3, f=1 means net scaling is 10^(3-1) = 100
        // value 1.23 * 1000 * 0.1 = 123.0
        double value = 1.23;
        long encoded = AlpEncoder.EncodeValue(value, 3, 1);
        Assert.Equal(123, encoded);
    }

    [Fact]
    public void EncodeValue_LargeValues()
    {
        double value = 123456789.0;
        long encoded = AlpEncoder.EncodeValue(value, 0, 0);
        Assert.Equal(123456789L, encoded);
    }

    [Fact]
    public void EncodeValue_NegativeValues()
    {
        long encoded = AlpEncoder.EncodeValue(-1.5, 1, 0);
        Assert.Equal(-15, encoded);
    }

    [Fact]
    public void EncodeValue_Zero()
    {
        long encoded = AlpEncoder.EncodeValue(0.0, 5, 0);
        Assert.Equal(0, encoded);
    }
}
