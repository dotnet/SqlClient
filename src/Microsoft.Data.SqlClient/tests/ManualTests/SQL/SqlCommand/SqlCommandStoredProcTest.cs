// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlCommandStoredProcTest
    {
        private static readonly string s_tcp_connStr = DataTestUtility.TCPConnectionString;

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ShouldFailWithExceededLengthForSP()
        {
            string baseCommandText = "random text\u0000\u400a\u7300\u7400\u6100\u7400\u6500\u6d00\u6500\u6e00\u7400\u0000\u0006\u01ff\u0900\uf004\u0000\uffdc\u0001";
            string exceededLengthText = baseCommandText + new string(' ', 2000);
            using SqlConnection conn = new(s_tcp_connStr);
            conn.Open();
            using SqlCommand command = new()
            {
                Connection = conn,
                CommandType = CommandType.StoredProcedure,
                CommandText = exceededLengthText
            };

            // It should fail on the driver as the length of RPC is over 1046
            // 4-part name 1 + 128 + 1 + 1 + 1 + 128 + 1 + 1 + 1 + 128 + 1 + 1 + 1 + 128 + 1 = 523
            // each char takes 2 bytes. 523 * 2 = 1046
            Assert.Throws<ArgumentException>(() => command.ExecuteScalar());

            command.CommandText = baseCommandText;
            var ex = Assert.Throws<SqlException>(() => command.ExecuteScalar());
            Assert.StartsWith("Could not find stored procedure", ex.Message);
        }
    }
}
