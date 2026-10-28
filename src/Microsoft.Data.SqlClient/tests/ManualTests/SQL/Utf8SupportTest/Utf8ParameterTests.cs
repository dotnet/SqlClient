// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies that UTF-8 RPC values crossing 8000 bytes round-trip without data loss.
    /// </summary>
    [Trait("Set", "3")]
    public static class Utf8ParameterTests
    {
        /// <summary>
        /// Covers text, stored-procedure and prepared RPCs with sync and async execution.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsUTF8Supported), nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, 0, 8000, 0)]
        [InlineData(true, 0, 8000, 0)]
        [InlineData(false, 1, 8000, 0)]
        [InlineData(true, 1, 8000, 0)]
        [InlineData(false, 2, 8000, 0)]
        [InlineData(true, 2, 8000, 0)]
        [InlineData(false, 0, 0, 1)]
        [InlineData(true, 0, 0, 1)]
        [InlineData(false, 1, 0, 1)]
        [InlineData(true, 1, 0, 1)]
        [InlineData(false, 0, 8000, 1)]
        [InlineData(true, 0, 8000, 1)]
        [InlineData(false, 1, 8000, 1)]
        [InlineData(true, 1, 8000, 1)]
        [InlineData(false, 2, 8000, 1)]
        [InlineData(true, 2, 8000, 1)]
        public static async Task VarCharUtf8ByteBoundaryRoundTrip(
            bool useAsync, int commandMode, int size, int offset)
        {
            string database = DataTestUtility.GetLongName("Utf8Parameter", false);
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                InitialCatalog = "master"
            };
            using SqlConnection admin = DataTestUtility.CreateConnection(builder.ConnectionString);
            admin.Open();
            using (SqlCommand create = admin.CreateCommand())
            {
                create.CommandText = $"CREATE DATABASE [{database}] " +
                    "COLLATE Latin1_General_100_CI_AS_SC_UTF8";
                create.ExecuteNonQuery();
            }
            try
            {
                builder.InitialCatalog = database;
                using SqlConnection connection = DataTestUtility.CreateConnection(
                    builder.ConnectionString);
                if (useAsync)
                {
                    await connection.OpenAsync();
                }
                else
                {
                    connection.Open();
                }
                using StoredProcedure procedure = new(
                    connection, "Utf8Echo", "@p varchar(max) AS SELECT @p");
                using SqlCommand command = connection.CreateCommand();
                command.CommandText = commandMode == 1 ? procedure.Name : "SELECT @p";
                command.CommandType = commandMode == 1
                    ? CommandType.StoredProcedure
                    : CommandType.Text;
                SqlParameter parameter = command.Parameters.Add("@p", SqlDbType.VarChar, size);
                parameter.Offset = offset;
                parameter.Value = new string('a', offset) + new string('\u00e9', 4000);
                if (commandMode == 2)
                {
                    command.Prepare();
                }

                // Reuse the command across the byte boundary, including a prepared handle.
                foreach (int count in new[] { 4000, 4001, 4000, 4001 })
                {
                    string text = new('\u00e9', count);
                    parameter.Value = new string('a', offset) + text;
                    object result = useAsync
                        ? await command.ExecuteScalarAsync()
                        : command.ExecuteScalar();
                    Assert.Equal(text, result);
                }
            }
            finally
            {
                DataTestUtility.DropDatabase(admin, database);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsUTF8Supported), nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, 0)]
        [InlineData(true, 0)]
        [InlineData(false, 1)]
        [InlineData(true, 1)]
        [InlineData(false, 2)]
        [InlineData(true, 2)]
        [InlineData(false, 3)]
        [InlineData(true, 3)]
        [InlineData(false, 4)]
        [InlineData(true, 4)]
        [InlineData(false, 5)]
        [InlineData(true, 5)]
        [InlineData(false, 6)]
        [InlineData(true, 6)]
        [InlineData(false, 7)]
        [InlineData(true, 7)]
        public static async Task PreparedVarChar_GrowingByteCapacity_PreservesValueAndReuse(
            bool useAsync, int scenario)
        {
            string database = DataTestUtility.GetLongName("Utf8Prepared", false);
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                InitialCatalog = "master",
                Pooling = false
            };
            using SqlConnection admin = new(builder.ConnectionString);
            admin.Open();
            using (SqlCommand create = admin.CreateCommand())
            {
                create.CommandText = $"CREATE DATABASE [{database}] " +
                    "COLLATE Latin1_General_100_CI_AS_SC_UTF8";
                create.ExecuteNonQuery();
            }
            try
            {
                builder.InitialCatalog = database;
                using SqlConnection connection = new(builder.ConnectionString);
                await connection.OpenAsync();
                using SqlConnection replacement = new(builder.ConnectionString);
                using SqlCommand command = connection.CreateCommand();
                bool insert = scenario == 2;
                if (insert)
                {
                    using SqlCommand create = new("CREATE TABLE ValuesTable (Value varchar(max))",
                        connection);
                    create.ExecuteNonQuery();
                }
                command.CommandText = insert
                    ? "INSERT INTO ValuesTable VALUES (@p); SELECT Value FROM ValuesTable; " +
                        "DELETE FROM ValuesTable"
                    : "SELECT @p";
                SqlParameter parameter = command.Parameters.Add("@p", SqlDbType.VarChar,
                    scenario == 1 ? 8000 : 4000);
                string original = scenario == 1
                    ? new string('\u00e9', 4001)
                    : new string('a', 4000);
                parameter.Value = original;
                parameter.Offset = scenario == 1 ? 1 : 0;
                command.Prepare();

                object first = useAsync
                    ? await command.ExecuteScalarAsync() : command.ExecuteScalar();
                Assert.Equal(original.Substring(parameter.Offset), first);
                FieldInfo handleField = typeof(SqlCommand).GetField("_prepareHandle",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                object originalHandle = handleField.GetValue(command);

                // Equal byte capacities should keep the original server preparation.
                Assert.Equal(first, useAsync
                    ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
                Assert.Equal(originalHandle, handleField.GetValue(command));

                if (scenario == 3)
                {
                    connection.Close();
                    await connection.OpenAsync();
                }
                else if (scenario == 4 || scenario == 7)
                {
                    // A failed reprepare must discard the old declaration's handle.
                    command.CommandText = "SELECT @p FROM MissingUtf8PreparedTable";
                    if (useAsync)
                    {
                        await Assert.ThrowsAsync<SqlException>(() => scenario == 7
                            ? command.ExecuteNonQueryAsync() : command.ExecuteScalarAsync());
                    }
                    else
                    {
                        Assert.Throws<SqlException>(() =>
                        {
                            if (scenario == 7)
                            {
                                command.ExecuteNonQuery();
                            }
                            else
                            {
                                command.ExecuteScalar();
                            }
                        });
                    }
                    command.CommandText = "SELECT @p";
                }
                else if (scenario == 5)
                {
                    await replacement.OpenAsync();
                    command.Connection = replacement;
                    command.Prepare();
                }
                else if (scenario == 6)
                {
                    // A collection/schema change must replace the entire capacity snapshot.
                    command.Parameters.Add("@unused", SqlDbType.Int).Value = 1;
                }

                parameter.Offset = 0;
                string grown = new('\u00e9', scenario == 1 ? 4001 : 4000);
                if (scenario != 1)
                {
                    parameter.Value = grown;
                }
                Assert.Equal(grown, useAsync
                    ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
                object grownHandle = handleField.GetValue(command);
                if (scenario < 3)
                {
                    Assert.NotEqual(originalHandle, grownHandle);
                }

                // Shrinking and regrowing within the new capacity (including MAX) reuse it.
                foreach (string value in new[] { "small", grown })
                {
                    parameter.Value = value;
                    Assert.Equal(value, useAsync
                        ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
                    Assert.Equal(grownHandle, handleField.GetValue(command));
                }
            }
            finally
            {
                DataTestUtility.DropDatabase(admin, database);
            }
        }
    }
}
