// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

﻿using System;
using System.Diagnostics;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.Common.SystemDataInternals;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class AADFedAuthTokenRefreshTest
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public AADFedAuthTokenRefreshTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAADPasswordConnStrSetup))]
        public void FedAuthTokenRefreshTest()
        {
            string connectionString = DataTestUtility.AADPasswordConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string oldTokenHash = "";
                DateTime? oldExpiryDateTime = FedAuthTokenHelper.SetTokenExpiryDateTime(connection, minutesToExpire: 1, out oldTokenHash);
                Assert.True(oldExpiryDateTime != null, "Failed to make token expiry to expire in one minute.");

                // Convert and display the old expiry into local time which should be in 1 minute from now
                DateTime oldLocalExpiryTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)oldExpiryDateTime, TimeZoneInfo.Local);
                LogInfo($"Token: {oldTokenHash}   Old Expiry: {oldLocalExpiryTime}");
                TimeSpan timeDiff = oldLocalExpiryTime - DateTime.Now;
                Assert.InRange(timeDiff.TotalSeconds, 0, 60);

                // Check if connection is still alive to continue further testing
                string result = "";
                SqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = "select @@version";
                result = $"{cmd.ExecuteScalar()}";
                Assert.True(result != string.Empty, "The connection's command must return a value");

                // The new connection will use the same FedAuthToken but will refresh it first as it will expire in 1 minute.
                using (SqlConnection connection2 = new SqlConnection(connectionString))
                {
                    connection2.Open();

                    // Check if connection is alive
                    cmd = connection2.CreateCommand();
                    cmd.CommandText = "select 1";
                    result = $"{cmd.ExecuteScalar()}";
                    Assert.True(result != string.Empty, "The connection's command must return a value after a token refresh.");

                    string newTokenHash = "";
                    DateTime? newExpiryDateTime = FedAuthTokenHelper.GetTokenExpiryDateTime(connection2, out newTokenHash);
                    DateTime newLocalExpiryTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)newExpiryDateTime, TimeZoneInfo.Local);
                    LogInfo($"Token: {newTokenHash}   New Expiry: {newLocalExpiryTime}");

                    Assert.True(oldTokenHash == newTokenHash, "The token's hash before and after token refresh must be identical.");
                    Assert.True(newLocalExpiryTime > oldLocalExpiryTime, "The refreshed token must have a new or later expiry time.");
                }
            }
        }

        [Conditional("DEBUG")]
        private void LogInfo(string message)
        {
            _testOutputHelper.WriteLine(message);
        }
    }
}
