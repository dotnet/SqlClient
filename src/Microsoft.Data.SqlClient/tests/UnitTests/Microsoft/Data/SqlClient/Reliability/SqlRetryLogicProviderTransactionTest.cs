// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.Connection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Regression tests for GitHub issue #4309: configurable retry logic must never retry a
/// <see cref="SqlCommand"/> that was associated with a transaction when execution was attempted.
/// </summary>
/// <remarks>
/// <c>SqlRetryLogic.RetryCondition</c> checks the live transaction state, but it only runs after
/// the failure. A transient error that aborts the transaction (a deadlock victim, error 1205)
/// zombies the <see cref="SqlTransaction"/>, so <see cref="SqlCommand.Transaction"/> reports
/// <see langword="null"/>. The guard then passed and the command was re-executed outside of
/// its transaction. An abort does not clear <see cref="Transaction.Current"/> from an active
/// scope; the ambient tests separately simulate operation code clearing that state.
/// </remarks>
public class SqlRetryLogicProviderTransactionTest
{
    /// <summary>Deadlock victim; a member of the default transient error list.</summary>
    private const int DeadlockErrorNumber = 1205;

    #region Explicit SqlTransaction

    /// <summary>Prevents retrying a command after its explicit transaction is zombied by a failure.</summary>
    [Fact]
    public void Execute_TransactionClearedByFailure_IsNotRetried()
    {
        var (command, transaction) = CreateCommandWithLiveTransaction();
        var (provider, counter) = CreateProvider();

        Assert.Throws<SqlException>(() => provider.Execute<int>(command, () =>
        {
            // Model what SqlClient does when the transient error aborts the transaction.
            transaction.InternalTransaction.Completed(TransactionState.Aborted);
            Assert.Null(command.Transaction);
            throw CreateTransientSqlException();
        }));

        Assert.Equal(0, counter.Count);
    }

    /// <summary>Preserves the initial explicit transaction guard in the generic asynchronous retry loop.</summary>
    [Fact]
    public async Task ExecuteAsync_TransactionClearedByFailure_IsNotRetried()
    {
        var (command, transaction) = CreateCommandWithLiveTransaction();
        var (provider, counter) = CreateProvider();

        await Assert.ThrowsAsync<SqlException>(() => provider.ExecuteAsync<int>(command, () =>
        {
            transaction.InternalTransaction.Completed(TransactionState.Aborted);
            Assert.Null(command.Transaction);
            throw CreateTransientSqlException();
        }));

        Assert.Equal(0, counter.Count);
    }

    /// <summary>Preserves the initial explicit transaction guard in the non-generic asynchronous retry loop.</summary>
    [Fact]
    public async Task ExecuteAsyncNonGeneric_TransactionClearedByFailure_IsNotRetried()
    {
        var (command, transaction) = CreateCommandWithLiveTransaction();
        var (provider, counter) = CreateProvider();

        await Assert.ThrowsAsync<SqlException>(() => provider.ExecuteAsync(command, () =>
        {
            transaction.InternalTransaction.Completed(TransactionState.Aborted);
            Assert.Null(command.Transaction);
            throw CreateTransientSqlException();
        }));

        Assert.Equal(0, counter.Count);
    }

    #endregion

    #region Ambient TransactionScope

    /// <summary>Prevents retries when operation code clears the ambient transaction before throwing.</summary>
    [Fact]
    public void Execute_AmbientTransactionClearedByOperation_IsNotRetried()
    {
        SqlCommand command = new SqlCommand("UPDATE [Table1] SET [Value] = 2 WHERE [Id] = 1");
        var (provider, counter) = CreateProvider();

        using TransactionScope scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var ambient = Transaction.Current;
        Assert.NotNull(ambient);

        try
        {
            Assert.Throws<SqlException>(() => provider.Execute<int>(command, () =>
            {
                // Simulate operation code clearing the ambient state, not an abort doing so.
                Transaction.Current = null;
                throw CreateTransientSqlException();
            }));
        }
        finally
        {
            // Restore the ambient transaction so the scope can dispose cleanly.
            Transaction.Current = ambient;
        }

        Assert.Equal(0, counter.Count);
    }

