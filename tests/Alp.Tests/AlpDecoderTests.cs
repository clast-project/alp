// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Clast.Alp.Tests;

public class AlpDecoderTests
{
    [Theory]
    [InlineData(123, 2, 0, 1.23)]
    [InlineData(5, 1, 0, 0.5)]
    [InlineData(42, 0, 0, 42.0)]
    [InlineData(-314, 2, 0, -3.14)]
    [InlineData(1, 3, 0, 0.001)]
    [InlineData(100, 0, 0, 100.0)]
    [InlineData(12345678, 4, 0, 1234.5678)]
    public void DecodeValue_ProducesExpectedDouble(long encoded, int exponent, int factor, double expected)
    {
        double decoded = AlpDecoder.DecodeValue(encoded, exponent, factor);
        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void Decode_ReconstructsAllValues()
    {
        double[] original = [1.5, 2.5, 3.5, 4.5, 5.5];
        var encoded = AlpEncoder.Encode(original, 1, 0);
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Decode_PatchesExceptions()
    {
        double[] original = [1.5, double.NaN, 3.5, double.PositiveInfinity, 5.5];
        var encoded = AlpEncoder.Encode(original, 1, 0);
        double[] decoded = AlpDecoder.Decode(encoded);

        Assert.Equal(1.5, decoded[0]);
        Assert.True(double.IsNaN(decoded[1]));
        Assert.Equal(3.5, decoded[2]);
        Assert.Equal(double.PositiveInfinity, decoded[3]);
        Assert.Equal(5.5, decoded[4]);
    }

    [Fact]
    public void DecodeValue_Zero()
    {
        Assert.Equal(0.0, AlpDecoder.DecodeValue(0, 5, 0));
    }

    [Fact]
    public void DecodeValue_WithNonZeroFactor()
    {
        // e=3, f=1: decoded = 123 * 10^1 * 10^-3 = 1230 * 0.001 = 1.23
        double decoded = AlpDecoder.DecodeValue(123, 3, 1);
        Assert.Equal(1.23, decoded);
    }
}
