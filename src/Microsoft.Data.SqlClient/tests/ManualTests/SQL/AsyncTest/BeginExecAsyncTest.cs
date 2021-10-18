// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class BeginExecAsyncTest
    {
        private static string GenerateCommandText()
        {
            int suffix = (new Random()).Next(5000);

            string commandText =
                $"CREATE TABLE #Shippers{suffix}(" +
                    $"[ShipperID][int] NULL," +
                    $"[CompanyName] [nvarchar] (40) NOT NULL," +
                    $"[Phone] [nvarchar] (24) NULL )" +
                $"INSERT INTO #Shippers{suffix}" +
                        $"([CompanyName]  " +
                        $",[Phone])" +
                    $"VALUES " +
                        $"('Acme Inc.' " +
                        $",'555-1212'); " +
                $"WAITFOR DELAY '0:0:3'; " +
                $"DELETE FROM #Shippers{suffix} WHERE ShipperID > 3;";

            return commandText;
        }

        // Synapse: Parse error at line: 1, column: 201: Incorrect syntax near ';'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ExecuteTest()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);

            using SqlCommand command = new(GenerateCommandText(), connection);
            connection.Open();

            IAsyncResult result = command.BeginExecuteNonQuery();
            while (!result.IsCompleted)
            {
                System.Threading.Thread.Sleep(100);
            }

            Assert.True(command.EndExecuteNonQuery(result) > 0, "FAILED: BeginExecuteNonQuery did not complete successfully.");
        }

        // Synapse: Parse error at line: 1, column: 201: Incorrect syntax near ';'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void FailureTest()
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                SqlCommand command = new SqlCommand(GenerateCommandText(), connection);
                connection.Open();

                //Try to execute a synchronous query on same command
                IAsyncResult result = command.BeginExecuteNonQuery();
                InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

                while (!result.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                Assert.True(result.IsCompleted, "FAILED: ExecuteNonQueryAsync Task did not complete successfully.");
                Assert.True(command.EndExecuteNonQuery(result) > 0, "FAILED: No rows affected");
            }
        }
    }
}