    /// <summary>Preserves the initial ambient transaction guard when asynchronous operation code clears that state.</summary>
    [Fact]
    public async Task ExecuteAsync_AmbientTransactionClearedByOperation_IsNotRetried()
    {
        SqlCommand command = new SqlCommand("UPDATE [Table1] SET [Value] = 2 WHERE [Id] = 1");
        var (provider, counter) = CreateProvider();

        using TransactionScope scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var ambient = Transaction.Current;
        Assert.NotNull(ambient);

        try
        {
            await Assert.ThrowsAsync<SqlException>(() => provider.ExecuteAsync<int>(command, () =>
            {
                Transaction.Current = null;
                throw CreateTransientSqlException();
            }));
        }
        finally
        {
            Transaction.Current = ambient;
        }

        Assert.Equal(0, counter.Count);
    }

    #endregion

    #region No transaction (must still retry)

    /// <summary>Allows all configured attempts when a command starts without a transaction.</summary>
    [Fact]
    public void Execute_CommandWithoutTransaction_IsRetried()
    {
        SqlCommand command = new SqlCommand("SELECT 1");
        var (provider, counter) = CreateProvider();
        int attempts = 0;

        Assert.Throws<AggregateException>(() => provider.Execute<int>(command, () =>
        {
            attempts++;
            throw CreateTransientSqlException();
        }));

        Assert.Equal(3, attempts);
        Assert.Equal(2, counter.Count);
    }

    /// <summary>Allows all configured attempts in the generic asynchronous loop without a transaction.</summary>
    [Fact]
    public async Task ExecuteAsync_CommandWithoutTransaction_IsRetried()
    {
        SqlCommand command = new SqlCommand("SELECT 1");
        var (provider, counter) = CreateProvider();
        int attempts = 0;

        await Assert.ThrowsAsync<AggregateException>(() => provider.ExecuteAsync<int>(command, () =>
        {
            attempts++;
            throw CreateTransientSqlException();
        }));

        Assert.Equal(3, attempts);
        Assert.Equal(2, counter.Count);
    }

    /// <summary>Allows all configured attempts in the separate non-generic asynchronous loop without a transaction.</summary>
    [Fact]
    public async Task ExecuteAsyncNonGeneric_CommandWithoutTransaction_IsRetried()
    {
        using SqlCommand command = new SqlCommand("SELECT 1");
        var (provider, counter) = CreateProvider();
        int attempts = 0;

        await Assert.ThrowsAsync<AggregateException>(() => provider.ExecuteAsync(command, () =>
        {
            attempts++;
            return Task.FromException(CreateTransientSqlException());
        }));

        Assert.Equal(3, attempts);
        Assert.Equal(2, counter.Count);
    }

    #endregion

    #region Command reuse

    /// <summary>Limits the transaction snapshot to one execution so command reuse can retry outside a transaction.</summary>
    [Fact]
    public void Execute_ReusedCommandWithoutTransaction_IsRetriedAfterEarlierTransactionalExecution()
    {
        var (command, transaction) = CreateCommandWithLiveTransaction();
        var (provider, counter) = CreateProvider();

        Assert.Throws<SqlException>(() => provider.Execute<int>(command, () =>
        {
            transaction.InternalTransaction.Completed(TransactionState.Aborted);
            throw CreateTransientSqlException();
        }));
        Assert.Equal(0, counter.Count);

        // The same command object is now used without a transaction; it must be retriable again.
        command.Transaction = null;
        int attempts = 0;

        Assert.Throws<AggregateException>(() => provider.Execute<int>(command, () =>
        {
            attempts++;
            throw CreateTransientSqlException();
        }));

        Assert.Equal(3, attempts);
        Assert.Equal(2, counter.Count);
    }

