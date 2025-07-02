// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using Microsoft.Data.SqlTypes;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class SqlTypeWorkaroundsTests
    {
        // @TODO: Need a facade pattern for Type getting so we can test the case where reflection fails
        
        #if NETFRAMEWORK
        
        #region SqlBinary
        
        public static TheoryData<byte[]> ByteArrayToSqlBinary_NonNullInput_Data => 
            new TheoryData<byte[]>
            {
                Array.Empty<byte>(),
                new byte[] { 1, 2, 3, 4},
            };
        
        [Theory]
        [MemberData(nameof(ByteArrayToSqlBinary_NonNullInput_Data))]
        public void ByteArrayToSqlBinary_NonNullInput(byte[] input)
        {
            // Act
            SqlBinary result = SqlTypeWorkarounds.ByteArrayToSqlBinary(input);
            
            // Assert
            Assert.False(result.IsNull);
            Assert.Equal(input, result.Value);
        }
        
        [Fact]
        public void ByteArrayToSqlBinary_NullInput()
        {
            // Act
            SqlBinary result = SqlTypeWorkarounds.ByteArrayToSqlBinary(null);
            
            // Assert
            Assert.True(result.IsNull);
        }
        
        #endregion
        
        #region SqlDecimal
        
        public static TheoryData<SqlDecimal> SqlDecimalWriteTdsValue_NonNullInput_Data =>
            new TheoryData<SqlDecimal>
            {
                SqlDecimal.MinValue,
                new SqlDecimal(-1.2345678),
                new SqlDecimal(0),
                new SqlDecimal(1.2345678),
                SqlDecimal.MaxValue,
            };
        
        [Theory]
        [MemberData(nameof(SqlDecimalWriteTdsValue_NonNullInput_Data))]
        public void SqlDecimalWriteTdsValue_NonNullInput(SqlDecimal input)
        {
            // Arrange
            Span<uint> output = stackalloc uint[4];
            
            // Act
            SqlTypeWorkarounds.SqlDecimalWriteTdsValue(input, output);
            
            // Assert
            int[] expected = input.Data;
            Assert.Equal(expected[0], (int)output[0]);
            Assert.Equal(expected[1], (int)output[1]);
            Assert.Equal(expected[2], (int)output[2]);
            Assert.Equal(expected[3], (int)output[3]);
        }
        
        [Fact]
        public void SqlDecimalWriteTdsValue_NullInput()
        {
            Action action = () =>
            {
                // Arrange
                SqlDecimal input = SqlDecimal.Null;
                Span<uint> output = stackalloc uint[4];
                
                // Act
                SqlTypeWorkarounds.SqlDecimalWriteTdsValue(input, output);
            };
            
            // Assert
            Assert.Throws<SqlNullValueException>(action);
        }
        
        #endregion
        
        #region SqlGuid
        
        public static TheoryData<byte[]?> ByteArrayToSqlGuid_InvalidInput_Data =>
            new TheoryData<byte[]?>
            {
                null,
                Array.Empty<byte>(),
                new byte[] { 1, 2, 3, 4 }, // Too short
                new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 } // Too long
            };
        
        [Theory]
        [MemberData(nameof(ByteArrayToSqlGuid_InvalidInput_Data))]
        public void ByteArrayToSqlGuid_InvalidInput(byte[]? input)
        {
            // Act
            Action action = () => SqlTypeWorkarounds.ByteArrayToSqlGuid(input);
            
            // Assert
            Assert.Throws<ArgumentException>(action);
        }
        
        public static TheoryData<byte[]> ByteArrayToSqlGuid_ValidInput_Data => 
            new TheoryData<byte[]>
            {
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0,  0,  0,  0,  0,  0,  0,  0 },
                new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }
            };
        
        [Theory]
        [MemberData(nameof(ByteArrayToSqlGuid_ValidInput_Data))]
        public void ByteArrayToSqlGuid_ValidInput(byte[] input)
        {
            // Act
            SqlGuid result = SqlTypeWorkarounds.ByteArrayToSqlGuid(input);
            
            // Assert
            Assert.False(result.IsNull);
            Assert.Equal(input, result.Value.ToByteArray());
        }
        
        #endregion
        
        #region SqlMoney

        public static TheoryData<long, SqlMoney> LongToSqlMoney_Data =>
            new TheoryData<long, SqlMoney>
            {
                { long.MinValue, SqlMoney.MinValue },
                { (long)((decimal)-123000000 / 10000), new SqlMoney(-1.23) },
                { 0, SqlMoney.Zero },
                { (long)((decimal)123000000 / 10000), new SqlMoney(1.23) },
                { long.MaxValue, SqlMoney.MaxValue },
            };
        
        [Theory]
        [MemberData(nameof(LongToSqlMoney_Data))]
        public void LongToSqlMoney(long input, SqlMoney expected)
        {
            // Act
            SqlMoney result = SqlTypeWorkarounds.LongToSqlMoney(input);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        public static TheoryData<SqlMoney, long> SqlMoneyToLong_NonNullInput_Data =>
            new TheoryData<SqlMoney, long>
            {
                { SqlMoney.MinValue, long.MinValue },
                { new SqlMoney(-1.23), (long)(new SqlMoney(-1.23).ToDecimal() * 10000) },
                { SqlMoney.Zero, 0 },
                { new SqlMoney(1.23), (long)(new SqlMoney(1.23).ToDecimal() * 10000) },
                { SqlMoney.MaxValue, long.MaxValue },
            };
        
        [Theory]
        [MemberData(nameof(SqlMoneyToLong_NonNullInput_Data))]
        public void SqlMoneyToLong_NonNullInput(SqlMoney input, long expected)
        {
            // Act
            long result = SqlTypeWorkarounds.SqlMoneyToLong(input);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        [Fact]
        public void SqlMoneyToLong_NullInput()
        {
            // Arrange
            SqlMoney input = SqlMoney.Null;
            
            // Act
            Action action = () => SqlTypeWorkarounds.SqlMoneyToLong(input);
            
            // Assert
            Assert.Throws<SqlNullValueException>(action);
        }
        
        #endregion
        
        #endif
    }
}
