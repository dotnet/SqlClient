// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [Trait("Set", "3")]
    public static class ParallelTransactionsTest
    {
        #region <<Basic Parallel Test>>
        // TODO Synapse: Remove dependency from Northwind database
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void BasicParallelTest_shouldThrowsUnsupported()
        {
            string connectionString = DataTestUtility.TCPConnectionString;
            string expectedErrorMessage = SystemDataResourceManager.Instance.ADP_ParallelTransactionsNotSupported(typeof(SqlConnection).Name);
            string tempTableName = "";
            try
            {
                tempTableName = CreateTempTable(connectionString);
                DataTestUtility.AssertThrows<InvalidOperationException>(
                    actionThatFails: () => { BasicParallelTest(connectionString, tempTableName); },
                    exceptionMessage: expectedErrorMessage);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempTableName))
                {
                    DropTempTable(connectionString, tempTableName);
                }
            }
        }

        private static void BasicParallelTest(string connectionString, string tempTableName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlTransaction trans1 = connection.BeginTransaction();
                SqlTransaction trans2 = connection.BeginTransaction();
                SqlTransaction trans3 = connection.BeginTransaction();

                using SqlCommand com1 = new SqlCommand("select top 1 EmployeeID from " + tempTableName, connection);
                com1.Transaction = trans1;
                com1.ExecuteNonQuery();

                using SqlCommand com2 = new SqlCommand("select top 1 EmployeeID from " + tempTableName, connection);
                com2.Transaction = trans2;
                com2.ExecuteNonQuery();

                using SqlCommand com3 = new SqlCommand("select top 1 EmployeeID from " + tempTableName, connection);
                com3.Transaction = trans3;
                com3.ExecuteNonQuery();

                trans1.Rollback();
                trans2.Rollback();
                trans3.Rollback();
            }
        }

        #endregion

        #region <<MultipleExecutesInSameTransactionTest>>
        // Synapse: Remove dependency from Northwind database
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void MultipleExecutesInSameTransactionTest_shouldThrowsUnsupported()
        {
            string connectionString = DataTestUtility.TCPConnectionString;
            string expectedErrorMessage = SystemDataResourceManager.Instance.ADP_ParallelTransactionsNotSupported(typeof(SqlConnection).Name);
            string tempTableName = "";
            try
            {
                tempTableName = CreateTempTable(connectionString);
                DataTestUtility.AssertThrows<InvalidOperationException>(
                    actionThatFails: () => { MultipleExecutesInSameTransactionTest(connectionString, tempTableName); },
                    exceptionMessage: expectedErrorMessage);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempTableName))
                {
                    DropTempTable(connectionString, tempTableName);
                }
            }
        }

        private static void MultipleExecutesInSameTransactionTest(string connectionString, string tempTableName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlTransaction trans1 = connection.BeginTransaction();
                SqlTransaction trans2 = connection.BeginTransaction();
                SqlTransaction trans3 = connection.BeginTransaction();

                using SqlCommand com1 = new SqlCommand("select top 1 EmployeeID from " + tempTableName, connection);
                com1.Transaction = trans1;
                com1.ExecuteNonQuery();

                using SqlCommand com2 = new SqlCommand("select top 1 EmployeeID from " + tempTableName, connection);
                com2.Transaction = trans2;
                com2.ExecuteNonQuery();

                using SqlCommand com3 = new SqlCommand("select top 1 EmployeeID from " + tempTableName, connection);
                com3.Transaction = trans3;
                com3.ExecuteNonQuery();

                trans1.Rollback();
                trans2.Rollback();
                trans3.Rollback();

                using SqlCommand com4 = new SqlCommand("select top 1 EmployeeID from " + tempTableName, connection);
                com4.Transaction = trans1;
                using (SqlDataReader reader4 = com4.ExecuteReader())
                {
                    // Scoped deliberately: MARS is off here, so the reader must be closed before
                    // the rollback below, which would otherwise fail on a connection that still
                    // has an open reader.
                }

                trans1.Rollback();
            }
        }
        #endregion

        private static string CreateTempTable(string connectionString)
        {
            var uniqueKey = string.Format("{0}_{1}_{2}", Environment.GetEnvironmentVariable("ComputerName"), Environment.TickCount, Guid.NewGuid()).Replace("-", "_");
            var tempTableName = "TEMP_" + uniqueKey;
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using SqlCommand cmd = new SqlCommand(string.Format("SELECT EmployeeID, LastName, FirstName, Title, Address, City, Region, PostalCode, Country into {0} from Employees", tempTableName), conn);
                cmd.ExecuteNonQuery();
                cmd.CommandText = string.Format("alter table {0} add constraint EmployeeID_{1} primary key (EmployeeID)", tempTableName, uniqueKey);
                cmd.ExecuteNonQuery();
            }

            return tempTableName;
        }

        /// <remarks>
        /// Best-effort: both callers invoke this from a <c>finally</c>, so a throw here would surface
        /// in place of the failure that is already propagating and hide the real test result. The
        /// table is named on failure so the leak stays attributable.
        /// </remarks>
        private static void DropTempTable(string connectionString, string tempTableName)
        {
            try
            {
                using (SqlConnection con1 = new SqlConnection(connectionString))
                {
                    con1.Open();
                    using SqlCommand cmd = new SqlCommand(
                        string.Format("IF (OBJECT_ID('{0}') IS NOT NULL) DROP TABLE {0}", tempTableName), con1);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to drop temp table '{tempTableName}'; it may be orphaned in the test database. {ex}");
            }
        }
    }
}
