// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class RetryLogicTestHelper
    {
        private static readonly HashSet<int> s_defaultTransientErrors =
        [
            .. SqlConfigurableRetryFactory.IntrinsicTransientErrors,
            4060,   // Cannot open database requested by the login. The login failed.
            10928,  // Resource ID : %d. The %s limit for the database is %d and has been reached.
            10929,  // Resource ID : %d. The %s limit for the database is %d and has been reached.
            40197,  // The service has encountered an error processing your request. Please try again.
            40501,  // The service is currently busy. Retry the request after 10 seconds. Code: (reason code to be decoded).
            40613   // Database XXXX on server YYYY is not currently available. Please retry the connection later.
        ];

        public static readonly Regex FilterDmlStatements = new Regex(
            @"\b(INSERT( +INTO)|UPDATE|DELETE|TRUNCATE)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static readonly string s_exceedErrMsgPattern = SystemDataResourceManager.Instance.SqlRetryLogic_RetryExceeded;
        internal static readonly string s_cancelErrMsgPattern = SystemDataResourceManager.Instance.SqlRetryLogic_RetryCanceled;

        public static TheoryData<string, SqlRetryLogicBaseProvider> GetConnectionStringAndRetryProviders(
            int numberOfRetries,
            TimeSpan maxInterval,
            TimeSpan? deltaTime = null,
            IEnumerable<int> transientErrorCodes = null,
            Regex unauthorizedStatementRegex = null)
        {
            var option = new SqlRetryLogicOption
            {
                NumberOfTries = numberOfRetries,
                DeltaTime = deltaTime ?? TimeSpan.FromMilliseconds(10),
                MaxTimeInterval = maxInterval,
                TransientErrors = transientErrorCodes ?? s_defaultTransientErrors,
                AuthorizedSqlCondition = RetryPreCondition(unauthorizedStatementRegex)
            };

            var result = new TheoryData<string, SqlRetryLogicBaseProvider>();
            foreach (var connectionString in GetConnectionStringsTyped())
            {
                foreach (var retryProvider in GetRetryStrategiesTyped(option))
                {
                    result.Add(connectionString, retryProvider);
                }
            }

            return result;
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> GetNonRetriableCases() =>
            new TheoryData<string, SqlRetryLogicBaseProvider>
            {
                { DataTestUtility.TCPConnectionString, null },
                { DataTestUtility.TCPConnectionString, SqlConfigurableRetryFactory.CreateNoneRetryProvider() }
            };

        private static IEnumerable<string> GetConnectionStringsTyped()
        {
            var builder = new SqlConnectionStringBuilder();
            foreach (var connectionString in DataTestUtility.GetConnectionStrings(withEnclave: false))
            {
                builder.Clear();
                builder.ConnectionString = connectionString;
                builder.ConnectTimeout = 5;
                builder.Pooling = false;
                yield return builder.ConnectionString;

                builder.Pooling = true;
                yield return builder.ConnectionString;
            }
        }

        private static IEnumerable<SqlRetryLogicBaseProvider> GetRetryStrategiesTyped(SqlRetryLogicOption option)
        {
            yield return SqlConfigurableRetryFactory.CreateExponentialRetryProvider(option);
            yield return SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(option);
            yield return SqlConfigurableRetryFactory.CreateFixedRetryProvider(option);
        }

        public static IEnumerable<int> GetDefaultTransientErrorCodes(params int[] additionalCodes)
        {
            var transientErrorCodes = new HashSet<int>(s_defaultTransientErrors);
            foreach (int additionalCode in additionalCodes)
            {
                transientErrorCodes.Add(additionalCode);
            }

            return transientErrorCodes;
        }

        /// Generate a predicate function to skip unauthorized SQL commands.
        private static Predicate<string> RetryPreCondition(Regex unauthorizedStatementRegex)
        {
            return commandText => unauthorizedStatementRegex is null ||
                                  !unauthorizedStatementRegex.IsMatch(commandText);
        }
    }
}
