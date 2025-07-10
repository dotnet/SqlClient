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
        
        public static TheoryData<byte[]> ByteBinaryCtor_NonNullInput_Data => 
            new TheoryData<byte[]>
            {
                Array.Empty<byte>(),
                new byte[] { 1, 2, 3, 4},
            };
        
        [Theory]
        [MemberData(nameof(ByteBinaryCtor_NonNullInput_Data))]
        public void SqlBinaryCtor_NonNullInput(byte[] input)
        {
            // Act
            SqlBinary result = SqlTypeWorkarounds.SqlBinaryCtor(input, true);
            
            // Assert
            Assert.False(result.IsNull);
            Assert.Equal(input, result.Value);
        }
        
        [Fact]
        public void SqlBinaryCtor_NullInput()
        {
            // Act
            SqlBinary result = SqlTypeWorkarounds.SqlBinaryCtor(null, true);
            
            // Assert
            Assert.True(result.IsNull);
        }
        
        #endregion
        
        #region SqlDecimal
        
        public static TheoryData<SqlDecimal> SqlDecimalExtractData_NonNullInput_Data =>
            new TheoryData<SqlDecimal>
            {
                SqlDecimal.MinValue,
                new SqlDecimal(-1.2345678),
                new SqlDecimal(0),
                new SqlDecimal(1.2345678),
                SqlDecimal.MaxValue,
            };
        
        [Theory]
        [MemberData(nameof(SqlDecimalExtractData_NonNullInput_Data))]
        public void SqlDecimalExtractData_NonNullInput(SqlDecimal input)
        {
            // Act
            SqlTypeWorkarounds.SqlDecimalExtractData(
                input,
                out uint data1,
                out uint data2,
                out uint data3,
                out uint data4);
            
            // Assert
            int[] expected = input.Data;
            Assert.Equal(expected[0], (int)data1);
            Assert.Equal(expected[1], (int)data2);
            Assert.Equal(expected[2], (int)data3);
            Assert.Equal(expected[3], (int)data4);
        }
        
        [Fact]
        public void SqlDecimalExtractData_NullInput()
        {
            Action action = () =>
            {
                // Arrange
                SqlDecimal input = SqlDecimal.Null;
                
                // Act
                SqlTypeWorkarounds.SqlDecimalExtractData(input, out _, out _, out _, out _);
            };
            
            // Assert
            Assert.Throws<SqlNullValueException>(action);
        }
        
        #endregion
        
        #region SqlGuid
        
        public static TheoryData<byte[]?> SqlGuidCtor_InvalidInput_Data =>
            new TheoryData<byte[]?>
            {
                null,
                Array.Empty<byte>(),
                new byte[] { 1, 2, 3, 4 }, // Too short
                new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 } // Too long
            };
        
        [Theory]
        [MemberData(nameof(SqlGuidCtor_InvalidInput_Data))]
        public void SqlGuidCtor_InvalidInput(byte[]? input)
        {
            // Act
            Action action = () => SqlTypeWorkarounds.SqlGuidCtor(input, true);
            
            // Assert
            Assert.Throws<ArgumentException>(action);
        }
        
        public static TheoryData<byte[]> SqlGuidCtor_ValidInput_Data => 
            new TheoryData<byte[]>
            {
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0,  0,  0,  0,  0,  0,  0,  0 },
                new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }
            };
        
        [Theory]
        [MemberData(nameof(SqlGuidCtor_ValidInput_Data))]
        public void SqlGuidCtor_ValidInput(byte[] input)
        {
            // Act
            SqlGuid result = SqlTypeWorkarounds.SqlGuidCtor(input, true);
            
            // Assert
            Assert.False(result.IsNull);
            Assert.Equal(input, result.Value.ToByteArray());
        }
        
        #endregion
        
        #region SqlMoney

        public static TheoryData<long, SqlMoney> SqlMoneyCtor_Data =>
            new TheoryData<long, SqlMoney>
            {
                { long.MinValue, SqlMoney.MinValue },
                { (long)((decimal)-123000000 / 10000), new SqlMoney(-1.23) },
                { 0, SqlMoney.Zero },
                { (long)((decimal)123000000 / 10000), new SqlMoney(1.23) },
                { long.MaxValue, SqlMoney.MaxValue },
            };
        
        [Theory]
        [MemberData(nameof(SqlMoneyCtor_Data))]
        public void SqlMoneyCtor(long input, SqlMoney expected)
        {
            // Act
            SqlMoney result = SqlTypeWorkarounds.SqlMoneyCtor(input, 1);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        public static TheoryData<SqlMoney, long> SqlMoneyToSqlInternalRepresentation_NonNullInput_Data =>
            new TheoryData<SqlMoney, long>
            {
                { SqlMoney.MinValue, long.MinValue },
                { new SqlMoney(-1.23), (long)(new SqlMoney(-1.23).ToDecimal() * 10000) },
                { SqlMoney.Zero, 0 },
                { new SqlMoney(1.23), (long)(new SqlMoney(1.23).ToDecimal() * 10000) },
                { SqlMoney.MaxValue, long.MaxValue },
            };
        
        [Theory]
        [MemberData(nameof(SqlMoneyToSqlInternalRepresentation_NonNullInput_Data))]
        public void SqlMoneyToSqlInternalRepresentation_NonNullInput(SqlMoney input, long expected)
        {
            // Act
            long result = SqlTypeWorkarounds.SqlMoneyToSqlInternalRepresentation(input);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        [Fact]
        public void SqlMoneyToSqlInternalRepresentation_NullInput()
        {
            // Arrange
            SqlMoney input = SqlMoney.Null;
            
            // Act
            Action action = () => SqlTypeWorkarounds.SqlMoneyToSqlInternalRepresentation(input);
            
            // Assert
            Assert.Throws<SqlNullValueException>(action);
        }
        
        #endregion
        
        #endif
    }
}
