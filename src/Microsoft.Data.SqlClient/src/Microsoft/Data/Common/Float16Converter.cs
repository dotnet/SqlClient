// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
#if NETFRAMEWORK
using Microsoft.Data.SqlClient;
#endif

namespace Microsoft.Data.Common
{
    /// <summary>
    /// Widens IEEE 754 binary16 (half precision) values to binary32 (single precision).
    /// </summary>
    /// <remarks>
    /// <para>
    /// SQL Server transports <c>vector(N, float16)</c> elements as raw binary16
    /// values. <c>System.Half</c> is only available on .NET, so the conversion is
    /// implemented manually for .NET Framework.
    /// </para>
    /// <para>
    /// The manual implementation is compiled for every target framework, rather
    /// than only for .NET Framework, so that it can be validated exhaustively
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

        // The difference in mantissa widths when widening binary16 to binary32.
        private const int MantissaShift = Binary32MantissaBits - Binary16MantissaBits;

        /// <summary>
        /// The most significant mantissa bit of a binary32 value, which distinguishes a
        /// quiet NaN from a signalling one.
        /// </summary>
        private const int Binary32QuietBit = 1 << (Binary32MantissaBits - 1);

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
        /// Widens float16 to float32 for .NET Framework, which has no <c>System.Half</c>.
        /// Compiled on .NET as well so tests can compare it against <c>System.Half</c>;
        /// the .NET runtime path in <see cref="ToSingle"/> uses <c>System.Half</c> instead.
        /// </summary>
        internal static float ManualToSingle(ushort bits)
        {
            int sign = (bits >> 15) & 0x1;
            int exponent = (bits >> Binary16MantissaBits) & Binary16MaxExponent;
            int mantissa = bits & 0x3FF;

            if (exponent == Binary16MaxExponent)
            {
                // Infinity or NaN. A NaN keeps its sign and payload, with the quiet bit
                // forced, which is what a System.Half conversion produces. A signalling
                // NaN is therefore quietened, as IEEE 754 requires of a conversion.
                return mantissa == 0
                    ? Int32BitsToSingle((sign << 31) | (Binary32MaxExponent << Binary32MantissaBits))
                    : Int32BitsToSingle(
                        (sign << 31) |
                        (Binary32MaxExponent << Binary32MantissaBits) |
                        Binary32QuietBit |
                        (mantissa << MantissaShift));
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

        private static float Int32BitsToSingle(int value)
        {
            #if NET
            return BitConverter.Int32BitsToSingle(value);
            #else
            return BitConverterCompatible.Int32BitsToSingle(value);
            #endif
        }
    }
}
