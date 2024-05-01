// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class DateTimeOffsetList : SqlDataRecord
    {
        public DateTimeOffsetList(DateTimeOffset dateTimeOffset)
            : base(new SqlMetaData("dateTimeOffset", SqlDbType.DateTimeOffset, 0, 1)) // this is using scale 1
        {
            this.SetValues(dateTimeOffset);
        }
    }

    public class DateTimeOffsetVariableScale : SqlDataRecord
    {
        public DateTimeOffsetVariableScale(DateTimeOffset dateTimeOffset, int scale)
            : base(new SqlMetaData("dateTimeOffset", SqlDbType.DateTimeOffset, 0, (byte)scale)) // this is using variable scale
        {
            this.SetValues(dateTimeOffset);
        }
    }

    public class UdtDateTimeOffsetTest
    {
        private static readonly long[] TICKS_FROM_SCALE = {
            10000000,
            1000000,
            100000,
            10000,
            1000,
            100,
            10,
            1,
        };

        private readonly string _connectionString = null;
        private readonly string _udtTableType = DataTestUtility.GetUniqueNameForSqlServer("DataTimeOffsetTableType");

        public UdtDateTimeOffsetTest()
        {
            _connectionString = DataTestUtility.TCPConnectionString;
        }

        // This unit test is for the reported issue #2423 using a specific scale of 1
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void SelectFromSqlParameterShouldSucceed()
        {
            using SqlConnection connection = new(_connectionString);
            connection.Open();
            SetupUserDefinedTableType(connection, _udtTableType);

            try
            {
                DateTimeOffset dateTimeOffset = new DateTimeOffset(2024, 1, 1, 23, 59, 59, 500, TimeSpan.Zero);
                var param = new SqlParameter
                {
                    ParameterName = "@params",
                    SqlDbType = SqlDbType.Structured,
                    TypeName = $"dbo.{_udtTableType}",
                    Value = new DateTimeOffsetList[] { new DateTimeOffsetList(dateTimeOffset) }
                };

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM @params";
                    cmd.Parameters.Add(param);
                    var result = cmd.ExecuteScalar();
                    Assert.Equal(dateTimeOffset, result);
                }
            }
            finally
            {
                DataTestUtility.DropUserDefinedType(connection, _udtTableType);
            }
        }

        // This unit test is to ensure that time in DateTimeOffset with all scales are working as expected
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void DateTimeOffsetAllScalesTestShouldSucceed()
        {
            string tvpTypeName = DataTestUtility.GetUniqueNameForSqlServer("tvpType");

            using SqlConnection connection = new(_connectionString);
            connection.Open();

            try
            {
                // Use different scale for each test: 0 to 7
                int fromScale = 0;
                int toScale = 7;

                for (int scale = fromScale; scale <= toScale; scale++)
                {
                    DateTimeOffset dateTimeOffset = new DateTimeOffset(2024, 1, 1, 23, 59, 59, TimeSpan.Zero);
                    // This additional precision is to compare the time part of the DateTimeOffset with the scale used in the test.
                    dateTimeOffset = dateTimeOffset.AddSeconds(.123456789012);

                    DataTestUtility.DropUserDefinedType(connection, tvpTypeName);
                    SetupDateTimeOffsetTableType(connection,tvpTypeName, scale);

                    var param = new SqlParameter
                    {
                        ParameterName = "@params",
                        SqlDbType = SqlDbType.Structured,
                        Scale = (byte)scale,
                        TypeName = $"dbo.{tvpTypeName}",
                        Value = new DateTimeOffsetVariableScale[] { new DateTimeOffsetVariableScale(dateTimeOffset, scale) }
                    };

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT * FROM @params";
                        cmd.Parameters.Add(param);
                        var result = cmd.ExecuteScalar();

                        if (dateTimeOffset != (DateTimeOffset)result)
                        {
                            Console.WriteLine($"Scale: {scale}    dateTimeOffset:  {dateTimeOffset}   result:  {result}");
                        }

                        // Get the time part of the DateTimeOffset and scale it to the scale used in the test.
                        long timeScaledInput = dateTimeOffset.TimeOfDay.Ticks / TICKS_FROM_SCALE[scale];
                        // Get the time part of the result and scale it to the scale used in the test
                        long timeScaledOutput = ((DateTimeOffset)result).TimeOfDay.Ticks / TICKS_FROM_SCALE[scale];

                        // Both time parts should be the same. The parameter passed in should be identical to the output regardless of scale used. 
                        Assert.Equal(timeScaledInput, timeScaledOutput);
                    }
                }
            }
            finally
            {
                DataTestUtility.DropUserDefinedType(connection, tvpTypeName);
            }
        }

        private static void SetupUserDefinedTableType(SqlConnection connection, string tableTypeName)
        {
            using (SqlCommand cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $"CREATE TYPE {tableTypeName} AS TABLE ([Value] DATETIMEOFFSET(1) NOT NULL) ";
                cmd.ExecuteNonQuery();
            }
        }

        private static void SetupDateTimeOffsetTableType(SqlConnection connection, string tableTypeName, int scale)
        {
            using (SqlCommand cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $"CREATE TYPE {tableTypeName} AS TABLE ([Value] DATETIMEOFFSET({scale}) NOT NULL) ";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
