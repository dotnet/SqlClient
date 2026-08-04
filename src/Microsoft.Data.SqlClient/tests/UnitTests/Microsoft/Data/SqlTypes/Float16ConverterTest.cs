// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlTypes.UnitTests;

/// <summary>
/// Tests for the IEEE 754 binary16 codec used to exchange <c>vector(N, float16)</c> values.
/// </summary>
/// <remarks>
/// The codec's framework independent implementation is exercised directly rather than
/// through <see cref="Float16Converter.ToSingle"/> and
/// <see cref="Float16Converter.FromSingle"/>, because those use <c>System.Half</c> where it
/// is available. Testing the manual implementation on every target framework means the
/// .NET Framework code path is covered by these tests too, and on .NET it can additionally
/// be compared against <c>System.Half</c> as a reference.
/// </remarks>
public class Float16ConverterTest
{
    #region Reference comparison

    #if NET

    [Fact]
    public void ToSingle_MatchesHalf_ForEveryBitPattern()
    {
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            ushort bits = (ushort)i;
            float expected = (float)BitConverter.UInt16BitsToHalf(bits);
            float actual = Float16Converter.ManualToSingle(bits);

            if (float.IsNaN(expected))
            {
                Assert.True(float.IsNaN(actual), $"0x{bits:X4} should convert to NaN.");
                continue;
            }

            // Compared bitwise so that positive and negative zero are distinguished.
            Assert.True(
                BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual),
                $"0x{bits:X4} converted to {actual} but Half converts it to {expected}.");
        }
    }

    [Fact]
    public void FromSingle_MatchesHalf_ForEveryRepresentableValue()
    {
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            ushort bits = (ushort)i;
            Half value = BitConverter.UInt16BitsToHalf(bits);

            if (Half.IsNaN(value))
            {
                continue;
            }

            Assert.True(
                bits == Float16Converter.ManualFromSingle((float)value),
                $"0x{bits:X4} did not survive a round trip through single precision.");
        }
    }

    [Fact]
    public void FromSingle_MatchesHalf_AcrossTheSinglePrecisionRange()
    {
        // Steps through the single precision space by raw bit pattern. The stride is prime
        // so that the samples do not align with exponent or mantissa boundaries.
        const long Stride = 1039;

        for (long b = 0; b <= uint.MaxValue; b += Stride)
        {
            float value = BitConverter.Int32BitsToSingle((int)(uint)b);

            if (float.IsNaN(value))
            {
                continue;
            }

            Assert.True(
                BitConverter.HalfToUInt16Bits((Half)value) == Float16Converter.ManualFromSingle(value),
                $"{value:R} (0x{(uint)b:X8}) was not narrowed the same way as Half.");
        }
    }

    #endif

    #endregion

    #region Round trips

    [Theory]
    // Exactly representable values.
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-1.5f)]
    [InlineData(2.5f)]
    [InlineData(65504f)]      // Largest finite binary16 value.
    [InlineData(-65504f)]
    [InlineData(6.103515625e-5f)]  // Smallest normal binary16 value.
    [InlineData(5.9604645e-8f)]    // Smallest subnormal binary16 value.
    public void RoundTrip_PreservesExactlyRepresentableValues(float value)
    {
        Assert.Equal(value, Float16Converter.ManualToSingle(Float16Converter.ManualFromSingle(value)));
    }

    [Fact]
    public void RoundTrip_PreservesSignOfZero()
    {
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(Float16Converter.ManualToSingle(Float16Converter.ManualFromSingle(-0.0f))));

        Assert.Equal(0x8000, Float16Converter.ManualFromSingle(-0.0f));
        Assert.Equal(0x0000, Float16Converter.ManualFromSingle(0.0f));
    }

    #endregion

    #region Rounding

    [Theory]
    // Values which are not representable are rounded to the nearest binary16 value.
    [InlineData(1.1f, 1.0996094f)]
    [InlineData(0.3f, 0.30004883f)]
    [InlineData(0.1f, 0.099975586f)]
    // Rounding up at the top of the range still produces the largest finite value rather
    // than an infinity, because the value is nearer to it than to the overflow threshold.
    [InlineData(65505f, 65504f)]
    public void FromSingle_RoundsToNearest(float value, float expected)
    {
        Assert.Equal(expected, Float16Converter.ManualToSingle(Float16Converter.ManualFromSingle(value)));
    }

    [Fact]
    public void FromSingle_RoundsTiesToEven()
    {
        // Halfway between the binary16 values 1.0 (0x3C00) and 1.0009765625 (0x3C01). The
        // tie is resolved towards the value with an even mantissa, which is 1.0.
        Assert.Equal(0x3C00, Float16Converter.ManualFromSingle(1.00048828125f));

        // Halfway between 1.0009765625 (0x3C01) and 1.001953125 (0x3C02), which resolves
        // upwards for the same reason.
        Assert.Equal(0x3C02, Float16Converter.ManualFromSingle(1.00146484375f));
    }

    #endregion

    #region Overflow and underflow

    [Theory]
    [InlineData(70000f)]
    [InlineData(float.MaxValue)]
    public void FromSingle_SaturatesToInfinityOnOverflow(float value)
    {
        Assert.Equal(0x7C00, Float16Converter.ManualFromSingle(value));
    }

    [Theory]
    [InlineData(-70000f)]
    [InlineData(float.MinValue)]
    public void FromSingle_SaturatesToNegativeInfinityOnOverflow(float value)
    {
        Assert.Equal(0xFC00, Float16Converter.ManualFromSingle(value));
    }

    [Theory]
    // Below half of the smallest subnormal, so these round to zero rather than to it.
    [InlineData(1e-8f)]
    [InlineData(1e-30f)]
    [InlineData(float.Epsilon)]
    public void FromSingle_FlushesToZeroOnUnderflow(float value)
    {
        Assert.Equal(0x0000, Float16Converter.ManualFromSingle(value));
    }

    [Fact]
    public void FromSingle_PreservesSubnormals()
    {
        // The smallest subnormal, and the value just above half of it, which rounds up to
        // the smallest subnormal rather than to zero.
        Assert.Equal(0x0001, Float16Converter.ManualFromSingle(5.9604645e-8f));
        Assert.Equal(0x0001, Float16Converter.ManualFromSingle(4.0e-8f));
    }

    #endregion

    #region Infinity and NaN

    [Fact]
    public void FromSingle_PreservesInfinity()
    {
        Assert.Equal(0x7C00, Float16Converter.ManualFromSingle(float.PositiveInfinity));
        Assert.Equal(0xFC00, Float16Converter.ManualFromSingle(float.NegativeInfinity));
    }

    [Fact]
    public void ToSingle_PreservesInfinity()
    {
        Assert.Equal(float.PositiveInfinity, Float16Converter.ManualToSingle(0x7C00));
        Assert.Equal(float.NegativeInfinity, Float16Converter.ManualToSingle(0xFC00));
    }

    [Fact]
    public void ConvertsNaN()
    {
        Assert.True(float.IsNaN(Float16Converter.ManualToSingle(Float16Converter.ManualFromSingle(float.NaN))));

        // Any binary16 value with a maximal exponent and a non-zero mantissa is a NaN.
        Assert.True(float.IsNaN(Float16Converter.ManualToSingle(0x7E00)));
        Assert.True(float.IsNaN(Float16Converter.ManualToSingle(0x7C01)));
    }

    #endregion
}
