// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies driver-owned cleanup reports both failures when operation and cleanup fail,
    /// and preserves the existing exception when only one fails.
    /// </summary>
    [Trait("Set", "3")]
    public sealed class ScalarFailureCleanupTest
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>
        /// Uncatchable operation failures skip reader cleanup. Uncatchable cleanup failures
        /// propagate directly whether or not a catchable SQL error preceded them.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, 0)]
        [InlineData(false, 1)]
        [InlineData(false, 2)]
        [InlineData(true, 0)]
        [InlineData(true, 1)]
        [InlineData(true, 2)]
        public async Task ScalarFailure_PreservesOriginalDuringCleanup(bool async, int failureKind)
        {
            var builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = false,
                Enlist = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            await connection.OpenAsync();
            string query = "SELECT 1; RAISERROR ('Configure cleanup', 10, 1); " +
                (failureKind == 1 ? "RAISERROR ('Original SQL failure', 16, 1);" : "SELECT 2;");
            using SqlCommand command = new(query, connection);
            var original = new OutOfMemoryException("Original fatal failure");
            var secondary = new OutOfMemoryException("Secondary cleanup failure");
            SqlDataReader reader = null;
            int cleanupCalls = 0;
            SqlInfoMessageEventHandler onMessage = (_, args) =>
            {
                if (args.Message.Contains("Cleanup sentinel"))
                {
                    cleanupCalls++;
                    throw secondary;
                }

                object inner = connection.GetInternalConnection();
                reader = (SqlDataReader)inner.GetType()
                    .GetMethod("FindLiveReader", InstanceNonPublic).Invoke(inner, new object[] { null });
                Assert.NotNull(reader);
                typeof(SqlDataReader).GetField("_resetOptionsString", InstanceNonPublic)
                    .SetValue(reader, "RAISERROR ('Cleanup sentinel', 10, 1);");
                if (failureKind == 2)
                {
                    throw original;
                }
            };
            connection.InfoMessage += onMessage;
            try
            {
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
                if (failureKind == 2)
                {
                    Assert.Same(original, actual);
                    Assert.Equal(0, cleanupCalls);
                    Assert.False(reader.IsClosed);
                    Assert.Contains("OnInfoMessage", original.StackTrace);
                }
                else
                {
                    Assert.Same(secondary, actual);
                    Assert.True(cleanupCalls > 0, "Expected cleanup to encounter the secondary failure.");
                }
            }
            finally
            {
                connection.InfoMessage -= onMessage;
                if (reader != null)
                {
                    // Remove the injected fault before test-owned disposal.
                    typeof(SqlDataReader).GetField("_resetOptionsString", InstanceNonPublic)
                        .SetValue(reader, null);
                    reader.Dispose();
                }
            }
        }

        /// <summary>
        /// Reader Close aggregates catchable drain and teardown failures, propagates a lone
        /// teardown failure, and skips teardown when the drain failure is uncatchable.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ReaderClose_PreservesOriginalDuringCleanup(int failureKind)
        {
            var builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = false,
                Enlist = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();
            using SqlCommand command = new("SELECT 1", connection);
            using SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection);
            var original = new OutOfMemoryException("Original fatal failure");
            var secondary = new InvalidOperationException("Secondary close failure");
            SqlInfoMessageEventHandler onMessage = (_, _) => throw original;
            if (failureKind == 2)
            {
                connection.InfoMessage += onMessage;
            }
            int closeCalls = 0;
            StateChangeEventHandler onClose = (_, args) =>
            {
                if (args.CurrentState == ConnectionState.Closed)
                {
                    closeCalls++;
                    throw secondary;
                }
            };
            connection.StateChange += onClose;
            if (failureKind != 0)
            {
                typeof(SqlDataReader).GetField("_resetOptionsString", InstanceNonPublic).SetValue(reader,
                    failureKind == 2 ? "RAISERROR ('Original fatal failure', 10, 1);" :
                        "RAISERROR ('Original SQL failure', 16, 1);");
            }
            try
            {
                Exception actual = Record.Exception(() => reader.Close());
                if (failureKind == 0)
                {
                    Assert.Same(secondary, actual);
                }
                else if (failureKind == 2)
                {
                    Assert.Same(original, actual);
                    Assert.Contains("OnInfoMessage", original.StackTrace);
                }
                else
                {
                    var aggregate = Assert.IsType<AggregateException>(
                        Assert.IsType<SqlException>(actual).InnerException);
                    Assert.Equal(2, aggregate.InnerExceptions.Count);
                    Assert.Contains("Original SQL failure",
                        Assert.IsType<SqlException>(aggregate.InnerExceptions[0]).Message);
                    Assert.Same(secondary, aggregate.InnerExceptions[1]);
                }
                Assert.Equal(failureKind == 2 ? 0 : 1, closeCalls);
                Assert.Equal(failureKind != 2, reader.IsClosed);
                Assert.Equal(failureKind == 2 ? ConnectionState.Open : ConnectionState.Closed, connection.State);
            }
            finally
            {
                connection.InfoMessage -= onMessage;
                connection.StateChange -= onClose;
                typeof(SqlDataReader).GetField("_resetOptionsString", InstanceNonPublic).SetValue(reader, null);
            }
        }
    }
}
