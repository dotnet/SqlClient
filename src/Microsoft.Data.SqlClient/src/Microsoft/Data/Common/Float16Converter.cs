// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Converts between IEEE 754 binary16 (half precision) and binary32 (single
    /// precision) values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SQL Server transports <c>vector(N, float16)</c> elements as raw binary16
    /// values. <c>System.Half</c> is only available on .NET, so the conversion is
    /// implemented manually for .NET Framework.
    /// </para>
    /// <para>
    /// The manual implementations are compiled for every target framework, rather
    /// than only for .NET Framework, so that they can be validated exhaustively
    /// against <c>System.Half</c> in unit tests.
    /// </para>
    /// </remarks>
    internal static class Float16Converter
    {
        // Layout of an IEEE 754 binary16 value:
        //   bit 15    : sign
        //   bits 14-10: exponent, biased by 15
        //   bits  9-0 : mantissa
        private const int Binary16MantissaBits = 10;
        private const int Binary16ExponentBias = 15;
        private const int Binary16MaxExponent = 0x1F;

        // Layout of an IEEE 754 binary32 value:
        //   bit 31    : sign
        //   bits 30-23: exponent, biased by 127
        //   bits 22-0 : mantissa
        private const int Binary32MantissaBits = 23;
        private const int Binary32ExponentBias = 127;
        private const int Binary32MaxExponent = 0xFF;

        // The number of mantissa bits discarded when narrowing binary32 to binary16.
        private const int MantissaShift = Binary32MantissaBits - Binary16MantissaBits;

        /// <summary>
        /// Converts the raw bits of an IEEE 754 binary16 value to the equivalent
        /// single precision value. The conversion is always exact.
        /// </summary>
        internal static float ToSingle(ushort bits)
        {
            #if NET
            return (float)BitConverter.UInt16BitsToHalf(bits);
            #else
            return ManualToSingle(bits);
            #endif
        }

        /// <summary>
        /// Converts a single precision value to the raw bits of the nearest IEEE 754
        /// binary16 value, using the round-to-nearest-even rounding mode. Values whose
        /// magnitude exceeds the binary16 range are converted to an infinity.
        /// </summary>
        internal static ushort FromSingle(float value)
        {
            #if NET
            return BitConverter.HalfToUInt16Bits((Half)value);
            #else
            return ManualFromSingle(value);
            #endif
        }

        /// <summary>
        /// Framework independent implementation of <see cref="ToSingle"/>. Exposed
        /// separately so that it can be validated against <c>System.Half</c>.
        /// </summary>
        internal static float ManualToSingle(ushort bits)
        {
            int sign = (bits >> 15) & 0x1;
            int exponent = (bits >> Binary16MantissaBits) & Binary16MaxExponent;
            int mantissa = bits & 0x3FF;

            if (exponent == Binary16MaxExponent)
            {
                // Infinity or NaN. A zero mantissa denotes an infinity; any other
                // value denotes a NaN, which is canonicalised to float.NaN.
                return mantissa == 0
                    ? Int32BitsToSingle((sign << 31) | (Binary32MaxExponent << Binary32MantissaBits))
                    : float.NaN;
            }

            if (exponent == 0)
            {
                if (mantissa == 0)
                {
                    // Positive or negative zero.
                    return Int32BitsToSingle(sign << 31);
                }

                // A subnormal binary16 value is always normal when widened to binary32,
                // so shift the mantissa left until the implicit leading bit is set,
                // decrementing the exponent to compensate.
                do
                {
                    mantissa <<= 1;
                    exponent--;
                }
                while ((mantissa & 0x400) == 0);

                // Discard the now-explicit leading bit and correct for the loop
                // having started from a biased exponent of zero rather than one.
                mantissa &= 0x3FF;
                exponent++;
            }

            int rebiasedExponent = exponent - Binary16ExponentBias + Binary32ExponentBias;

            return Int32BitsToSingle(
                (sign << 31) |
                (rebiasedExponent << Binary32MantissaBits) |
                (mantissa << MantissaShift));
        }

        /// <summary>
        /// Framework independent implementation of <see cref="FromSingle"/>. Exposed
        /// separately so that it can be validated against <c>System.Half</c>.
        /// </summary>
        internal static ushort ManualFromSingle(float value)
        {
            int bits = SingleToInt32Bits(value);
            int sign = (bits >> 31) & 0x1;
            int exponent = (bits >> Binary32MantissaBits) & Binary32MaxExponent;
            int mantissa = bits & 0x7FFFFF;

            if (exponent == Binary32MaxExponent)
            {
                // Infinity or NaN. NaN is canonicalised to a quiet NaN, matching the
                // representation produced by System.Half.
                int payload = mantissa == 0 ? 0x7C00 : 0x7E00;
                return (ushort)((sign << 15) | payload);
            }

            if ((bits & 0x7FFFFFFF) == 0)
            {
                // Positive or negative zero.
                return (ushort)(sign << 15);
            }

            int targetExponent = exponent - Binary32ExponentBias + Binary16ExponentBias;

            if (targetExponent >= Binary16MaxExponent)
            {
                // Too large to represent, so saturate to an infinity.
                return (ushort)((sign << 15) | 0x7C00);
            }

            if (targetExponent <= 0)
            {
                // Too small to represent as a normal value. Values more than eleven
                // binade below the smallest subnormal cannot round up to one, so they
                // are flushed to zero rather than shifted by more than the mantissa width.
                if (targetExponent < -Binary16MantissaBits)
                {
                    return (ushort)(sign << 15);
                }

                // Restore the implicit leading bit and shift the mantissa into the
                // subnormal range, rounding to nearest even.
                mantissa |= 1 << Binary32MantissaBits;
                int shift = MantissaShift + 1 - targetExponent;
                int subnormal = RoundShiftRight(mantissa, shift);

                // Rounding may have carried into the exponent, producing the smallest
                // normal value. That is representable and needs no special handling,
                // because the carry lands in the exponent field naturally.
                return (ushort)((sign << 15) | subnormal);
            }

            int roundedMantissa = RoundShiftRight(mantissa, MantissaShift);

            if (roundedMantissa == 0x400)
            {
                // Rounding overflowed the mantissa, so carry into the exponent.
                roundedMantissa = 0;
                targetExponent++;

                if (targetExponent >= Binary16MaxExponent)
                {
                    return (ushort)((sign << 15) | 0x7C00);
                }
            }

            return (ushort)((sign << 15) | (targetExponent << Binary16MantissaBits) | roundedMantissa);
        }

        /// <summary>
        /// Shifts <paramref name="value"/> right by <paramref name="shift"/> bits,
        /// rounding the discarded bits to nearest and ties to even.
        /// </summary>
        private static int RoundShiftRight(int value, int shift)
        {
            int result = value >> shift;
            int roundBit = (value >> (shift - 1)) & 1;

            if (roundBit == 0)
            {
                // The discarded portion is below the halfway point, so round down.
                return result;
            }

            // Round up when the discarded portion is above the halfway point, or when
            // it is exactly halfway and rounding up produces an even result.
            int stickyMask = (1 << (shift - 1)) - 1;
            bool isTie = (value & stickyMask) == 0;

            return isTie && (result & 1) == 0 ? result : result + 1;
        }

        private static float Int32BitsToSingle(int value)
        {
            #if NET
            return BitConverter.Int32BitsToSingle(value);
            #else
            return BitConverterCompatible.Int32BitsToSingle(value);
            #endif
        }

        private static int SingleToInt32Bits(float value)
        {
            #if NET
            return BitConverter.SingleToInt32Bits(value);
            #else
            return BitConverterCompatible.SingleToInt32Bits(value);
            #endif
        }
    }
}
