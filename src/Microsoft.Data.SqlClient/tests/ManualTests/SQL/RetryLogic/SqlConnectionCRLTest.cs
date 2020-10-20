// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlConnectionCRLTest
    {
        private const string InvalidInitialCatalog = "InvalidInitialCatalog_for_retry";

        #region Sync
        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCatalog), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
        public void ConnectionRetryOpenInvalidCatalogFailed(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfRetries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfRetries + 1;
            var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries);
            Assert.Throws<AggregateException>(() => cnn.Open());
            Assert.Equal(numberOfRetries, provider.RetryLogic?.Current + 1);
        }

        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCatalog), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
        public void ConnectionCancelRetryOpenInvalidCatalog(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int cancelAfterRetries = provider.RetryLogic.NumberOfTries - 1;
            var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries);
            Assert.Throws<AggregateException>(() => cnn.Open());
            Assert.Equal(cancelAfterRetries, provider.RetryLogic.Current);
        }

        [ActiveIssue(14590, TestPlatforms.Windows)]
        // avoid creating a new database in Azure
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer))]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyLongRunner), parameters: new object[] { 10 }, MemberType = typeof(RetryLogicTestHelper))]
        public void CreateDatabaseWhileTryingToConnect(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            var r = new Random();

            string database = DataTestUtility.GetUniqueNameForSqlServer($"RetryLogic_{provider.RetryLogic.RetryIntervalEnumerator.GetType().Name}", false);
            var builder = new SqlConnectionStringBuilder(cnnString)
            {
                InitialCatalog = database,
                ConnectTimeout = 1
            };

            using (var cnn1 = new SqlConnection(new SqlConnectionStringBuilder(cnnString) { ConnectTimeout = 60 }.ConnectionString))
            {
                cnn1.Open();
                try
                {
                    provider.Retrying += (object s, SqlRetryingEventArgs e) =>
                    {
                        using (var cmd = cnn1.CreateCommand())
                        {
                            // Create database just after first faliure.
                            if (e.RetryCount == 0)
                            {
                                cmd.CommandText = $"CREATE DATABASE [{database}];";
                                cmd.ExecuteNonQueryAsync();
                            }
                        }
                    };

                    using (var cnn2 = new SqlConnection(builder.ConnectionString))
                    {
                        cnn2.RetryLogicProvider = provider;
                        cnn2.Open();
                    }
                }
                catch (Exception e)
                {
                    Assert.Null(e);
                }
                finally
                {
                    DataTestUtility.DropDatabase(cnn1, database);
                }
            }
            Assert.True(provider.RetryLogic.Current > 0);
        }

        #endregion

        #region Async
        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCatalog), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
        public async void ConnectionRetryOpenAsyncInvalidCatalogFailed(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfRetries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfRetries + 1;
            var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries);
            await Assert.ThrowsAsync<AggregateException>(() => cnn.OpenAsync());
            Assert.Equal(numberOfRetries, provider.RetryLogic.Current + 1);
        }

        [Theory]
        [MemberData(nameof(RetryLogicTestHelper.GetConnectionAndRetryStrategyInvalidCatalog), parameters: new object[] { 2 }, MemberType = typeof(RetryLogicTestHelper))]
        public async void ConnectionCancelRetryOpenAsyncInvalidCatalog(string cnnString, SqlRetryLogicBaseProvider provider)
        {
            int numberOfRetries = provider.RetryLogic.NumberOfTries;
            int cancelAfterRetries = numberOfRetries - 1;
            var cnn = CreateConnectionWithInvalidCatalog(cnnString, provider, cancelAfterRetries);
            await Assert.ThrowsAsync<AggregateException>(() => cnn.OpenAsync());
            Assert.Equal(cancelAfterRetries, provider.RetryLogic.Current);
        }

        #endregion

        #region private members
        private SqlConnection CreateConnectionWithInvalidCatalog(string cnnString, SqlRetryLogicBaseProvider provider, int cancelAfterRetries)
        {
            var builder = new SqlConnectionStringBuilder(cnnString)
            {
                InitialCatalog = InvalidInitialCatalog
            };

            SqlConnection cnn = new SqlConnection(builder.ConnectionString);
            cnn.RetryLogicProvider = provider;
            cnn.RetryLogicProvider.Retrying += (object s, SqlRetryingEventArgs e) =>
            {
                if (e.RetryCount > cancelAfterRetries)
                {
                    e.Cancel = true;
                }
            };

            return cnn;
        }
        #endregion
    }
}
