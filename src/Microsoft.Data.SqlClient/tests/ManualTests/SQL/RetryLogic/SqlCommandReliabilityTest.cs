// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlCommandReliabilityTest
    {
        private readonly string _exceedErrMsgPattern = RetryLogicTestHelper.s_ExceedErrMsgPattern;
        private readonly string _cancelErrMsgPattern = RetryLogicTestHelper.s_CancelErrMsgPattern;

        #region Sync
        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCommand), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
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

                cmd.CommandText = query + " FOR XML AUTO";
                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteXmlReader());
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);
            }
        }

        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCommand), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
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

                cmd.CommandText = query + " FOR XML AUTO";
                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteXmlReader());
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);
            }
        }

        [ActiveIssue(14588)]
        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCommand), parameters: new object[] { 5 }, MemberType = typeof(RetryLogicTestHelper))]
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

        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCommand), parameters: new object[] { 5 }, MemberType = typeof(RetryLogicTestHelper))]
        public void RetryExecuteWithTrans(string cnnString, SqlRetryLogicBaseProvider provider)
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

        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyFilterDMLStatements), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
        public void RetryExecuteUnauthorizedSqlStatementDML(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries + 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;

            using (SqlConnection cnn = new SqlConnection(cnnString))
            using (SqlCommand cmd = CreateCommand(cnn, provider, cancelAfterRetries))
            {
                #region unauthorized
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
                #endregion

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

        [ActiveIssue(14325)]
        // avoid creating a new database in Azure
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer))]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyDropDB), parameters: new object[] { 5 }, MemberType = typeof(RetryLogicTestHelper))]
        public void DropDatabaseWithActiveConnection(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int currentRetries = 0;
            string database = DataTestUtility.GetUniqueNameForSqlServer($"RetryLogic_{provider.RetryLogic.RetryIntervalEnumerator.GetType().Name}", false);
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
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotUsingManagedSNIOnWindows))]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyLockedTable), parameters: new object[] { 10 }, MemberType = typeof(RetryLogicTestHelper))]
        public void UpdateALockedTable(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int currentRetries = 0;
            string tableName = DataTestUtility.GetUniqueNameForSqlServer("Region");
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

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(RetryLogicTestHelper.GetNoneRetriableCondition), MemberType = typeof(RetryLogicTestHelper))]
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

                cmd.CommandText = query + " FOR XML AUTO";
                Assert.Throws<SqlException>(() => cmd.ExecuteXmlReader());
                Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteXmlReaderAsync()).Wait();
            }
        }
        #endregion

        #region Async
        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCommand), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
        public async void RetryExecuteAsyncFail(string cnnString, SqlRetryLogicBaseProvider provider)
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

        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCommand), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
        public async void RetryExecuteAsyncCancel(string cnnString, SqlRetryLogicBaseProvider provider)
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
        #endregion

        #region Concurrent
        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCommand), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
        public void ConcurrentExecution(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            var cnnStr = new SqlConnectionStringBuilder(cnnString) { MultipleActiveResultSets = true, ConnectTimeout = 0 }.ConnectionString;
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            string query = "SELECT bad command";
            int retriesCount = 0;
            int concurrentExecution = 5;
            provider.Retrying += (s, e) => Interlocked.Increment(ref retriesCount);

            using (SqlConnection cnn = new SqlConnection(cnnStr))
            {
                cnn.Open();

                Parallel.For(0, concurrentExecution,
                i =>
                {
                    using (SqlCommand cmd = cnn.CreateCommand())
                    {
                        cmd.RetryLogicProvider = provider;
                        cmd.CommandText = query;
                        Assert.Throws<AggregateException>(() => cmd.ExecuteScalar());
                    }
                });
                Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);

                retriesCount = 0;
                Parallel.For(0, concurrentExecution,
                i =>
                {
                    using (SqlCommand cmd = cnn.CreateCommand())
                    {
                        cmd.RetryLogicProvider = provider;
                        cmd.CommandText = query;
                        Assert.Throws<AggregateException>(() => cmd.ExecuteNonQuery());
                    }
                });
                Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);

                retriesCount = 0;
                Parallel.For(0, concurrentExecution,
                i =>
                {
                    using (SqlCommand cmd = cnn.CreateCommand())
                    {
                        cmd.RetryLogicProvider = provider;
                        cmd.CommandText = query;
                        Assert.Throws<AggregateException>(() => cmd.ExecuteReader());
                    }
                });
                Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);

                retriesCount = 0;
                Parallel.For(0, concurrentExecution,
                i =>
                {
                    using (SqlCommand cmd = cnn.CreateCommand())
                    {
                        cmd.RetryLogicProvider = provider;
                        cmd.CommandText = query + " FOR XML AUTO";
                        Assert.Throws<AggregateException>(() => cmd.ExecuteXmlReader());
                    }
                });
                Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);

                retriesCount = 0;
                Parallel.For(0, concurrentExecution,
                i =>
                {
                    using (SqlCommand cmd = cnn.CreateCommand())
                    {
                        cmd.RetryLogicProvider = provider;
                        cmd.CommandText = query;
                        Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteScalarAsync()).Wait();
                    }
                });
                Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);

                retriesCount = 0;
                Parallel.For(0, concurrentExecution,
                i =>
                {
                    using (SqlCommand cmd = cnn.CreateCommand())
                    {
                        cmd.RetryLogicProvider = provider;
                        cmd.CommandText = query;
                        Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteNonQueryAsync()).Wait();
                    }
                });
                Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);

                retriesCount = 0;
                Parallel.For(0, concurrentExecution,
                i =>
                {
                    using (SqlCommand cmd = cnn.CreateCommand())
                    {
                        cmd.RetryLogicProvider = provider;
                        cmd.CommandText = query;
                        Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteReaderAsync()).Wait();
                    }
                });
                Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);

                retriesCount = 0;
                Parallel.For(0, concurrentExecution,
                i =>
                {
                    using (SqlCommand cmd = cnn.CreateCommand())
                    {
                        cmd.RetryLogicProvider = provider;
                        cmd.CommandText = query + " FOR XML AUTO";
                        Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteXmlReaderAsync()).Wait();
                    }
                });
                Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);
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
        #endregion
    }
}
