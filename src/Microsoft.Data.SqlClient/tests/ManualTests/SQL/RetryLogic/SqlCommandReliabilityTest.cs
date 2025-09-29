// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlCommandReliabilityTest : IDisposable
    {
        private const int ConcurrentParallelExecutions = 3;
        private const string UnexpectedSqlConnectionExceptionMessage = "An unexpected SQL Connection Error occured and is not part of the test.";
        private readonly string _exceedErrMsgPattern = RetryLogicTestHelper.s_exceedErrMsgPattern;
        private readonly string _cancelErrMsgPattern = RetryLogicTestHelper.s_cancelErrMsgPattern;
        private readonly int _originalWorkerThreads;
        private readonly int _originalCompletionPortThreads;
        private bool _disposed = false;

        public SqlCommandReliabilityTest()
        {
            ThreadPool.GetMinThreads(out _originalWorkerThreads, out _originalCompletionPortThreads);
            ThreadPool.SetMinThreads(ConcurrentParallelExecutions * 2, ConcurrentParallelExecutions * 2); // Multiply by 2 for possible additional threads needed in functions
        }

        // Dispose Pattern
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                ThreadPool.SetMinThreads(_originalWorkerThreads, _originalCompletionPortThreads);
                _disposed = true;
            }
        }

        #region Sync

        public static TheoryData<string, SqlRetryLogicBaseProvider> RetryExecuteFail_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromMilliseconds(100));

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(RetryExecuteFail_Data), DisableDiscoveryEnumeration = true)]
        public void RetryExecuteFail(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries + 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            string query = "SELECT bad command";

            using (SqlConnection cnn = new SqlConnection(cnnString))
            using (SqlCommand cmd = CreateCommand(cnn, provider, cancelAfterRetries))
            {
                cmd.CommandText = query;
                var ex = Assert.Throws<AggregateException>(() => cmd.ExecuteScalar());
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteReader());
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteReader(CommandBehavior.Default));
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                if (!DataTestUtility.IsAzureSynapse)
                {
                    cmd.CommandText = query + " FOR XML AUTO";
                    ex = Assert.Throws<AggregateException>(() => cmd.ExecuteXmlReader());
                    Assert.Equal(numberOfTries, currentRetries + 1);
                    Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                    Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);
                }
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> RetryExecuteCancel_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromMilliseconds(100));

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(RetryExecuteCancel_Data), DisableDiscoveryEnumeration = true)]
        public void RetryExecuteCancel(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries - 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            string query = "SELECT bad command";

            using (SqlConnection cnn = new SqlConnection(cnnString))
            using (SqlCommand cmd = CreateCommand(cnn, provider, cancelAfterRetries))
            {
                cmd.CommandText = query;
                var ex = Assert.Throws<AggregateException>(() => cmd.ExecuteScalar());
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteReader());
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteReader(CommandBehavior.Default));
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                if (DataTestUtility.IsNotAzureSynapse())
                {
                    cmd.CommandText = query + " FOR XML AUTO";
                    ex = Assert.Throws<AggregateException>(() => cmd.ExecuteXmlReader());
                    Assert.Equal(cancelAfterRetries, currentRetries);
                    Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                    Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);
                }
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> RetryExecuteWithTransactionScope_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 5,
                maxInterval: TimeSpan.FromMilliseconds(100));

        [ActiveIssue("14588")]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(RetryExecuteWithTransactionScope_Data), DisableDiscoveryEnumeration = true)]
        public void RetryExecuteWithTransScope(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries + 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            string query = "SELECT bad command";

            using (TransactionScope transScope = new TransactionScope())
            using (SqlConnection cnn = new SqlConnection(cnnString))
            using (SqlCommand cmd = CreateCommand(cnn, provider, cancelAfterRetries))
            {
                cmd.CommandText = query;
                Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
                Assert.Equal(0, currentRetries);

                Assert.Throws<SqlException>(() => cmd.ExecuteReader());
                Assert.Equal(0, currentRetries);

                Assert.Throws<SqlException>(() => cmd.ExecuteReader(CommandBehavior.Default));
                Assert.Equal(0, currentRetries);

                Assert.Throws<SqlException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(0, currentRetries);

                cmd.CommandText = query + " FOR XML AUTO";
                Assert.Throws<SqlException>(() => cmd.ExecuteXmlReader());
                Assert.Equal(0, currentRetries);

                transScope.Complete();
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> RetryExecuteWithTransaction_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 5,
                maxInterval: TimeSpan.FromMilliseconds(100));

        // Synapse: 111214;An attempt to complete a transaction has failed. No corresponding transaction found.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(RetryExecuteWithTransaction_Data), DisableDiscoveryEnumeration = true)]
        public void RetryExecuteWithTransaction(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries + 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            string query = "SELECT bad command";

            using (SqlConnection cnn = new SqlConnection(cnnString))
            using (SqlCommand cmd = CreateCommand(cnn, provider, cancelAfterRetries))
            using (SqlTransaction tran = cnn.BeginTransaction())
            {
                cmd.CommandText = query;
                cmd.Transaction = tran;
                Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
                Assert.Equal(0, currentRetries);

                Assert.Throws<SqlException>(() => cmd.ExecuteReader());
                Assert.Equal(0, currentRetries);

                Assert.Throws<SqlException>(() => cmd.ExecuteReader(CommandBehavior.Default));
                Assert.Equal(0, currentRetries);

                Assert.Throws<SqlException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(0, currentRetries);

                cmd.CommandText = query + " FOR XML AUTO";
                Assert.Throws<SqlException>(() => cmd.ExecuteXmlReader());
                Assert.Equal(0, currentRetries);

                tran.Commit();
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> RetryExecuteUnauthorizedSqlStatementDml_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromMilliseconds(100),
                transientErrorCodes: [102, 207, 2812],
                unauthorizedStatements: FilterSqlStatements.DML);

        // Synapse: Msg 103010, Level 16, State 1, Line 1 | Parse error at line: 1, column: 1: Incorrect syntax near 'command'
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(RetryExecuteUnauthorizedSqlStatementDml_Data), DisableDiscoveryEnumeration = true)]
        public void RetryExecuteUnauthorizedSqlStatementDml(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries + 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;

            using (SqlConnection cnn = new SqlConnection(cnnString))
            using (SqlCommand cmd = CreateCommand(cnn, provider, cancelAfterRetries))
            {
                // Unauthorized commands
                cmd.CommandText = "UPDATE bad command";
                Assert.Throws<SqlException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(0, currentRetries);

                cmd.CommandText = "INSERT INTO bad command";
                Assert.Throws<SqlException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(0, currentRetries);

                cmd.CommandText = "DELETE FROM bad command";
                Assert.Throws<SqlException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(0, currentRetries);

                cmd.CommandText = "TRUNCATE TABLE bad command";
                Assert.Throws<SqlException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(0, currentRetries);

                // Authorized commands
                cmd.CommandText = "SELECT bad command";
                Assert.Throws<AggregateException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(numberOfTries, currentRetries + 1);

                cmd.CommandText = "ALTER TABLE bad command";
                Assert.Throws<AggregateException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(numberOfTries, currentRetries + 1);

                cmd.CommandText = "EXEC bad command";
                Assert.Throws<AggregateException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(numberOfTries, currentRetries + 1);

                cmd.CommandText = "CREATE TABLE bad command";
                Assert.Throws<AggregateException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(numberOfTries, currentRetries + 1);

                cmd.CommandText = "DROP TABLE bad command";
                Assert.Throws<AggregateException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(numberOfTries, currentRetries + 1);
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> DropDatabaseWithActiveConnection_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 5,
                maxInterval: TimeSpan.FromSeconds(2),
                deltaTime: TimeSpan.FromMilliseconds(500),
                transientErrorCodes: RetryLogicTestHelper.GetDefaultTransientErrorCodes(3702));

        [ActiveIssue("14325")]
        // avoid creating a new database in Azure
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(DropDatabaseWithActiveConnection_Data), DisableDiscoveryEnumeration = true)]
        public void DropDatabaseWithActiveConnection(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int currentRetries = 0;
            string database = DataTestUtility.GetLongName($"RetryLogic_{provider.RetryLogic.RetryIntervalEnumerator.GetType().Name}", false);
            var builder = new SqlConnectionStringBuilder(cnnString)
            {
                InitialCatalog = database,
                ConnectTimeout = 1
            };

            using (var cnn3 = new SqlConnection(cnnString))
            using (var cnn2 = new SqlConnection(builder.ConnectionString))
            using (var cnn1 = new SqlConnection(new SqlConnectionStringBuilder(cnnString) { ConnectTimeout = 120 }.ConnectionString))
            using (var cmd = new SqlCommand())
            {
                cnn1.Open();
                cmd.Connection = cnn1;
                // Create the database and wait until it is finalized.
                cmd.CommandText = $"CREATE DATABASE [{database}]; \nWHILE(NOT EXISTS(SELECT 1 FROM sys.databases WHERE name = '{database}')) \nWAITFOR DELAY '00:00:10' ";
                cmd.ExecuteNonQuery();

                try
                {
                    // open an active connection to the database to raise error 3702 if someone drops it.
                    cnn2.Open();
                    cnn3.Open();

                    // kill the active connection to the database after the first faliure.
                    provider.Retrying += (s, e) =>
                    {
                        currentRetries = e.RetryCount;
                        if (cnn2.State == ConnectionState.Open)
                        {
                            // in some reason closing connection doesn't eliminate the active connection to the database
                            using (var cmd3 = cnn3.CreateCommand())
                            {
                                cmd3.CommandText = $"KILL {cnn2.ServerProcessId}";
                                cmd3.ExecuteNonQueryAsync();
                            }
                            cnn2.Close();
                        }
                    };

                    // drop the database
                    cmd.RetryLogicProvider = provider;
                    cmd.CommandText = $"DROP DATABASE [{database}]";
                    cmd.ExecuteNonQuery();

                    Assert.True(currentRetries > 0);
                }
                finally
                {
                    // additional try to drop the database if it still exists.
                    DataTestUtility.DropDatabase(cnn1, database);
                }
            }
        }

        // In Managed SNI by Named pipe connection, SqlCommand doesn't respect timeout. "ActiveIssue 12167"
        // Synapse: Does not support WAITFOR DELAY.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotUsingManagedSNIOnWindows), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyLockedTable), parameters: new object[] { 10 }, MemberType = typeof(RetryLogicTestHelper), DisableDiscoveryEnumeration = true)]
        public void UpdateALockedTable(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int currentRetries = 0;
            string tableName = DataTestUtility.GetLongName("Region");
            string fieldName = "RegionDescription";

            using (var cnn1 = new SqlConnection(cnnString))
            using (var cmd1 = new SqlCommand())
            using (var cnn2 = new SqlConnection(cnnString))
            using (var cmd2 = new SqlCommand())
            {
                cnn1.Open();
                cnn2.Open();

                cmd1.Connection = cnn1;
                cmd2.Connection = cnn2;

                // Create a separate table from Region
                cmd1.CommandText = $"SELECT TOP (1) * INTO {tableName} FROM Region;";
                cmd1.ExecuteNonQuery();

                try
                {
                    CancellationTokenSource tokenSource = new CancellationTokenSource();

                    provider.Retrying += (s, e) =>
                    {
                        currentRetries = e.RetryCount;
                        // cancel the blocker task
                        tokenSource.Cancel();
                        cmd1.Cancel();
                        cnn1.Close();
                    };

                    // Hold lock the table for 10 seconds (more that the connection timeout)
                    cmd1.CommandText = $"BEGIN TRAN; SELECT * FROM {tableName} WITH(TABLOCKx, HOLDLOCK); WAITFOR DELAY '00:00:10'; ROLLBACK;";
                    cmd1.ExecuteNonQueryAsync(tokenSource.Token);
                    // Be sure the table is locked.
                    Thread.Sleep(1000);

                    // Update the locked table
                    cmd2.RetryLogicProvider = provider;
                    cmd2.CommandTimeout = 1;
                    cmd2.CommandText = $"UPDATE {tableName} SET {fieldName} = 'updated';";
                    cmd2.ExecuteNonQuery();
                }
                finally
                {
                    DataTestUtility.DropTable(cnn2, tableName);
                }

                Assert.True(currentRetries > 0);
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> NoneRetriableExecuteFail_Data =>
            RetryLogicTestHelper.GetNonRetriableCases();

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(NoneRetriableExecuteFail_Data), DisableDiscoveryEnumeration = true)]
        public void NoneRetriableExecuteFail(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            string query = "SELECT bad command";

            using (SqlConnection cnn = new SqlConnection(cnnString))
            using (SqlCommand cmd = new SqlCommand())
            {
                cnn.Open();
                cmd.Connection = cnn;
                cmd.CommandText = query;
                cmd.RetryLogicProvider = provider;
                Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
                Assert.Throws<SqlException>(() => cmd.ExecuteReader());
                Assert.Throws<SqlException>(() => cmd.ExecuteReader(CommandBehavior.Default));
                Assert.Throws<SqlException>(() => cmd.ExecuteNonQuery());

                Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteScalarAsync()).Wait();
                Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteReaderAsync()).Wait();
                Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteReaderAsync(CommandBehavior.Default)).Wait();
                Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync()).Wait();

                if (DataTestUtility.IsNotAzureSynapse())
                {
                    cmd.CommandText = query + " FOR XML AUTO";
                    Assert.Throws<SqlException>(() => cmd.ExecuteXmlReader());
                    Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteXmlReaderAsync()).Wait();
                }
            }
        }
        #endregion

        #region Async

        public static TheoryData<string, SqlRetryLogicBaseProvider> RetryExecuteAsyncFail_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromMilliseconds(100));

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(RetryExecuteAsyncFail_Data), DisableDiscoveryEnumeration = true)]
        public async Task RetryExecuteAsyncFail(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries + 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            string query = "SELECT bad command";

            using (SqlConnection cnn = new SqlConnection(cnnString))
            using (SqlCommand cmd = CreateCommand(cnn, provider, cancelAfterRetries))
            {
                cmd.CommandText = query;
                var ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteScalarAsync());
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteReaderAsync());
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteReaderAsync(CommandBehavior.Default));
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None));
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => provider.ExecuteAsync(cmd, () => Task.Factory.FromAsync(cmd.BeginExecuteReader(), cmd.EndExecuteReader)));
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteNonQueryAsync());
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => provider.ExecuteAsync(cmd, () => Task.Factory.FromAsync(cmd.BeginExecuteNonQuery(), cmd.EndExecuteNonQuery)));
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                if (DataTestUtility.IsNotAzureSynapse())
                {
                    cmd.CommandText = query + " FOR XML AUTO";
                    ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteXmlReaderAsync());
                    Assert.Equal(numberOfTries, currentRetries + 1);
                    Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                    Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);

                    ex = await Assert.ThrowsAsync<AggregateException>(() => provider.ExecuteAsync(cmd, () => Task.Factory.FromAsync(cmd.BeginExecuteXmlReader(), cmd.EndExecuteXmlReader)));
                    Assert.Equal(numberOfTries, currentRetries + 1);
                    Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                    Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);
                }
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> RetryExecuteAsyncCancel_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromMilliseconds(100));

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(RetryExecuteAsyncCancel_Data), DisableDiscoveryEnumeration = true)]
        public async Task RetryExecuteAsyncCancel(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries - 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            string query = "SELECT bad command";

            using (SqlConnection cnn = new SqlConnection(cnnString))
            using (SqlCommand cmd = CreateCommand(cnn, provider, cancelAfterRetries))
            {
                cmd.CommandText = query;
                var ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteScalarAsync());
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteReaderAsync());
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteReaderAsync(CommandBehavior.Default));
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None));
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => provider.ExecuteAsync(cmd, () => Task.Factory.FromAsync(cmd.BeginExecuteReader(), cmd.EndExecuteReader)));
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteNonQueryAsync());
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                ex = await Assert.ThrowsAsync<AggregateException>(() => provider.ExecuteAsync(cmd, () => Task.Factory.FromAsync(cmd.BeginExecuteNonQuery(), cmd.EndExecuteNonQuery)));
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                if (DataTestUtility.IsNotAzureSynapse())
                {
                    cmd.CommandText = query + " FOR XML AUTO";
                    ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteXmlReaderAsync());
                    Assert.Equal(cancelAfterRetries, currentRetries);
                    Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                    Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);

                    ex = await Assert.ThrowsAsync<AggregateException>(() => provider.ExecuteAsync(cmd, () => Task.Factory.FromAsync(cmd.BeginExecuteXmlReader(), cmd.EndExecuteXmlReader)));
                    Assert.Equal(cancelAfterRetries, currentRetries);
                    Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                    Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);
                }
            }
        }
        #endregion

        #region Concurrent

        public static TheoryData<string, SqlRetryLogicBaseProvider> ConcurrentExecution_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromMilliseconds(100));

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(ConcurrentExecution_Data), DisableDiscoveryEnumeration = true)]
        public void ConcurrentExecution(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            string query = "SELECT bad command";
            ProcessDataInParallel(cnnString, provider, query, cmd => cmd.ExecuteScalar());
            ProcessDataInParallel(cnnString, provider, query, cmd => cmd.ExecuteNonQuery());
            ProcessDataInParallel(cnnString, provider, query, cmd => cmd.ExecuteReader());
            ProcessDataInParallel(cnnString, provider, query, cmd => cmd.ExecuteXmlReader());

            if (DataTestUtility.IsNotAzureSynapse())
            {
                query += " FOR XML AUTO";
                ProcessDataInParallel(cnnString, provider, query, cmd => cmd.ExecuteXmlReader());
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> ConcurrentExecutionAsync_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromMilliseconds(100));

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(ConcurrentExecutionAsync_Data), DisableDiscoveryEnumeration = true)]
        public async Task ConcurrentExecutionAsync(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            string query = "SELECT bad command";
            await ProcessDataAsAsync(cnnString, provider, query, cmd => cmd.ExecuteScalarAsync());
            await ProcessDataAsAsync(cnnString, provider, query, cmd => cmd.ExecuteNonQueryAsync());
            await ProcessDataAsAsync(cnnString, provider, query, cmd => cmd.ExecuteReaderAsync());
            await ProcessDataAsAsync(cnnString, provider, query, cmd => cmd.ExecuteXmlReaderAsync());

            if (DataTestUtility.IsNotAzureSynapse())
            {
                query += " FOR XML AUTO";
                await ProcessDataAsAsync(cnnString, provider, query, cmd => cmd.ExecuteXmlReaderAsync());
            }
        }
        #endregion

        #region private members
        private SqlCommand CreateCommand(SqlConnection cnn, SqlRetryLogicBaseProvider provider, int cancelAfterRetries)
        {
            cnn.Open();
            SqlCommand cmd = cnn.CreateCommand();
            cmd.RetryLogicProvider = provider;
            cmd.RetryLogicProvider.Retrying += (object s, SqlRetryingEventArgs e) =>
            {
                Assert.Equal(e.RetryCount, e.Exceptions.Count);
                Assert.NotEqual(TimeSpan.Zero, e.Delay);

                if (e.RetryCount >= cancelAfterRetries)
                {
                    e.Cancel = true;
                }
            };
            return cmd;
        }

        private static void ProcessDataInParallel(string cnnString, SqlRetryLogicBaseProvider provider,
                                                  string query, Action<SqlCommand> commandAction)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int retryExceptionCount = 0;

            Parallel.For(0, ConcurrentParallelExecutions,
            i =>
            {
                using (SqlConnection cnn = new SqlConnection(cnnString))
                {
                    cnn.Open();
                    Assert.True(cnn.State == ConnectionState.Open, UnexpectedSqlConnectionExceptionMessage);

                    using (SqlCommand cmd = cnn.CreateCommand())
                    {
                        cmd.RetryLogicProvider = provider;
                        cmd.CommandText = query;

                        AggregateException retryAggregateException = Assert.Throws<AggregateException>(() => commandAction(cmd));
                        Interlocked.Add(ref retryExceptionCount, retryAggregateException.InnerExceptions?.Count ?? 0);
                    }
                }
            });

            // Assertion for Retries
            int expectedTotalNumberOfRetries = numberOfTries * ConcurrentParallelExecutions;
            Assert.Equal(expectedTotalNumberOfRetries, retryExceptionCount);
        }

        private static async Task ProcessDataAsAsync(string cnnString, SqlRetryLogicBaseProvider provider,
                                                     string query, Func<SqlCommand, Task> commandAction)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int retryExceptionCount = 0;

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < ConcurrentParallelExecutions; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using (SqlConnection cnn = new SqlConnection(cnnString))
                    {
                        await cnn.OpenAsync();
                        Assert.True(cnn.State == ConnectionState.Open, UnexpectedSqlConnectionExceptionMessage);

                        using (SqlCommand cmd = cnn.CreateCommand())
                        {
                            cmd.RetryLogicProvider = provider;
                            cmd.CommandText = query;

                            AggregateException retryAggregateException = await Assert.ThrowsAsync<AggregateException>(async () => await commandAction(cmd));
                            Interlocked.Add(ref retryExceptionCount, retryAggregateException.InnerExceptions?.Count ?? 0);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assertion for Retries
            int expectedTotalNumberOfRetries = numberOfTries * ConcurrentParallelExecutions;
            Assert.Equal(expectedTotalNumberOfRetries, retryExceptionCount);
        }
        #endregion
    }
}
