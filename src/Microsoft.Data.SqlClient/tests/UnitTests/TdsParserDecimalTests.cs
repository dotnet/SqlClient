// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Globalization;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Verifies decimal scale adjustment preserves values and the precision required by RPC parameters.
    /// </summary>
    [Collection(AppContextSwitchTestCollection.Name)]
    public class TdsParserDecimalTests
    {
        /// <summary>
        /// Zero must fit when parameter precision equals scale, regardless of its CLR scale or sign.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AdjustDecimalScale_ZeroUsesMinimumPrecision(bool truncate)
        {
            using LocalAppContextSwitchesHelper switches = new();
            switches.TruncateScaledDecimal = truncate;

            for (byte oldScale = 0; oldScale <= 28; oldScale++)
            {
                foreach (bool negative in new[] { false, true })
                {
                    decimal value = new(0, 0, 0, negative, oldScale);
                    for (int newScale = 0; newScale <= 38; newScale++)
                    {
                        SqlDecimal adjusted = TdsParser.AdjustDecimalScale(value, newScale);

                        Assert.Equal(newScale, adjusted.Scale);
                        Assert.Equal(Math.Max(1, newScale), adjusted.Precision);
                        Assert.Equal(new int[4], adjusted.Data);
                    }
                }
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
    }
}
