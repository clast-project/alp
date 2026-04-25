// Copyright (c) clast-project. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Clast.Alp
{
    internal static class Polyfill
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double value) =>
#if NETSTANDARD2_0
            !double.IsNaN(value) && !double.IsInfinity(value);
#else
            double.IsFinite(value);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDoubleLittleEndian(Span<byte> destination, double value) =>
#if NETSTANDARD2_0
            BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(value));
#else
            BinaryPrimitives.WriteDoubleLittleEndian(destination, value);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDoubleLittleEndian(ReadOnlySpan<byte> source) =>
#if NETSTANDARD2_0
            BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(source));
#else
            BinaryPrimitives.ReadDoubleLittleEndian(source);
#endif
    }
}

#if NETSTANDARD2_0
namespace System.Numerics
{
    internal static class BitOperations
    {
        public static int LeadingZeroCount(ulong value)
        {
            if (value == 0) return 64;
            int n = 0;
            if ((value & 0xFFFFFFFF00000000UL) == 0) { n += 32; value <<= 32; }
            if ((value & 0xFFFF000000000000UL) == 0) { n += 16; value <<= 16; }
            if ((value & 0xFF00000000000000UL) == 0) { n += 8; value <<= 8; }
            if ((value & 0xF000000000000000UL) == 0) { n += 4; value <<= 4; }
            if ((value & 0xC000000000000000UL) == 0) { n += 2; value <<= 2; }
            if ((value & 0x8000000000000000UL) == 0) { n += 1; }
            return n;
        }
    }
}
#endif
