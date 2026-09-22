// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Guards the type identity and wire representation of bulk-copy variant values.
    /// </summary>
    public class SqlBulkCopyVariantTests : IDisposable
    {
        private readonly TdsParser _parser = new TdsParser(false, false);

        /// <summary>
        /// Releases the parser state after each wire-format test.
        /// </summary>
        public void Dispose() => _parser._physicalStateObj.Dispose();

        /// <summary>
        /// Money must retain its type through validation and use the signed, scaled money payload.
        /// </summary>
        [Theory]
        [InlineData(12300L)]
        [InlineData(-12345L)]
        [InlineData(0L)]
        [InlineData(1L)]
        [InlineData(long.MinValue)]
        [InlineData(long.MaxValue)]
        public void MoneyRetainsTypeAndPayload(long scaledValue)
        {
            SqlMoney money = new SqlMoney(scaledValue / 10000m);
            object value = ValidateVariant(money);
            Assert.Equal(money, Assert.IsType<SqlMoney>(value));

            TdsParserStateObject state = _parser._physicalStateObj;
            int start = state._outBytesUsed;
            Assert.Null(_parser.WriteSqlVariantDataRowValue(value, state));

            Assert.Equal(14, state._outBytesUsed - start);
            Assert.Equal(10, BitConverter.ToInt32(state._outBuff, start));
            Assert.Equal(TdsEnums.SQLMONEY, state._outBuff[start + 4]);
            Assert.Equal(0, state._outBuff[start + 5]);
            Assert.Equal(unchecked((int)(scaledValue >> 32)), BitConverter.ToInt32(state._outBuff, start + 6));
            Assert.Equal(unchecked((uint)scaledValue), BitConverter.ToUInt32(state._outBuff, start + 10));
        }

        /// <summary>
        /// Both decimal representations remain numeric, even when the value also fits in money.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DecimalRemainsNumeric(bool sqlType)
        {
            const decimal Expected = 1.23456789m;
            object value = ValidateVariant(sqlType ? (object)new SqlDecimal(Expected) : Expected);
            Assert.Equal(Expected, Assert.IsType<decimal>(value));

            TdsParserStateObject state = _parser._physicalStateObj;
            int start = state._outBytesUsed;
            Assert.Null(_parser.WriteSqlVariantDataRowValue(value, state));

            Assert.Equal(25, state._outBytesUsed - start);
            Assert.Equal(21, BitConverter.ToInt32(state._outBuff, start));
            Assert.Equal(TdsEnums.SQLNUMERICN, state._outBuff[start + 4]);
            Assert.Equal(2, state._outBuff[start + 5]);
            Assert.Equal(8, state._outBuff[start + 7]);
        }

        /// <summary>
        /// Supplies the null representations accepted by the bulk-copy variant writer.
        /// </summary>
        public static IEnumerable<object?[]> NullValues()
        {
            yield return new object?[] { null };
            yield return new object?[] { DBNull.Value };
            yield return new object?[] { SqlMoney.Null };
        }

        /// <summary>
        /// Null money must be written as a null variant without reading SqlMoney.Value.
        /// </summary>
        [Theory]
        [MemberData(nameof(NullValues), DisableDiscoveryEnumeration = true)]
        public void NullWritesOnlyNullLength(object? value)
        {
            TdsParserStateObject state = _parser._physicalStateObj;
            int start = state._outBytesUsed;
            Assert.Null(_parser.WriteSqlVariantDataRowValue(value, state));

            Assert.Equal(4, state._outBytesUsed - start);
            Assert.Equal(0, BitConverter.ToInt32(state._outBuff, start));
        }

        /// <summary>
        /// Runs the bulk-copy conversion step without requiring a server connection.
        /// </summary>
        /// <param name="value">A non-null variant value.</param>
        /// <returns>The value passed to the variant writer.</returns>
        private static object ValidateVariant(object value)
        {
            using SqlConnection connection = new SqlConnection();
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);
            return typeof(SqlBulkCopy)
                .GetMethod("ValidateBulkCopyVariant", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(bulkCopy, new[] { value })!;
        }
    }
}