    /// <summary>Allows asynchronous command reuse to retry after a previous transactional execution failed.</summary>
    [Fact]
    public async Task ExecuteAsync_ReusedCommandWithoutTransaction_IsRetriedAfterEarlierTransactionalExecution()
    {
        var (command, transaction) = CreateCommandWithLiveTransaction();
        var (provider, counter) = CreateProvider();

        await Assert.ThrowsAsync<SqlException>(() => provider.ExecuteAsync<int>(command, () =>
        {
            transaction.InternalTransaction.Completed(TransactionState.Aborted);
            throw CreateTransientSqlException();
        }));
        Assert.Equal(0, counter.Count);

        command.Transaction = null;
        int attempts = 0;

        await Assert.ThrowsAsync<AggregateException>(() => provider.ExecuteAsync<int>(command, () =>
        {
            attempts++;
            throw CreateTransientSqlException();
        }));

        Assert.Equal(3, attempts);
        Assert.Equal(2, counter.Count);
    }

    #endregion

    #region Helpers

    /// <summary>Counts how many times the provider raised its <c>Retrying</c> event.</summary>
    private sealed class RetryCounter
    {
        public int Count { get; private set; }

        /// <summary>Records one retry notification for assertions on the number of scheduled retries.</summary>
        public void Increment() => Count++;
    }

    /// <summary>Creates a three-attempt provider with no retry delay and attaches an event counter.</summary>
    /// <returns>The fixed-interval provider and the counter updated by its retry notifications.</returns>
    private static (SqlRetryLogicBaseProvider Provider, RetryCounter Counter) CreateProvider()
    {
        SqlRetryLogicBaseProvider provider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(
            new SqlRetryLogicOption
            {
                NumberOfTries = 3,
                DeltaTime = TimeSpan.Zero,
                MinTimeInterval = TimeSpan.Zero,
                MaxTimeInterval = TimeSpan.Zero
            });

        RetryCounter counter = new RetryCounter();
        provider.Retrying += (sender, args) => counter.Increment();

        return (provider, counter);
    }

    /// <summary>Builds a deadlock exception recognized by the default transient-error predicate without a server.</summary>
    /// <returns>A SQL exception containing the deadlock-victim error number.</returns>
    private static SqlException CreateTransientSqlException()
    {
        SqlErrorCollection errors = new SqlErrorCollection();
        errors.Add(new SqlError(
            infoNumber: DeadlockErrorNumber,
            errorState: 0,
            errorClass: 0,
            server: "TestServer",
            errorMessage: "Transaction was deadlocked and has been chosen as the deadlock victim.",
            procedure: string.Empty,
            lineNumber: 0));

        return SqlException.CreateException(errors, string.Empty);
    }

    /// <summary>
    /// Builds a <see cref="SqlCommand"/> carrying a non-zombied <see cref="SqlTransaction"/>.
    /// Uses the internal transaction constructor with a connection stub to avoid opening a
    /// server connection while preserving the transaction's normal completion behavior.
    /// </summary>
    /// <returns>The command and its transaction, which the test can complete during execution.</returns>
    private static (SqlCommand Command, SqlTransaction Transaction) CreateCommandWithLiveTransaction()
    {
        SqlTransaction transaction = new SqlTransaction(
            CreateConnectionStub(),
            new SqlConnection(),
            System.Data.IsolationLevel.ReadCommitted,
            internalTransaction: null);

        // Sanity check: the transaction has to look alive before execution starts.
        Assert.NotNull(transaction.Connection);

        SqlCommand command = new SqlCommand("UPDATE [Table1] SET [Value] = 2 WHERE [Id] = 1")
        {
            Transaction = transaction
        };
        Assert.NotNull(command.Transaction);

        return (command, transaction);
    }

    /// <summary>
    /// Allocates a connection stub without running its constructor, which opens a server
    /// connection. Only its object ID and null-safe transaction disconnection are used.
    /// </summary>
    /// <returns>A connection stub for constructing and completing a transaction without network I/O.</returns>
    private static SqlConnectionInternal CreateConnectionStub() =>
#if NET
        (SqlConnectionInternal)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SqlConnectionInternal));
#else
        (SqlConnectionInternal)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SqlConnectionInternal));
#endif

    #endregion
}
