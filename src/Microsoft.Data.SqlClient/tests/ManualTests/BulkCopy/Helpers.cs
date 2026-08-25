// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using Microsoft.Data.SqlClient.ManualTesting.Tests;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    public class Helpers
    {
        internal static void ProcessCommandBatch(Type connType, string constr, string[] batch)
        {
            if (batch.Length > 0)
            {
                object[] activatorArgs = new object[1];
                activatorArgs[0] = constr;
                using (DbConnection conn = (DbConnection)Activator.CreateInstance(connType, activatorArgs))
                {
                    conn.Open();
                    DbCommand cmd = conn.CreateCommand();

                    ProcessCommandBatch(cmd, batch);
                }
            }
        }

        internal static void ProcessCommandBatch(DbCommand cmd, string[] batch)
        {
            foreach (string cmdtext in batch)
            {
                Helpers.TryExecute(cmd, cmdtext);
            }
        }

        /// <summary>
        /// Executes a batch of cleanup statements, running each one independently.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="ProcessCommandBatch(DbCommand, string[])"/>, a statement that fails does
        /// not prevent the remaining statements from running. Cleanup batches typically remove several
        /// objects (for example a table and the schema or trigger that depends on it), and aborting on
        /// the first failure leaks everything that follows into the shared test database.
        /// </remarks>
        internal static void ProcessCleanupBatch(DbCommand cmd, string[] batch)
        {
            foreach (string cmdtext in batch)
            {
                TryCleanup(cmd, cmdtext);
            }
        }

        /// <summary>
        /// Executes a batch of cleanup statements on a new connection, running each one independently.
        /// </summary>
        internal static void ProcessCleanupBatch(Type connType, string constr, string[] batch)
        {
            if (batch.Length == 0)
            {
                return;
            }

            try
            {
                using DbConnection conn = (DbConnection)Activator.CreateInstance(connType, new object[] { constr });
                conn.Open();

                using DbCommand cmd = conn.CreateCommand();
                ProcessCleanupBatch(cmd, batch);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cleanup batch could not be run: {e.Message}");
            }
        }

        /// <summary>
        /// Executes a single cleanup statement, best-effort.
        /// </summary>
        internal static void TryCleanup(DbCommand cmd, string statement)
        {
            try
            {
                TryExecute(cmd, statement);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cleanup statement failed ({statement}): {e.Message}");
            }
        }

        /// <summary>
        /// Drops a table if it exists, best-effort.
        /// </summary>
        /// <remarks>
        /// Test table names embed a GUID, so anything that is not dropped stays in the shared test
        /// database forever. The drop is therefore both guarded (so that dropping a table which was
        /// never created is a no-op) and best-effort (so that one failure does not prevent subsequent
        /// cleanup from running).
        /// </remarks>
        public static void DropTable(DbCommand cmd, string tableName) =>
            TryCleanup(cmd, GetDropTableStatement(tableName));

        public static int TryDropTable(string dstConstr, string tableName)
        {
            using (SqlConnection dropConn = new SqlConnection(dstConstr))
            using (SqlCommand dropCmd = dropConn.CreateCommand())
            {
                dropConn.Open();
                return Helpers.TryExecute(dropCmd, GetDropTableStatement(tableName));
            }
        }

        /// <summary>
        /// Drops the supplied tables if they exist, best-effort, on a new connection.
        /// </summary>
        public static void DropTables(string dstConstr, params string[] tableNames)
        {
            try
            {
                using SqlConnection dropConn = new SqlConnection(dstConstr);
                dropConn.Open();

                using SqlCommand dropCmd = dropConn.CreateCommand();
                foreach (string tableName in tableNames)
                {
                    DropTable(dropCmd, tableName);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Tables could not be dropped: {e.Message}");
            }
        }

        private static string GetDropTableStatement(string tableName) =>
            $"IF (OBJECT_ID('{tableName.Replace("'", "''")}') IS NOT NULL) DROP TABLE {tableName}";

        public static int TryExecute(DbCommand cmd, string strText)
        {
            cmd.CommandText = strText;
            return cmd.ExecuteNonQuery();
        }

        public static int ExecuteNonQueryAzure(string strConnectionString, string strCommand, int commandTimeout = 60)
        {
            using (SqlConnection connection = new SqlConnection(strConnectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                // We need to increase CommandTimeout else you might see the following error:
                // "Timeout expired. The timeout period elapsed prior to completion of the operation or the server is not responding."
                command.CommandTimeout = commandTimeout;
                return Helpers.TryExecute(command, strCommand);
            }
        }

        public static bool VerifyResults(DbConnection conn, string dstTable, int expectedColumns, int expectedRows)
        {
            using (DbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = "select * from " + dstTable + "; select count(*) from " + dstTable;
                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    int numColumns = reader.FieldCount;
                    reader.NextResult();
                    reader.Read();
                    int numRows = (int)reader[0];
                    reader.Close();

                    DataTestUtility.AssertEqualsWithDescription(expectedColumns, numColumns, "Unexpected number of columns.");
                    DataTestUtility.AssertEqualsWithDescription(expectedRows, numRows, "Unexpected number of rows.");
                }
            }
            return false;
        }

        public static bool CheckTableRows(DbConnection conn, string table, bool shouldHaveRows)
        {
            string query = "select * from " + table;
            using (DbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = query;
                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    DataTestUtility.AssertEqualsWithDescription(shouldHaveRows, reader.HasRows, "Unexpected value for HasRows.");
                }
            }
            return false;
        }
    }
}
