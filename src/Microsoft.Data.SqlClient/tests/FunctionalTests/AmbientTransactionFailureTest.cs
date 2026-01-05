// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Transactions;
using Xunit;

namespace Microsoft.Data.SqlClient.FunctionalTests
{
    public class AmbientTransactionFailureTest
    {
        private static readonly bool s_isNotArmProcess = RuntimeInformation.ProcessArchitecture != Architecture.Arm;

        private static readonly string s_servername = Guid.NewGuid().ToString();
        private static readonly string s_connectionStringWithEnlistAsDefault = $"Data Source={s_servername}; Integrated Security=true; Connect Timeout=1;";
        private static readonly string s_connectionStringWithEnlistOff = $"Data Source={s_servername}; Integrated Security=true; Connect Timeout=1;Enlist=False";

        private static readonly Action<string> ConnectToServer = (connectionString) =>
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
        };

        private static readonly Action<string> ConnectToServerInTransactionScope = (connectionString) =>
        {
            using TransactionScope scope = new TransactionScope();
            ConnectToServer(connectionString);
        };

        public static readonly TheoryData<Action<string>, string> TestSqlException_Data = new()
        {
            { ConnectToServerInTransactionScope, s_connectionStringWithEnlistOff },
            { ConnectToServer, s_connectionStringWithEnlistAsDefault }
        };

        // @TODO: Verify that this test still will not run on ARM.
        [ConditionalTheory(nameof(s_isNotArmProcess))] // https://github.com/dotnet/corefx/issues/21598
        [MemberData(
            nameof(TestSqlException_Data),
            // xUnit can't consistently serialize the data for this test, so we
            // disable enumeration of the test data to avoid warnings on the
            // console.
            DisableDiscoveryEnumeration = true)]
        public void TestSqlException(Action<string> connectAction, string connectionString)
        {
            Assert.Throws<SqlException>(() =>
            {
                connectAction(connectionString);
            });
        }
    }
}
