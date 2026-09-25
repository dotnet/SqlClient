// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Verifies optimized-binding diagnostics before an RPC reaches the server.
    /// </summary>
    [Collection(SimulatedServerTestCollection.Name)]
    public class SqlCommandOptimizedBindingTests
    {
        /// <summary>
        /// Deferred preparation reports the incompatible option rather than blaming an unnamed parameter.
        /// </summary>
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        [InlineData(true, true, true)]
        public async Task Prepare_OptimizedBinding_ReportsIncompatibleOption(bool async, bool enableBeforePrepare, bool reopen)
        {
            using TdsServer server = new();
            server.Start();
            using SqlConnection connection = CreateConnection(server);
            connection.Open();
            using SqlCommand command = new("SELECT @value", connection);
            command.Parameters.Add("@value", SqlDbType.Int).Value = 1;
            command.EnableOptimizedParameterBinding = enableBeforePrepare;
            command.Prepare();
            command.EnableOptimizedParameterBinding = true;
            if (reopen)
            {
                connection.Close();
                connection.Open();
            }

            InvalidOperationException exception = async
                ? await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteNonQueryAsync())
                : Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

            Assert.Equal(Strings.SQL_PrepareNotSupportedForOptimizedBinding, exception.Message);
            Assert.Contains("Prepare", exception.Message);
            Assert.Contains("EnableOptimizedParameterBinding", exception.Message);
            Assert.Contains("new SqlCommand", exception.Message);
            Assert.Contains("before its first execution", exception.Message);
            Assert.DoesNotContain("Parameter ''", exception.Message);
        }

        /// <summary>
        /// User output parameters retain their existing diagnostic when no preparation is requested.
        /// </summary>
        [Theory]
        [InlineData(false, ParameterDirection.Output)]
        [InlineData(true, ParameterDirection.Output)]
        [InlineData(false, ParameterDirection.InputOutput)]
        [InlineData(true, ParameterDirection.InputOutput)]
        public async Task Unprepared_OutputParameter_PreservesDiagnostic(bool async, ParameterDirection direction)
        {
            using TdsServer server = new();
            server.Start();
            using SqlConnection connection = CreateConnection(server);
            connection.Open();
            using SqlCommand command = new("SELECT @value = 1", connection);
            command.EnableOptimizedParameterBinding = true;
            SqlParameter parameter = command.Parameters.Add("@value", SqlDbType.Int);
            parameter.Direction = direction;
            parameter.Value = 1;

            InvalidOperationException exception = async
                ? await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteNonQueryAsync())
                : Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

            Assert.Equal(SQL.ParameterDirectionInvalidForOptimizedBinding("@value").Message, exception.Message);
        }

        /// <summary>
        /// Enabling the option does not replace connection validation or reject no-op preparation.
        /// </summary>
        [Fact]
        public void Prepare_ClosedConnection_PreservesValidationAndNoOps()
        {
            using SqlConnection connection = new();
            using SqlCommand command = new("SELECT @value", connection);
            command.Parameters.Add("@value", SqlDbType.Int).Value = 1;
            string expected = Assert.Throws<InvalidOperationException>(() => command.Prepare()).Message;
            command.EnableOptimizedParameterBinding = true;
            Assert.Equal(expected, Assert.Throws<InvalidOperationException>(() => command.Prepare()).Message);

            command.CommandType = CommandType.StoredProcedure;
            command.Prepare();
            command.CommandType = CommandType.Text;
            command.Parameters.Clear();
            command.CommandText = "SELECT 1";
            command.Prepare();
        }

        /// <summary>
        /// Creates a nonpooled connection to the ephemeral simulated server.
        /// </summary>
        /// <param name="server">The running simulated server.</param>
        /// <returns>A connection owned by the calling test.</returns>
        private static SqlConnection CreateConnection(TdsServer server) =>
            new(new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false
            }.ConnectionString);
    }
}
