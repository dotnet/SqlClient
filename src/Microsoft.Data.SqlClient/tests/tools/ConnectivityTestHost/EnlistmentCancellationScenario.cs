// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient;

namespace ConnectivityTestHost;

/// <summary>Orders actual promotable enlistment against cancelled-scope disposal without adding driver synchronization hooks.</summary>
internal static class EnlistmentCancellationScenario
{
    /// <summary>Cancels at Login7, before enlistment can acquire either transaction lock.</summary>
    internal static void RunBeforeEnlistment(int port, bool enlist)
    {
        using var cancellation = new CancellationTokenSource();
        using var started = new ManualResetEventSlim();
        Task caller = Task.Run(async () =>
        {
            using var connection = new SqlConnection(ConnectionString(port, enlist));
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled))
            {
                Task open = connection.OpenAsync(cancellation.Token);
                started.Set();
                try
                {
                    await open.ConfigureAwait(false);
                    throw new InvalidOperationException("The held open completed without cancellation.");
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    Console.WriteLine("PUBLIC_CANCELLED");
                }
            }
            Console.WriteLine("SCOPE_COMPLETE");
        });
        if (!started.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("Ambient caller did not obtain an OpenAsync task.");
        }
        Console.WriteLine("CALL_RETURNED");
        if (Console.ReadLine() != "CANCEL")
        {
            throw new InvalidOperationException("Parent did not cancel the pre-enlistment open.");
        }
        cancellation.Cancel();
        if (!caller.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("Pre-enlistment cancellation did not finish scope disposal.");
        }
        if (Console.ReadLine() != "FINISH")
        {
            throw new InvalidOperationException("Parent did not release the held login.");
        }
        TransportAttemptScenario.WaitForPhysicalCompletion();
        Console.WriteLine("SCOPE_AND_PHYSICAL_COMPLETE");
    }

    internal static void Run(int port, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        using var started = new ManualResetEventSlim();
        Transaction transaction = null;
        using var trace = new EnlistmentTrace(() => transaction);
        Task caller = Task.Run(async () =>
        {
            using var connection = new SqlConnection(ConnectionString(port, true));
            using var scope = new TransactionScope(TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(2) },
                TransactionScopeAsyncFlowOption.Enabled);
            transaction = Transaction.Current;
            Task open = connection.OpenAsync(cancellation.Token);
            started.Set();
            try
            {
                await open.ConfigureAwait(false);
                scope.Complete();
            }
            catch (OperationCanceledException) when (cancel && cancellation.IsCancellationRequested)
            {
                Console.WriteLine("PUBLIC_CANCELLED");
            }
        });
        if (!started.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("Ambient caller did not obtain an OpenAsync task.");
        }
        Console.WriteLine("CALL_RETURNED");
        if (!trace.Enlisted.Wait(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("Actual BEGIN did not complete into the post-enlistment trace gate.");
        }
        if (trace.Failure != null)
        {
            throw new InvalidOperationException("The source-specific enlistment lock probe is unavailable.", trace.Failure);
        }
        if (!trace.OpenStack)
        {
            throw new InvalidOperationException("The gate did not observe the actual physical enlistment stack.");
        }
        Console.WriteLine($"ENLISTED_PARSER_HELD {trace.ParserOwned}");
        if (Console.ReadLine() != (cancel ? "CANCEL" : "FINISH"))
        {
            throw new InvalidOperationException("Parent did not issue the selected enlistment command.");
        }
        if (cancel)
        {
            cancellation.Cancel();
            if (!SpinWait.SpinUntil(() => caller.IsCompleted || (trace.RollbackEntered.IsSet &&
                    (trace.RollbackThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0),
                TimeSpan.FromSeconds(15)))
            {
                throw new TimeoutException("The cancelled scope did not complete or reach its actual rollback wait.");
            }
            if (!caller.IsCompleted && !trace.RollbackStack)
            {
                throw new InvalidOperationException("Rollback did not run from the cancelled caller's actual TransactionScope disposal.");
            }
            bool rollbackOwnsConnection = trace.ConnectionField.GetValue(trace.Promoter) == null &&
                IsMonitorOwned(trace.PhysicalConnection);
            Console.WriteLine(rollbackOwnsConnection ? "ROLLBACK_OWNS_CONNECTION" : "ROLLBACK_RELEASED_CONNECTION");
            if (Console.ReadLine() != "FINISH")
            {
                throw new InvalidOperationException("Parent did not release the login-owned parser lock path.");
            }
        }
        trace.Release.Set();
        if (!caller.Wait(TimeSpan.FromSeconds(20)))
        {
            if (trace.RollbackThread != null &&
                IsMonitorOwned(trace.PhysicalConnection) && IsMonitorOwned(trace.ParserSemaphore) &&
                (trace.OpenThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0 &&
                (trace.RollbackThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0)
            {
                Console.WriteLine("LOCK_CYCLE_STALLED");
            }
            throw new TimeoutException("The cancelled caller's scope disposal did not finish after releasing physical enlistment.");
        }
        TransportAttemptScenario.WaitForPhysicalCompletion();
        Console.WriteLine("SCOPE_AND_PHYSICAL_COMPLETE");
        if (trace.GateTimedOut)
        {
            throw new TimeoutException("The controller missed the enlistment trace release.");
        }
    }

    private static string ConnectionString(int port, bool enlist) => new SqlConnectionStringBuilder
    {
        DataSource = $"127.0.0.1,{port}",
        Pooling = false,
        Enlist = enlist,
        Encrypt = SqlConnectionEncryptOption.Optional,
        ConnectRetryCount = 0,
        ConnectTimeout = 60
    }.ConnectionString;

    private static bool IsMonitorOwned(object connection)
    {
        if (!Monitor.TryEnter(connection))
        {
            return true;
        }
        Monitor.Exit(connection);
        return false;
    }

    /// <summary>Uses existing post-BEGIN and rollback traces. No trace entry alone is accepted as proof of lock ownership.</summary>
    private sealed class EnlistmentTrace : EventListener
    {
        private readonly Func<Transaction> _transaction;
        internal readonly ManualResetEventSlim Enlisted = new();
        internal readonly ManualResetEventSlim RollbackEntered = new();
        internal readonly ManualResetEventSlim Release = new();
        internal object Promoter;
        internal object PhysicalConnection;
        internal object ParserSemaphore;
        internal Thread OpenThread;
        internal Thread RollbackThread;
        internal FieldInfo ConnectionField;
        internal bool ParserOwned;
        internal bool OpenStack;
        internal bool RollbackStack;
        internal bool GateTimedOut;
        internal Exception Failure;

        internal EnlistmentTrace(Func<Transaction> transaction) => _transaction = transaction;

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)(128 | 2));
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs data)
        {
            if (data.Payload?.FirstOrDefault() is not string message)
            {
                return;
            }
            if (message.StartsWith("SqlDelegatedTransaction.Rollback ", StringComparison.Ordinal))
            {
                RollbackStack = new StackTrace().GetFrames().Any(frame =>
                    frame.GetMethod().DeclaringType == typeof(TransactionScope) && frame.GetMethod().Name == "Dispose");
                RollbackThread = Thread.CurrentThread;
                RollbackEntered.Set();
            }
            if (message.StartsWith("SqlInternalConnection.EnlistNonNull ", StringComparison.Ordinal) &&
                message.Contains("delegated to transaction"))
            {
                try
                {
                    object internalTransaction = Field(typeof(Transaction), "_internalTransaction", "internalTransaction").GetValue(_transaction());
                    Promoter = Field(internalTransaction.GetType(), "_promoter", "promoter").GetValue(internalTransaction)
                        ?? throw new InvalidOperationException("The ambient transaction has no promotable enlistment.");
                    if (Promoter.GetType().FullName != "Microsoft.Data.SqlClient.SqlDelegatedTransaction")
                    {
                        throw new InvalidOperationException("The ambient transaction promoter is not SqlClient.");
                    }
                    ConnectionField = Field(Promoter.GetType(), "_connection");
                    PhysicalConnection = ConnectionField.GetValue(Promoter)
                        ?? throw new InvalidOperationException("Promotable enlistment has no physical connection.");
                    object parserLock = Field(PhysicalConnection.GetType(), "_parserLock").GetValue(PhysicalConnection);
                    ParserSemaphore = Field(parserLock.GetType(), "_semaphore").GetValue(parserLock);
                    ParserOwned = Monitor.IsEntered(ParserSemaphore);
                    OpenThread = Thread.CurrentThread;
                    OpenStack = new StackTrace().GetFrames().Any(frame => frame.GetMethod().Name == "CompleteLogin");
                }
                catch (Exception error) when (error is MissingFieldException || error is InvalidOperationException)
                {
                    Failure = error;
                }
                Enlisted.Set();
                GateTimedOut = !Release.Wait(TimeSpan.FromSeconds(45));
            }
        }

        private static FieldInfo Field(Type type, params string[] names)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                foreach (string name in names)
                {
                    FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (field != null)
                    {
                        return field;
                    }
                }
            }
            throw new MissingFieldException(type.FullName, string.Join("/", names));
        }
    }
}
