// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlTypes.UnitTests;

/// <summary>
/// Verifies the lossless binary16 decoder used by .NET Framework vector reads.
/// </summary>
public class Float16ConverterTest
{
    #if NET
    /// <summary>
    /// Compares every binary16 bit pattern with System.Half, including signed zeros and
    /// NaN payloads, without using a production encoder to generate the input.
    /// </summary>
    [Fact]
    public void ToSingle_MatchesHalf_ForEveryBitPattern()
    {
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            ushort bits = (ushort)i;
            int expected = BitConverter.SingleToInt32Bits((float)BitConverter.UInt16BitsToHalf(bits));
            int actual = BitConverter.SingleToInt32Bits(Float16Converter.ManualToSingle(bits));
            Assert.True(expected == actual, $"0x{bits:X4}: expected 0x{expected:X8}, got 0x{actual:X8}.");
        }
    }
    #endif

    /// <summary>
    /// Decodes known finite values and normal/subnormal boundaries on every framework.
    /// </summary>
    [Theory]
    [InlineData(0x0000, 0f)]
    [InlineData(0x3C00, 1f)]
    [InlineData(0xBE00, -1.5f)]
    [InlineData(0x4100, 2.5f)]
    [InlineData(0x7BFF, 65504f)]
    [InlineData(0xFBFF, -65504f)]
    [InlineData(0x0400, 6.103515625e-5f)]
    [InlineData(0x0001, 5.9604645e-8f)]
    public void ToSingle_DecodesKnownValues(int bits, float expected)
    {
        Assert.Equal(expected, Float16Converter.ManualToSingle((ushort)bits));
        Assert.Equal(expected, Float16Converter.ToSingle((ushort)bits));
    }

    /// <summary>
    /// Retains the sign of zero instead of collapsing both encodings to positive zero.
    /// </summary>
    [Fact]
    public void ToSingle_PreservesSignOfZero()
    {
        Assert.Equal(long.MinValue,
            BitConverter.DoubleToInt64Bits(Float16Converter.ManualToSingle(0x8000)));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(Float16Converter.ManualToSingle(0x0000)));
    }

    /// <summary>
    /// Preserves infinity and recognizes quiet and signalling NaN encodings.
    /// </summary>
    [Fact]
    public void ToSingle_PreservesInfinityAndNaN()
    {
        Assert.Equal(float.PositiveInfinity, Float16Converter.ManualToSingle(0x7C00));
        Assert.Equal(float.NegativeInfinity, Float16Converter.ManualToSingle(0xFC00));
        Assert.True(float.IsNaN(Float16Converter.ManualToSingle(0x7E00)));
        Assert.True(float.IsNaN(Float16Converter.ManualToSingle(0x7C01)));
    }
}
