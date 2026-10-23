// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
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
                parameter.Value = new string('a', offset) + new string('\u00e9', 4001);
                if (commandMode == 2)
                {
                    command.Prepare();
                }

                // Reuse the command across the byte boundary, including a prepared handle.
                foreach (int count in new[] { 4001, 4000, 4001 })
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
    }
}
