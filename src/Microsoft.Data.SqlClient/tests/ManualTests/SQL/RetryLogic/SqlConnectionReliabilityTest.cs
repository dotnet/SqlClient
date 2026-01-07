// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlConnectionReliabilityTest
    {
        internal const string InvalidInitialCatalog = "InvalidInitialCatalog_for_retry";
        private readonly string _exceedErrMsgPattern = RetryLogicTestHelper.s_exceedErrMsgPattern;
        private readonly string _cancelErrMsgPattern = RetryLogicTestHelper.s_cancelErrMsgPattern;

        #region Sync

        public static TheoryData<string, SqlRetryLogicBaseProvider> ConnectionRetryOpenInvalidCatalogFailed_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromSeconds(1),
                deltaTime: TimeSpan.FromMilliseconds(250));

        // Test relies on error 4060 for automatic retry, which is not reliable when using Azure or AAD auth
        // Restricted to non azure: https://github.com/dotnet/SqlClient/issues/3821
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [MemberData(nameof(ConnectionRetryOpenInvalidCatalogFailed_Data), DisableDiscoveryEnumeration = true)]
        public void ConnectionRetryOpenInvalidCatalogFailed(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries + 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            using (var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries))
            {
                var ex = Assert.Throws<AggregateException>(() => cnn.Open());
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> ConnectionCancelRetryOpenInvalidCatalog_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromSeconds(1),
                deltaTime: TimeSpan.FromMilliseconds(250));

        // Test relies on error 4060 for automatic retry, which is not reliable when using Azure or AAD auth
        // Restricted to non azure: https://github.com/dotnet/SqlClient/issues/3821
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [MemberData(nameof(ConnectionCancelRetryOpenInvalidCatalog_Data), DisableDiscoveryEnumeration = true)]
        public void ConnectionCancelRetryOpenInvalidCatalog(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int cancelAfterRetries = provider.RetryLogic.NumberOfTries - 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            using (var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries))
            {
                var ex = Assert.Throws<AggregateException>(() => cnn.Open());
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> CreateDatabaseWhileTryingToConnect_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 10,
                maxInterval: TimeSpan.FromSeconds(10),
                deltaTime: TimeSpan.FromSeconds(1));

        // avoid creating a new database in Azure
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(CreateDatabaseWhileTryingToConnect_Data), DisableDiscoveryEnumeration = true)]
        public void CreateDatabaseWhileTryingToConnect(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int currentRetries = 0;
            string database = DataTestUtility.GetLongName($"RetryLogic_{provider.RetryLogic.RetryIntervalEnumerator.GetType().Name}", false);
            var builder = new SqlConnectionStringBuilder(cnnString)
            {
                InitialCatalog = database,
                ConnectTimeout = 1
            };

            using (var cnn1 = new SqlConnection(new SqlConnectionStringBuilder(cnnString) { ConnectTimeout = 60, Pooling = false }.ConnectionString))
            {
                cnn1.Open();
                using (var cmd = cnn1.CreateCommand())
                {
                    Task createDBTask = null;
                    try
                    {
                        provider.Retrying += (s, e) =>
                        {
                            currentRetries = e.RetryCount;
                            // Try to create database just after first faliure.
                            if (createDBTask == null || createDBTask.Status == TaskStatus.Faulted)
                            {
                                cmd.CommandText = $"IF (NOT EXISTS(SELECT 1 FROM sys.databases WHERE name = '{database}')) CREATE DATABASE [{database}];";
                                createDBTask = cmd.ExecuteNonQueryAsync();
                            }
                        };

                        using (var cnn2 = new SqlConnection(builder.ConnectionString))
                        {
                            cnn2.RetryLogicProvider = provider;
                            cnn2.Open();
                        }
                    }
                    finally
                    {
                        createDBTask?.Wait();
                        DataTestUtility.DropDatabase(cnn1, database);
                    }
                }
            }
            Assert.True(currentRetries > 0);
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> ConcurrentExecution_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromSeconds(1),
                deltaTime: TimeSpan.FromMilliseconds(250));

        // Restricted to non azure: https://github.com/dotnet/SqlClient/issues/3821
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [MemberData(nameof(ConcurrentExecution_Data), DisableDiscoveryEnumeration = true)]
        public void ConcurrentExecution(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries + 1;
            int retriesCount = 0;
            int concurrentExecution = 5;
            provider.Retrying += (s, e) => Interlocked.Increment(ref retriesCount);

            Parallel.For(0, concurrentExecution,
            i =>
            {
                using (var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries))
                {
                    Assert.Throws<AggregateException>(() => cnn.Open());
                }
            });
            Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);

            retriesCount = 0;
            Parallel.For(0, concurrentExecution,
            i =>
            {
                using (var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries))
                {
                    Assert.ThrowsAsync<AggregateException>(() => cnn.OpenAsync()).Wait();
                }
            });
            Assert.Equal(numberOfTries * concurrentExecution, retriesCount + concurrentExecution);
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> DefaultOpenWithoutRetry_Data =>
            RetryLogicTestHelper.GetNonRetriableCases();

        // Restricted to non azure: https://github.com/dotnet/SqlClient/issues/3821
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [MemberData(nameof(DefaultOpenWithoutRetry_Data), DisableDiscoveryEnumeration = true)]
        public void DefaultOpenWithoutRetry(string connectionString, SqlRetryLogicBaseProvider cnnProvider)
        {
            var cnnString = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = InvalidInitialCatalog
            }.ConnectionString;

            Assert.Throws<SqlException>(() => new SqlConnection(cnnString).Open());
            Assert.ThrowsAsync<SqlException>(() => new SqlConnection(cnnString).OpenAsync()).Wait();

            using (var cnn = new SqlConnection(cnnString))
            {
                cnn.RetryLogicProvider = cnnProvider;
                Assert.Throws<SqlException>(() => cnn.Open());
                cnn.RetryLogicProvider = cnnProvider;
                Assert.ThrowsAsync<SqlException>(() => cnn.OpenAsync()).Wait();
            }
        }

        #endregion

        #region Async

        public static TheoryData<string, SqlRetryLogicBaseProvider> ConnectionRetryOpenAsyncInvalidCatalogFailed_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 5,
                maxInterval: TimeSpan.FromSeconds(1),
                deltaTime: TimeSpan.FromMilliseconds(250));

        // Test relies on error 4060 for automatic retry, which is not reliable when using Azure or AAD auth
        // Restricted to non azure: https://github.com/dotnet/SqlClient/issues/3821
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [MemberData(nameof(ConnectionRetryOpenAsyncInvalidCatalogFailed_Data), DisableDiscoveryEnumeration = true)]
        public async Task ConnectionRetryOpenAsyncInvalidCatalogFailed(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries + 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            using (var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries))
            {
                var ex = await Assert.ThrowsAsync<AggregateException>(() => cnn.OpenAsync());
                Assert.Equal(numberOfTries, currentRetries + 1);
                Assert.Equal(numberOfTries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_exceedErrMsgPattern, numberOfTries), ex.Message);
            }
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> ConnectionCancelRetryOpenAsyncInvalidCatalog_Data =>
            RetryLogicTestHelper.GetConnectionStringAndRetryProviders(
                numberOfRetries: 2,
                maxInterval: TimeSpan.FromSeconds(1),
                deltaTime: TimeSpan.FromMilliseconds(250));

        // Test relies on error 4060 for automatic retry, which is not returned when using AAD auth
        // Restricted to non azure: https://github.com/dotnet/SqlClient/issues/3821
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.TcpConnectionStringDoesNotUseAadAuth), nameof(DataTestUtility.IsNotAzureServer))]
        [MemberData(nameof(ConnectionCancelRetryOpenAsyncInvalidCatalog_Data), DisableDiscoveryEnumeration = true)]
        public async Task ConnectionCancelRetryOpenAsyncInvalidCatalog(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfTries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfTries - 1;
            int currentRetries = 0;
            provider.Retrying += (s, e) => currentRetries = e.RetryCount;
            using (var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries))
            {
                var ex = await Assert.ThrowsAsync<AggregateException>(() => cnn.OpenAsync());
                Assert.Equal(cancelAfterRetries, currentRetries);
                Assert.Equal(cancelAfterRetries, ex.InnerExceptions.Count);
                Assert.Contains(string.Format(_cancelErrMsgPattern, currentRetries), ex.Message);
            }
        }

        #endregion

        #region private members
        private SqlConnection CreateConnectionWithInvalidCatalog(string cnnString, SqlRetryLogicBaseProvider provider, int cancelAfterRetries)
        {
            var builder = new SqlConnectionStringBuilder(cnnString)
            {
                InitialCatalog = InvalidInitialCatalog
            };

            SqlConnection cnn = new(builder.ConnectionString)
            {
                RetryLogicProvider = provider
            };
            cnn.RetryLogicProvider.Retrying += (s, e) =>
            {
                Assert.Equal(e.RetryCount, e.Exceptions.Count);
                Assert.NotEqual(TimeSpan.Zero, e.Delay);
                if (e.RetryCount >= cancelAfterRetries)
                {
                    e.Cancel = true;
                }
            };

            return cnn;
        }
        #endregion
    }
}
