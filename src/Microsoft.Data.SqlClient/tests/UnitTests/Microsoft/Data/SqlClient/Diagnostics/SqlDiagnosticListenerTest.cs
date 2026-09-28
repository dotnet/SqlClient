// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Diagnostics;

/// <summary>
/// Verifies that SqlDiagnosticListener carries the executing command's batch onto the payload it
/// writes for a completed execution.  This is the one construction site that cannot be reached end
/// to end from this project: the simulated TDS server has no RPC handler, so a SqlBatch always
/// faults before WriteCommandAfter is called.
/// </summary>
// Serializes execution with the simulated-server tests.  Required because constructing a
// SqlDiagnosticListener publishes it to the process-wide DiagnosticListener.AllListeners, which
// those tests subscribe to.
[Collection(SimulatedServerTestCollection.Name)]
public class SqlDiagnosticListenerTest
{
    /// <summary>
    /// Verifies that WriteCommandAfter reports the commands of the batch the command is executing
    /// on behalf of, and not null.
    /// </summary>
    [Fact]
    public void WriteCommandAfter_ForBatchCommand_CarriesBatchCommands()
    {
        IReadOnlyList<SqlBatchCommand> batchCommands = new[] { new SqlBatchCommand("SELECT 1;") };
        using SqlCommand command = new();
        command.BatchCommands = batchCommands;

        using SqlDiagnosticListener listener = new();
        using PayloadCollector collector = new(listener);

        listener.WriteCommandAfter(Guid.NewGuid(), command, transaction: null);

        SqlClientCommandAfter payload = collector.SinglePayload<SqlClientCommandAfter>(SqlClientCommandAfter.Name);
        Assert.Same(batchCommands, payload.BatchCommands);
    }

    /// <summary>
    /// Collects the payloads written to a single DiagnosticListener for the lifetime of the
    /// instance.
    /// </summary>
    private sealed class PayloadCollector : IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<KeyValuePair<string, object?>> _events = new();
        private readonly IDisposable _subscription;

        public PayloadCollector(SqlDiagnosticListener listener) => _subscription = listener.Subscribe(this);

        public T SinglePayload<T>(string eventName) =>
            Assert.Single(_events.Where(e => e.Key == eventName).Select(e => e.Value).OfType<T>());

        public void OnNext(KeyValuePair<string, object?> value) => _events.Add(value);

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose() => _subscription.Dispose();
    }
}
