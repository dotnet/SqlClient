// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Verifies decimal scale adjustment and RPC precision validation preserve representable values.
    /// </summary>
    [Collection(AppContextSwitchTestCollection.Name)]
    public class TdsParserDecimalTests
    {
        /// <summary>
        /// CLR and SQL zero must serialize when precision equals scale, regardless of input scale or sign.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RpcDecimalParameter_ZeroFitsPrecisionEqualToScale(bool isSqlDecimal, bool truncate)
        {
            using LocalAppContextSwitchesHelper switches = new();
            switches.TruncateScaledDecimal = truncate;

            for (byte oldScale = 0; oldScale <= 28; oldScale++)
            {
                foreach (bool negative in new[] { false, true })
                {
                    decimal zero = new(0, 0, 0, negative, oldScale);
                    object value = isSqlDecimal ? (object)new SqlDecimal(zero) : zero;
                    for (byte scale = 1; scale <= 38; scale++)
                    {
                        WriteDecimalParameter(value, scale, scale);
                    }
                }
            }
        }

        /// <summary>
        /// Excess SqlDecimal precision must not reject signed zero or a value that rounds/truncates to zero.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RpcDecimalParameter_ZeroWithExcessPrecisionFits(bool negative)
        {
            using LocalAppContextSwitchesHelper switches = new();
            foreach (bool truncate in new[] { false, true })
            {
                switches.TruncateScaledDecimal = truncate;
                WriteDecimalParameter(new SqlDecimal(38, 3, !negative, 0, 0, 0, 0), 3, 3);
                WriteDecimalParameter(SqlDecimal.ConvertToPrecScale(
                    new SqlDecimal(new decimal(0, 0, 0, negative, 3)), 38, 3), 3, 3);
                WriteDecimalParameter(new SqlDecimal(38, 4, !negative, 1, 0, 0, 0), 3, 3);
            }
        }

        /// <summary>
        /// Exempting zero must not permit nonzero CLR or SQL values with insufficient parameter precision.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RpcDecimalParameter_NonzeroExceedingPrecisionThrows(bool isSqlDecimal)
        {
            foreach (decimal value in new[] { -1m, 1m })
            {
                object parameterValue = isSqlDecimal ? (object)new SqlDecimal(value) : value;
                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => WriteDecimalParameter(parameterValue, 3, 3));
                Assert.IsType<ArgumentException>(exception.InnerException);
            }
        }

        /// <summary>
        /// Scale adjustment must retain the rounding switch and precision needed for nonzero values.
        /// </summary>
        [Theory]
        [InlineData("0.001", 3, "0.001", "0.001", 3)]
        [InlineData("0.01", 3, "0.010", "0.010", 3)]
        [InlineData("1", 3, "1.000", "1.000", 4)]
        [InlineData("-1", 3, "-1.000", "-1.000", 4)]
        [InlineData("1.005", 2, "1.01", "1.00", 3)]
        [InlineData("-1.005", 2, "-1.01", "-1.00", 3)]
        public void AdjustDecimalScale_NonzeroPreservesValueAndPrecision(
            string value, int scale, string rounded, string truncated, int precision)
        {
            using LocalAppContextSwitchesHelper switches = new();
            foreach (bool truncate in new[] { false, true })
            {
                switches.TruncateScaledDecimal = truncate;
                SqlDecimal adjusted = TdsParser.AdjustDecimalScale(
                    decimal.Parse(value, CultureInfo.InvariantCulture), scale);

                Assert.Equal(truncate ? truncated : rounded, adjusted.ToString());
                Assert.Equal(scale, adjusted.Scale);
                Assert.Equal(precision, adjusted.Precision);
            }
        }

        /// <summary>
        /// Rescaling the CLR decimal limits must not reintroduce the overflow fixed by PR #4443.
        /// </summary>
        [Theory]
        [InlineData(2)]
        [InlineData(9)]
        public void AdjustDecimalScale_LargeValuesRemainSqlDecimal(int scale)
        {
            foreach (decimal value in new[] { decimal.MinValue, decimal.MaxValue })
            {
                SqlDecimal adjusted = TdsParser.AdjustDecimalScale(value, scale);

                Assert.Equal(value.ToString(CultureInfo.InvariantCulture) + "." + new string('0', scale), adjusted.ToString());
                Assert.Equal(scale, adjusted.Scale);
                Assert.Equal(29 + scale, adjusted.Precision);
                Assert.Throws<OverflowException>(() => adjusted.Value);
            }
        }

        /// <summary>
        /// Serializes one RPC parameter into a fresh parser buffer without connecting to a server.
        /// </summary>
        /// <param name="value">The CLR decimal or SqlDecimal parameter value.</param>
        /// <param name="precision">The declared parameter precision.</param>
        /// <param name="scale">The declared parameter scale.</param>
        private static void WriteDecimalParameter(object value, byte precision, byte scale)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            TdsParser parser = new(false, false);
            object state = typeof(TdsParser).GetField("_physicalStateObj", Flags)!.GetValue(parser)!;
            SqlParameter parameter = new("@Value", SqlDbType.Decimal)
            {
                Value = value,
                Precision = precision,
                Scale = scale
            };
            parameter.Validate(0, false);

            using SqlCommand command = new();
            MethodInfo write = typeof(TdsParser).GetMethod("TDSExecuteRPCAddParameter", Flags)!;
            Assert.Null(write.Invoke(parser, new object[]
            {
                state, parameter, parameter.InternalMetaType, (byte)0, command, false
            }));
        }
    }
}
