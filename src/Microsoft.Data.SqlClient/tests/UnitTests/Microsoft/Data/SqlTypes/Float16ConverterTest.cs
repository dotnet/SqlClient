// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;
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

    /// <summary>
    /// Verifies the widening direction against <c>System.Half</c> exhaustively, over all
    /// 65,536 binary16 bit patterns. Comparing bitwise rather than by value distinguishes
    /// positive from negative zero and compares a NaN's sign and payload rather than only
    /// its NaN-ness.
    /// </summary>
    [Fact]
    public void ToSingle_MatchesHalf_ForEveryBitPattern()
    {
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            ushort bits = (ushort)i;
            float expected = (float)BitConverter.UInt16BitsToHalf(bits);
            float actual = Float16Converter.ManualToSingle(bits);

            // Compared bitwise so that positive and negative zero are distinguished, and so
            // that a NaN's sign and payload are compared rather than only its NaN-ness.
            Assert.True(
                BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual),
                $"0x{bits:X4} converted to 0x{BitConverter.SingleToInt32Bits(actual):X8} " +
                $"but Half converts it to 0x{BitConverter.SingleToInt32Bits(expected):X8}.");
        }
    }

    /// <summary>
    /// Verifies the narrowing direction against <c>System.Half</c> for every value which
    /// binary16 can represent, so that a value read from a float16 column is written back
    /// unchanged.
    /// </summary>
    [Fact]
    public void FromSingle_MatchesHalf_ForEveryRepresentableValue()
    {
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            ushort bits = (ushort)i;
            Half value = BitConverter.UInt16BitsToHalf(bits);

            // Compared against a round trip through Half rather than against the original
            // bits, because widening quietens a signalling NaN and so cannot be reversed.
            Assert.True(
                BitConverter.HalfToUInt16Bits((Half)(float)value) == Float16Converter.ManualFromSingle((float)value),
                $"0x{bits:X4} did not survive a round trip through single precision.");
        }
    }

    /// <summary>
    /// Verifies narrowing against <c>System.Half</c> for inputs drawn from the whole single
    /// precision space, not just those binary16 can represent. This is what covers rounding,
    /// overflow, and underflow for arbitrary application values, which the exhaustive
    /// representable-value test above cannot reach.
    /// </summary>
    [Fact]
    public void FromSingle_MatchesHalf_AcrossTheSinglePrecisionRange()
    {
        // Steps through the single precision space by raw bit pattern. The stride is prime
        // so that the samples do not align with exponent or mantissa boundaries.
        const long Stride = 1039;

        for (long b = 0; b <= uint.MaxValue; b += Stride)
        {
            float value = BitConverter.Int32BitsToSingle((int)(uint)b);

            Assert.True(
                BitConverter.HalfToUInt16Bits((Half)value) == Float16Converter.ManualFromSingle(value),
                $"0x{(uint)b:X8} was not narrowed the same way as Half.");
        }
    }

    /// <summary>
    /// Verifies that a NaN keeps its sign and payload in both directions rather than being
    /// canonicalised, so the codec and <c>System.Half</c> cannot diverge on inputs an
    /// application could legitimately send.
    /// </summary>
    [Fact]
    public void ConvertsNaN_PreservingSignAndPayload()
    {
        // A NaN's sign and payload are carried through rather than canonicalised, so that
        // the manual implementation and System.Half cannot diverge.
        foreach (ushort bits in new ushort[] { 0x7E00, 0xFE00, 0x7C01, 0x7DFF, 0xFFFF })
        {
            Assert.Equal(
                BitConverter.SingleToInt32Bits((float)BitConverter.UInt16BitsToHalf(bits)),
                BitConverter.SingleToInt32Bits(Float16Converter.ManualToSingle(bits)));
        }

        foreach (uint bits in new uint[] { 0x7FC00000, 0xFFC00000, 0x7FFFFFFF, 0x7F800001 })
        {
            float value = BitConverter.Int32BitsToSingle((int)bits);

            Assert.Equal(
                BitConverter.HalfToUInt16Bits((Half)value),
                Float16Converter.ManualFromSingle(value));
        }
    }

    #endif

    #endregion

    #region Round trips

    /// <summary>
    /// Verifies that a value binary16 can represent exactly survives a narrow then widen
    /// round trip unchanged, covering the range's boundaries: the largest finite value, the
    /// smallest normal, and the smallest subnormal.
    /// </summary>
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

    /// <summary>
    /// Verifies that negative zero stays negative through a round trip, which a naive
    /// implementation loses by treating the value as equal to positive zero.
    /// </summary>
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

    /// <summary>
    /// Verifies that a value binary16 cannot represent is rounded to the nearest one it can,
    /// including at the top of the range where rounding up must still produce the largest
    /// finite value rather than an infinity.
    /// </summary>
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

    /// <summary>
    /// Verifies that a value exactly halfway between two binary16 values resolves towards
    /// the one with an even mantissa, which is the IEEE 754 default and the rule
    /// <c>System.Half</c> and SQL Server both follow.
    /// </summary>
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

    /// <summary>
    /// Verifies that a value too large for binary16 saturates to positive infinity. Callers
    /// which must reject such a value rather than store an infinity detect it from this
    /// result, which is what the bulk copy write path relies on.
    /// </summary>
    [Theory]
    [InlineData(70000f)]
    [InlineData(float.MaxValue)]
    public void FromSingle_SaturatesToInfinityOnOverflow(float value)
    {
        Assert.Equal(0x7C00, Float16Converter.ManualFromSingle(value));
    }

    /// <summary>
    /// Verifies that a value too negative for binary16 saturates to negative infinity,
    /// the counterpart of the overflow case above.
    /// </summary>
    [Theory]
    [InlineData(-70000f)]
    [InlineData(float.MinValue)]
    public void FromSingle_SaturatesToNegativeInfinityOnOverflow(float value)
    {
        Assert.Equal(0xFC00, Float16Converter.ManualFromSingle(value));
    }

    /// <summary>
    /// Verifies that a value below half the smallest subnormal flushes to zero rather than
    /// rounding up to the smallest subnormal.
    /// </summary>
    [Theory]
    // Below half of the smallest subnormal, so these round to zero rather than to it.
    [InlineData(1e-8f)]
    [InlineData(1e-30f)]
    [InlineData(float.Epsilon)]
    public void FromSingle_FlushesToZeroOnUnderflow(float value)
    {
        Assert.Equal(0x0000, Float16Converter.ManualFromSingle(value));
    }

    /// <summary>
    /// Verifies that subnormal binary16 values are produced rather than flushed to zero,
    /// including the value just above half the smallest subnormal, which must round up to
    /// it. Subnormals use a different encoding path from normals, so they are covered
    /// separately.
    /// </summary>
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

    /// <summary>
    /// Verifies that an infinity narrows to the binary16 infinity of the same sign, rather
    /// than being confused with the overflow saturation which produces the same encoding.
    /// </summary>
    [Fact]
    public void FromSingle_PreservesInfinity()
    {
        Assert.Equal(0x7C00, Float16Converter.ManualFromSingle(float.PositiveInfinity));
        Assert.Equal(0xFC00, Float16Converter.ManualFromSingle(float.NegativeInfinity));
    }

    /// <summary>
    /// Verifies that a binary16 infinity widens to the single precision infinity of the
    /// same sign, the counterpart of the narrowing case above.
    /// </summary>
    [Fact]
    public void ToSingle_PreservesInfinity()
    {
        Assert.Equal(float.PositiveInfinity, Float16Converter.ManualToSingle(0x7C00));
        Assert.Equal(float.NegativeInfinity, Float16Converter.ManualToSingle(0xFC00));
    }

    /// <summary>
    /// Verifies that a NaN stays a NaN through a round trip, and that any binary16 encoding
    /// with a maximal exponent and a non-zero mantissa widens to one. This runs on every
    /// target framework, unlike the payload-preserving test above which needs
    /// <c>System.Half</c> as a reference.
    /// </summary>
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
