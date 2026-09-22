// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Transactions;
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
/// <see langword="null"/> and <see cref="Transaction.Current"/> is likewise cleared. The guard
/// then passed and the command was re-executed outside of any transaction.
/// </remarks>
public class SqlRetryLogicProviderTransactionTest
{
    /// <summary>Deadlock victim; a member of the default transient error list.</summary>
    private const int DeadlockErrorNumber = 1205;

    private static readonly FieldInfo s_transactionConnectionField =
        typeof(SqlTransaction)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(SqlConnection));

    private static readonly FieldInfo s_transactionInternalField =
        typeof(SqlTransaction)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(SqlInternalTransaction));

    #region Explicit SqlTransaction

    [Fact]
    public void Execute_TransactionClearedByFailure_IsNotRetried()
    {
        SqlCommand command = CreateCommandWithLiveTransaction(out SqlTransaction transaction);
        SqlRetryLogicBaseProvider provider = CreateProvider(out RetryCounter counter);

        Assert.Throws<SqlException>(() => provider.Execute<int>(command, () =>
        {
            // Model what SqlClient does when the transient error aborts the transaction.
            Zombie(transaction);
            Assert.Null(command.Transaction);
            throw CreateTransientSqlException();
        }));

        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task ExecuteAsync_TransactionClearedByFailure_IsNotRetried()
    {
        SqlCommand command = CreateCommandWithLiveTransaction(out SqlTransaction transaction);
        SqlRetryLogicBaseProvider provider = CreateProvider(out RetryCounter counter);

        await Assert.ThrowsAsync<SqlException>(() => provider.ExecuteAsync<int>(command, () =>
        {
            Zombie(transaction);
            Assert.Null(command.Transaction);
            throw CreateTransientSqlException();
        }));

        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task ExecuteAsyncNonGeneric_TransactionClearedByFailure_IsNotRetried()
    {
        SqlCommand command = CreateCommandWithLiveTransaction(out SqlTransaction transaction);
        SqlRetryLogicBaseProvider provider = CreateProvider(out RetryCounter counter);

        await Assert.ThrowsAsync<SqlException>(() => provider.ExecuteAsync(command, () =>
        {
            Zombie(transaction);
            Assert.Null(command.Transaction);
            throw CreateTransientSqlException();
        }));

        Assert.Equal(0, counter.Count);
    }

    #endregion

    #region Ambient TransactionScope

    [Fact]
    public void Execute_AmbientTransactionClearedByFailure_IsNotRetried()
    {
        SqlCommand command = new SqlCommand("UPDATE [Table1] SET [Value] = 2 WHERE [Id] = 1");
        SqlRetryLogicBaseProvider provider = CreateProvider(out RetryCounter counter);

        using TransactionScope scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var ambient = Transaction.Current;
        Assert.NotNull(ambient);

        try
        {
            Assert.Throws<SqlException>(() => provider.Execute<int>(command, () =>
            {
                // The aborted transaction is no longer the ambient one.
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

    [Fact]
    public async Task ExecuteAsync_AmbientTransactionClearedByFailure_IsNotRetried()
    {
        SqlCommand command = new SqlCommand("UPDATE [Table1] SET [Value] = 2 WHERE [Id] = 1");
        SqlRetryLogicBaseProvider provider = CreateProvider(out RetryCounter counter);

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

    [Fact]
    public void Execute_CommandWithoutTransaction_IsRetried()
    {
        SqlCommand command = new SqlCommand("SELECT 1");
        SqlRetryLogicBaseProvider provider = CreateProvider(out RetryCounter counter);
        int attempts = 0;

        Assert.Throws<AggregateException>(() => provider.Execute<int>(command, () =>
        {
            attempts++;
            throw CreateTransientSqlException();
        }));

        Assert.Equal(3, attempts);
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task ExecuteAsync_CommandWithoutTransaction_IsRetried()
    {
        SqlCommand command = new SqlCommand("SELECT 1");
        SqlRetryLogicBaseProvider provider = CreateProvider(out RetryCounter counter);
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

    #region Command reuse

    [Fact]
    public void Execute_ReusedCommandWithoutTransaction_IsRetriedAfterEarlierTransactionalExecution()
    {
        SqlCommand command = CreateCommandWithLiveTransaction(out SqlTransaction transaction);
        SqlRetryLogicBaseProvider provider = CreateProvider(out RetryCounter counter);

        Assert.Throws<SqlException>(() => provider.Execute<int>(command, () =>
        {
            Zombie(transaction);
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

    [Fact]
    public async Task ExecuteAsync_ReusedCommandWithoutTransaction_IsRetriedAfterEarlierTransactionalExecution()
    {
        SqlCommand command = CreateCommandWithLiveTransaction(out SqlTransaction transaction);
        SqlRetryLogicBaseProvider provider = CreateProvider(out RetryCounter counter);

        await Assert.ThrowsAsync<SqlException>(() => provider.ExecuteAsync<int>(command, () =>
        {
            Zombie(transaction);
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

        public void Increment() => Count++;
    }

    private static SqlRetryLogicBaseProvider CreateProvider(out RetryCounter counter)
    {
        SqlRetryLogicBaseProvider provider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(
            new SqlRetryLogicOption
            {
                NumberOfTries = 3,
                DeltaTime = TimeSpan.Zero,
                MinTimeInterval = TimeSpan.Zero,
                MaxTimeInterval = TimeSpan.Zero
            });

        RetryCounter localCounter = new RetryCounter();
        provider.Retrying += (sender, args) => localCounter.Increment();
        counter = localCounter;

        return provider;
    }

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
    /// A real transaction requires a live server connection, so the object graph is assembled
    /// directly; only the state the retry guard inspects has to be faithful.
    /// </summary>
    private static SqlCommand CreateCommandWithLiveTransaction(out SqlTransaction transaction)
    {
        transaction = (SqlTransaction)CreateUninitialized(typeof(SqlTransaction));
        s_transactionConnectionField.SetValue(transaction, new SqlConnection());
        s_transactionInternalField.SetValue(
            transaction,
            (SqlInternalTransaction)CreateUninitialized(typeof(SqlInternalTransaction)));

        // Sanity check: the transaction has to look alive before execution starts.
        Assert.NotNull(transaction.Connection);

        SqlCommand command = new SqlCommand("UPDATE [Table1] SET [Value] = 2 WHERE [Id] = 1")
        {
            Transaction = transaction
        };
        Assert.NotNull(command.Transaction);

        return command;
    }

    /// <summary>
    /// Zombies the transaction the way an aborted transaction does, which makes
    /// <see cref="SqlCommand.Transaction"/> report <see langword="null"/>.
    /// </summary>
    private static void Zombie(SqlTransaction transaction) =>
        s_transactionInternalField.SetValue(transaction, null);

    private static object CreateUninitialized(Type type) =>
#if NET
        System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
#else
        System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#endif

    #endregion
}
