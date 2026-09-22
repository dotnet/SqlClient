// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    /// <summary>
    /// Verifies money identity and decimal fidelity through synchronous and asynchronous bulk copy.
    /// </summary>
    [Trait("Set", "2")]
    public class SqlBulkCopyVariantTests
    {
        /// <summary>
        /// Mixed variant rows preserve money, numeric, and null values for each supported source path.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("DataTable", false)]
        [InlineData("DataTable", true)]
        [InlineData("DataRows", false)]
        [InlineData("DataRows", true)]
        [InlineData("DataTableReader", false)]
        [InlineData("DataTableReader", true)]
        [InlineData("SqlDataReader", false)]
        [InlineData("SqlDataReader", true)]
        [InlineData("MoneyDataTable", false)]
        [InlineData("MoneyDataTable", true)]
        [InlineData("MoneySqlDataReader", false)]
        [InlineData("MoneySqlDataReader", true)]
        [InlineData("StreamingDataTableReader", false)]
        [InlineData("StreamingDataTableReader", true)]
        public async Task MoneyAndDecimalRetainVariantTypes(string source, bool async)
        {
            object[] values =
            {
                new SqlMoney(1.23m), new SqlMoney(-1.2345m), SqlMoney.Zero,
                new SqlMoney(0.0001m), SqlMoney.MinValue, SqlMoney.MaxValue,
                SqlMoney.Null, DBNull.Value
            };
            if (source != "MoneyDataTable" && source != "MoneySqlDataReader")
            {
                values = values.Concat(new object[] { 1.23m, 1.23456789m, decimal.MaxValue, new SqlDecimal(-1.23456789m) }).ToArray();
            }

            using DataTable data = new DataTable();
            data.Columns.Add("Id", typeof(int));
            data.Columns.Add("Val", source == "MoneyDataTable" ? typeof(SqlMoney) : typeof(object));
            for (int i = 0; i < values.Length; i++)
            {
                data.Rows.Add(i, values[i]);
            }

            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();
            using Table destination = new Table(connection, "bulkMoneyVariant", "(Id int, Val sql_variant NULL)");
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = destination.Name,
                BatchSize = 3,
                EnableStreaming = source == "StreamingDataTableReader"
            };
            switch (source)
            {
                case "DataTable":
                case "MoneyDataTable":
                    if (async)
                    {
                        await bulkCopy.WriteToServerAsync(data);
                    }
                    else
                    {
                        bulkCopy.WriteToServer(data);
                    }
                    break;
                case "DataRows":
                    if (async)
                    {
                        await bulkCopy.WriteToServerAsync(data.Select());
                    }
                    else
                    {
                        bulkCopy.WriteToServer(data.Select());
                    }
                    break;
                case "DataTableReader":
                case "StreamingDataTableReader":
                    using (DataTableReader reader = data.CreateDataReader())
                    {
                        if (async)
                        {
                            await bulkCopy.WriteToServerAsync(reader);
                        }
                        else
                        {
                            bulkCopy.WriteToServer(reader);
                        }
                    }
                    break;
                case "SqlDataReader":
                case "MoneySqlDataReader":
                    using (SqlConnection sourceConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
                    using (SqlCommand command = sourceConnection.CreateCommand())
                    {
                        sourceConnection.Open();
                        command.CommandText = string.Join(" UNION ALL ", Enumerable.Range(0, values.Length)
                            .Select(i => $"SELECT {i} AS Id, @p{i} AS Val"));
                        for (int i = 0; i < values.Length; i++)
                        {
                            command.Parameters.Add($"@p{i}", source == "MoneySqlDataReader" ? SqlDbType.Money : SqlDbType.Variant).Value = values[i];
                        }
                        using SqlDataReader reader = command.ExecuteReader();
                        if (async)
                        {
                            await bulkCopy.WriteToServerAsync(reader);
                        }
                        else
                        {
                            bulkCopy.WriteToServer(reader);
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }

            using SqlCommand verify = new SqlCommand(
                $"SELECT Id, Val, SQL_VARIANT_PROPERTY(Val, 'BaseType'), SQL_VARIANT_PROPERTY(Val, 'Scale') FROM {destination.Name} ORDER BY Id",
                connection);
            using SqlDataReader result = verify.ExecuteReader();
            for (int i = 0; i < values.Length; i++)
            {
                Assert.True(result.Read());
                Assert.Equal(i, result.GetInt32(0));
                object expected = values[i];
                if (expected == DBNull.Value || (expected is INullable nullable && nullable.IsNull))
                {
                    Assert.True(result.IsDBNull(1));
                    Assert.True(result.IsDBNull(2));
                }
                else if (expected is SqlMoney money)
                {
                    Assert.Equal("money", result.GetString(2));
                    Assert.Equal(money, Assert.IsType<SqlMoney>(result.GetSqlValue(1)));
                    Assert.Equal(money.Value, result.GetDecimal(1));
                    Assert.Equal(4, result.GetInt32(3));
                }
                else
                {
                    decimal number = expected is SqlDecimal sqlDecimal ? sqlDecimal.Value : (decimal)expected;
                    Assert.Equal("numeric", result.GetString(2));
                    Assert.IsType<SqlDecimal>(result.GetSqlValue(1));
                    Assert.Equal(number, result.GetDecimal(1));
                    Assert.Equal((int)new SqlDecimal(number).Scale, result.GetInt32(3));
                }
            }
            Assert.False(result.Read());
        }
    }
}
