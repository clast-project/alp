// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Clast.Alp;

/// <summary>
/// Holds the result of ALP encoding: the encoded integer values,
/// the (exponent, factor) pair used, and any exception values that
/// could not be losslessly encoded.
/// </summary>
internal sealed class AlpEncodedData
{
    public long[] EncodedValues = null!;
    public int Exponent;
    public int Factor;
    public int[] ExceptionPositions = null!;
    public double[] ExceptionValues = null!;

    public int ExceptionCount => ExceptionPositions.Length;
}
