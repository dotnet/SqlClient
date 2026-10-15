// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Prevents other test collections' ClearAllPools calls from invalidating pool reuse assertions.
    /// </summary>
    [CollectionDefinition(nameof(ScalarFailureCleanupTest), DisableParallelization = true)]
    public sealed class ScalarFailureCleanupCollection
    {
    }

    /// <summary>
    /// Verifies scalar cleanup preserves fatal failures and quarantines the interrupted connection,
    /// while ordinary SQL errors retain normal draining and pooling.
    /// </summary>
    [Collection(nameof(ScalarFailureCleanupTest))]
    [Trait("Set", "3")]
    public sealed class ScalarFailureCleanupTest
    {
        /// <summary>
        /// A preallocated fatal exception from an InfoMessage callback must not be replaced by
        /// a later SQL error during cleanup. Exercise both scalar implementations with and without MARS.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public async Task ScalarFailure_PreservesExceptionAndPoolSafety(bool async, bool mars, bool fatal)
        {
            var builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                ApplicationName = nameof(ScalarFailureCleanupTest) + Guid.NewGuid(),
                Pooling = true,
                MaxPoolSize = 1,
                MinPoolSize = 0,
                ConnectRetryCount = 0,
                MultipleActiveResultSets = mars,
                Enlist = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            try
            {
                await OpenConnection(connection, async);
                var original = new InternalConnectionWrapper(connection);
                Guid originalId = connection.ClientConnectionId;
                connection.Close();
                await OpenConnection(connection, async);
                Assert.True(original.IsInternalConnectionOf(connection));
                Assert.Equal(originalId, connection.ClientConnectionId);
                object parser = ConnectionHelper.GetParser(connection.GetInternalConnection());

                var failure = new OutOfMemoryException("Scalar cleanup sentinel");
                int notifications = 0;
                connection.InfoMessage += (_, args) =>
                {
                    Assert.Contains("Scalar cleanup sentinel", args.Message);
                    notifications++;
                    if (fatal)
                    {
                        throw failure;
                    }
                };

                // The first result binds the reader before the callback interrupts NextResult.
                // Normal cleanup would encounter the second error and could mask the fatal one.
                using SqlCommand command = new(
                    "SELECT 1; RAISERROR ('Scalar cleanup sentinel', 10, 1); " +
                    "RAISERROR ('Secondary cleanup error', 16, 1);", connection);
                Exception actual = await Record.ExceptionAsync(async () =>
                {
                    if (async)
                    {
                        await command.ExecuteScalarAsync();
                    }
                    else
                    {
                        command.ExecuteScalar();
                    }
                });

                Assert.Equal(1, notifications);
                if (fatal)
                {
                    Assert.Same(failure, actual);
                    Assert.Equal("Broken", parser.GetType()
                        .GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(parser).ToString());
                }
                else
                {
                    SqlException sqlError = Assert.IsType<SqlException>(actual);
                    Assert.Contains("Secondary cleanup error", sqlError.Message);
                }

                connection.Close();
                using SqlConnection next = new(builder.ConnectionString);
                await OpenConnection(next, async);
                Assert.Equal(!fatal, original.IsInternalConnectionOf(next));
                if (fatal)
                {
                    Assert.NotEqual(originalId, next.ClientConnectionId);
                }
                using SqlCommand probe = new("SELECT 42", next);
                Assert.Equal(42, async ? await probe.ExecuteScalarAsync() : probe.ExecuteScalar());
            }
            finally
            {
                SqlConnection.ClearPool(connection);
            }
        }

        /// <summary>Opens a connection using the API variant under test.</summary>
        /// <param name="connection">Connection to open.</param>
        /// <param name="async">Whether to exercise asynchronous opening.</param>
        /// <returns>A task completing when the connection is open.</returns>
        private static async Task OpenConnection(SqlConnection connection, bool async)
        {
            if (async)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }
        }
    }
}
