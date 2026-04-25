// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

namespace Clast.Alp;

/// <summary>
/// Numeric constants and lookup tables used by the ALP encoder and decoder.
/// </summary>
internal static class AlpConstants
{
    /// <summary>
    /// Maximum exponent index for double precision (10^0 through 10^23).
    /// </summary>
    public const int MaxExponent = 23;

    /// <summary>
    /// Maximum factor index for double precision.
    /// </summary>
    public const int MaxFactor = 23;

    /// <summary>
    /// Magic number for fast rounding (banker's rounding via IEEE 754).
    /// This is 2^51 + 2^52 = 6755399441055744.0.
    /// </summary>
    public const double MagicNumber = 6755399441055744.0; // 0x0018000000000000

    /// <summary>
    /// Upper encoding limit: values above this overflow the int64 after rounding.
    /// </summary>
    public const long EncodingUpperLimit = long.MaxValue - 1024;

    /// <summary>
    /// Lower encoding limit: values below this overflow the int64 after rounding.
    /// </summary>
    public const long EncodingLowerLimit = long.MinValue + 1024;

    /// <summary>
    /// Powers of 10 as doubles, used during encoding to scale values up.
    /// EXP_ARR[e] = 10^e.
    /// </summary>
    public static readonly double[] ExpArray =
    [
        1e0,  1e1,  1e2,  1e3,  1e4,  1e5,  1e6,  1e7,
        1e8,  1e9,  1e10, 1e11, 1e12, 1e13, 1e14, 1e15,
        1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22, 1e23,
    ];

    /// <summary>
    /// Negative powers of 10 as doubles, used during encoding (to divide) and decoding (to scale down).
    /// FRAC_ARR[f] = 10^(-f).
    /// </summary>
    public static readonly double[] FracArray =
    [
        1e0,   1e-1,  1e-2,  1e-3,  1e-4,  1e-5,  1e-6,  1e-7,
        1e-8,  1e-9,  1e-10, 1e-11, 1e-12, 1e-13, 1e-14, 1e-15,
        1e-16, 1e-17, 1e-18, 1e-19, 1e-20, 1e-21, 1e-22, 1e-23,
    ];

    /// <summary>
    /// Powers of 10 as long integers, used during decoding.
    /// FACT_ARR[f] = 10^f (as integer).
    /// Only valid up to index 18 (10^18 fits in int64).
    /// </summary>
    public static readonly long[] FactArray =
    [
        1L,
        10L,
        100L,
        1_000L,
        10_000L,
        100_000L,
        1_000_000L,
        10_000_000L,
        100_000_000L,
        1_000_000_000L,
        10_000_000_000L,
        100_000_000_000L,
        1_000_000_000_000L,
        10_000_000_000_000L,
        100_000_000_000_000L,
        1_000_000_000_000_000L,
        10_000_000_000_000_000L,
        100_000_000_000_000_000L,
        1_000_000_000_000_000_000L,
    ];

    /// <summary>
    /// Maximum usable factor index (10^18 is the largest power of 10 that fits in a long).
    /// </summary>
    public const int MaxFactorIndex = 18;
}
