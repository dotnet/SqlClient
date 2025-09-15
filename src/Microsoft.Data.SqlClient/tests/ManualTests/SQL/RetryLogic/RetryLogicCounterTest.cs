// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class RetryLogicCounterTest
    {
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("ExecuteScalarAsync", 3)]
        [InlineData("ExecuteReaderAsync", 3)]
        [InlineData("ExecuteXmlReaderAsync", 3)]
        [InlineData("ExecuteNonQueryAsync", 3)]
        public async Task ValidateRetryCount_SqlCommand_Async(string methodName, int numOfTries)
        {
            ErrorInfoRetryLogicProvider _errorInfoRetryProvider = new(
                SqlConfigurableRetryFactory.CreateFixedRetryProvider(new SqlRetryLogicOption()
                { NumberOfTries = numOfTries, TransientErrors = new[] { 50000 } }));

            try
            {
                using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
                connection.Open();

                using SqlCommand cmd = connection.CreateCommand();
                cmd.RetryLogicProvider = _errorInfoRetryProvider;
                cmd.CommandText = "THROW 50000,'Error',0";

                _errorInfoRetryProvider.CallCounter = 0;
                switch (methodName)
                {
                    case "ExecuteScalarAsync":
                        await cmd.ExecuteScalarAsync();
                        break;
                    case "ExecuteReaderAsync":
                        {
                            using SqlDataReader _ = await cmd.ExecuteReaderAsync();
                            break;
                        }
                    case "ExecuteXmlReaderAsync":
                        {
                            using System.Xml.XmlReader _ = await cmd.ExecuteXmlReaderAsync();
                            break;
                        }
                    case "ExecuteNonQueryAsync":
                        await cmd.ExecuteNonQueryAsync();
                        break;
                    default:
                        break;
                }
                Assert.Fail("Exception did not occur.");
            }
            catch
            {
                Assert.Equal(numOfTries, _errorInfoRetryProvider.CallCounter);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("ExecuteScalar", 3)]
        [InlineData("ExecuteReader", 3)]
        [InlineData("ExecuteXmlReader", 3)]
        [InlineData("ExecuteNonQuery", 3)]
        public void ValidateRetryCount_SqlCommand_Sync(string methodName, int numOfTries)
        {
            ErrorInfoRetryLogicProvider _errorInfoRetryProvider = new(
                SqlConfigurableRetryFactory.CreateFixedRetryProvider(new SqlRetryLogicOption()
                { NumberOfTries = numOfTries, TransientErrors = new[] { 50000 } }));

            try
            {
                using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
                connection.Open();

                using SqlCommand cmd = connection.CreateCommand();
                cmd.RetryLogicProvider = _errorInfoRetryProvider;
                cmd.CommandText = "THROW 50000,'Error',0";

                _errorInfoRetryProvider.CallCounter = 0;
                switch (methodName)
                {
                    case "ExecuteScalar":
                        cmd.ExecuteScalar();
                        break;
                    case "ExecuteReader":
                        {
                            using SqlDataReader _ = cmd.ExecuteReader();
                            break;
                        }
                    case "ExecuteXmlReader":
                        {
                            using System.Xml.XmlReader _ = cmd.ExecuteXmlReader();
                            break;
                        }
                    case "ExecuteNonQuery":
                        cmd.ExecuteNonQuery();
                        break;
                    default:
                        break;
                }
                Assert.Fail("Exception did not occur.");
            }
            catch
            {
                Assert.Equal(numOfTries, _errorInfoRetryProvider.CallCounter);
            }
        }

        public class ErrorInfoRetryLogicProvider : SqlRetryLogicBaseProvider
        {
            public SqlRetryLogicBaseProvider InnerProvider { get; }

            public ErrorInfoRetryLogicProvider(SqlRetryLogicBaseProvider innerProvider)
            {
                InnerProvider = innerProvider;
            }

            readonly AsyncLocal<int> _depth = new();
            public int CallCounter = 0;

            TResult LogCalls<TResult>(Func<TResult> function)
            {
                CallCounter++;
                return function();
            }

            public override TResult Execute<TResult>(object sender, Func<TResult> function)
            {
                _depth.Value++;
                try
                {
                    return InnerProvider.Execute(sender, () => LogCalls(function));
                }
                finally
                {
                    _depth.Value--;
                }
            }

            public async Task<TResult> LogCallsAsync<TResult>(Func<Task<TResult>> function)
            {
                CallCounter++;
                return await function();
            }

            public override async Task<TResult> ExecuteAsync<TResult>(object sender, Func<Task<TResult>> function,
                CancellationToken cancellationToken = default)
            {
                _depth.Value++;
                try
                {
                    return await InnerProvider.ExecuteAsync(sender, () => LogCallsAsync(function), cancellationToken);
                }
                finally
                {
                    _depth.Value--;
                }
            }

            public async Task LogCallsAsync(Func<Task> function)
            {
                CallCounter++;
                await function();
            }

            public override async Task ExecuteAsync(object sender, Func<Task> function,
                CancellationToken cancellationToken = default)
            {
                _depth.Value++;
                try
                {
                    await InnerProvider.ExecuteAsync(sender, () => LogCallsAsync(function), cancellationToken);
                }
                finally
                {
                    _depth.Value--;
                }
            }
        }
    }
}
