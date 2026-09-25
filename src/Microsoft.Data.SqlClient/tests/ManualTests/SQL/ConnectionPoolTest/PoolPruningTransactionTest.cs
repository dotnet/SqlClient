// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies that pruning during the first physical open preserves transaction-affine reuse.
    /// </summary>
    [Trait("Set", "3")]
    public class PoolPruningTransactionTest
    {
        /// <summary>
        /// Forces the otherwise timing-dependent empty-pool window before inventory publication.
        /// Sequential opens in one scope must retain the same session and never promote to DTC.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task Prune_DuringFirstOpen_PreservesTransactionSession(bool channel, bool async)
        {
            using var poolVersion = new ConnectionPoolVersionScope(channel);
            var builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MinPoolSize = 0,
                MaxPoolSize = 1,
                Enlist = true,
                ApplicationName = nameof(PoolPruningTransactionTest) + Guid.NewGuid().ToString("N")
            };
            using var listener = new CreationPruningListener();
            using var scope = new TransactionScope(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            int? sessionId = null;
            listener.Arm();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using var connection = new SqlConnection(builder.ConnectionString);
                if (async)
                {
                    await connection.OpenAsync();
                }
                else
                {
                    connection.Open();
                }
                Assert.Null(listener.Failure);
                Assert.Equal(1, listener.PruningPasses);
                using var command = new SqlCommand("SELECT CAST(@@SPID AS int)", connection);
                int currentSessionId = (int)(async ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
                sessionId ??= currentSessionId;
                Assert.Equal(sessionId.Value, currentSessionId);
                Assert.Equal(Guid.Empty, Transaction.Current.TransactionInformation.DistributedIdentifier);
            }
            scope.Complete();
        }

        /// <summary>
        /// Injects one pruning pass at the physical-create trace boundary. Unlike unit tests,
        /// manual tests have no internal factory override; no payload is stored or logged.
        /// </summary>
        private sealed class CreationPruningListener : EventListener
        {
            private int _armed;
            internal int PruningPasses { get; private set; }
            internal Exception Failure { get; private set; }

            internal void Arm() => Volatile.Write(ref _armed, 1);

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
                {
                    EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (Volatile.Read(ref _armed) == 0 ||
                    eventData.Payload == null ||
                    !eventData.Payload.OfType<string>().Any(value => value.Contains("<prov.SqlConnectionFactory.CreatePooledConnection|")) ||
                    Interlocked.Exchange(ref _armed, 0) == 0)
                {
                    return;
                }

                try
                {
                    Type factoryType = typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient.SqlConnectionFactory", throwOnError: true);
                    object factory = factoryType.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    factoryType.GetMethod("RunPruningPass", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(factory, null);
                    PruningPasses++;
                }
                catch (Exception exception)
                {
                    // EventListener can swallow callback exceptions; surface them in the test.
                    Failure = exception;
                }
            }
        }
    }
}
