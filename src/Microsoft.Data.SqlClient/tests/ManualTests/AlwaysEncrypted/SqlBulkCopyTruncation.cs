// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SqlBulkCopyTruncation : IClassFixture<PlatformSpecificTestContext>
    {
        private SQLSetupStrategy _fixture;

        private readonly Dictionary<string, string> tableNames = new Dictionary<string, string>();

        public SqlBulkCopyTruncation(PlatformSpecificTestContext context)
        {
            _fixture = context.Fixture;
            tableNames = _fixture.sqlBulkTruncationTableNames;
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyTestsInt(string connectionString)
        {
            PopulateTable("TabIntSource", "1, 300", connectionString);

            try
            {
                Assert.Throws<InvalidOperationException>(() => DoBulkCopy(tableNames["TabIntSource"], tableNames["TabTinyIntTarget"], connectionString));
            }
            finally
            {
                //Truncate removes the data but not the table.
                TruncateTables("TabIntSource", "TabTinyIntTarget", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest1(string connectionString)
        {
            try
            {
                //Populate table TabIntSourceDirect with parameters @c1,@c2
                using (SqlConnection connection = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand($@"INSERT INTO [dbo].[{tableNames["TabIntSourceDirect"]}] VALUES (@c1, @c2)",
                        connection,
                        null,
                        SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        SqlParameter paramC1 = cmd.Parameters.AddWithValue(@"@c1", 1);

                        SqlParameter paramC2 = cmd.CreateParameter();
                        paramC2.ParameterName = @"@c2";
                        paramC2.DbType = DbType.Int32;
                        paramC2.Direction = ParameterDirection.Input;
                        paramC2.Precision = 3;
                        paramC2.Value = -500;
                        cmd.Parameters.Add(paramC2);

                        cmd.ExecuteNonQuery();
                    }
                }

                DoBulkCopyDirect(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString, true, true);

                VerifyTablesEqual(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString);
            }
            finally
            {
                //Truncate populated tables.
                TruncateTables("TabIntSourceDirect", "TabIntTargetDirect", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest2(string connectionString)
        {
            try
            {
                //Populate table TabIntSourceDirect with parameters @c1,@c2
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand($@"INSERT INTO [{tableNames["TabIntSourceDirect"]}] ([c1],[c2]) VALUES (@c1, @c2)", connection,
                        null,
                        SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        SqlParameter paramC1 = cmd.Parameters.AddWithValue(@"@c1", 2);

                        SqlParameter paramC2 = cmd.CreateParameter();
                        paramC2.ParameterName = @"@c2";
                        paramC2.DbType = DbType.Int32;
                        paramC2.Direction = ParameterDirection.Input;
                        paramC2.Precision = 3;
                        paramC2.Value = -500;
                        cmd.Parameters.Add(paramC2);

                        cmd.ExecuteNonQuery();

                        paramC1.Value = 3;
                        paramC2.Value = 32767;
                        cmd.ExecuteNonQuery();

                        paramC1.Value = 4;
                        paramC2.Value = 83717; // some random number
                        cmd.ExecuteNonQuery();
                    }
                }

                // Test case when source is enabled and target are disabled
                // Expected to fail with casting error (client will attempt to cast int to varbinary)
                Assert.Throws<InvalidOperationException>(() => { DoBulkCopyDirect(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString, true, false); });
            }
            finally
            {
                //Truncate populated tables.
                TruncateTables("TabIntSourceDirect", "TabIntTargetDirect", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest3(string connectionString)
        {
            try
            {
                //Populate table TabIntSourceDirect with parameters @c1,@c2 as Integers.
                using (SqlConnection connection = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand($@"INSERT INTO [{tableNames["TabIntSourceDirect"]}] ([c1],[c2]) VALUES (@c1, @c2)", connection))
                    {
                        SqlParameter paramC1 = cmd.Parameters.AddWithValue(@"@c1", 3);

                        SqlParameter paramC2 = cmd.CreateParameter();
                        paramC2.ParameterName = @"@c2";
                        paramC2.DbType = DbType.Int32;
                        paramC2.Direction = ParameterDirection.Input;
                        paramC2.Precision = 3;
                        paramC2.Value = -500;
                        cmd.Parameters.Add(paramC2);

                        cmd.ExecuteNonQuery();

                        paramC1.Value = 4;
                        paramC2.Value = 32767;
                        paramC2.Precision = 5;
                        cmd.ExecuteNonQuery();

                        paramC1.Value = 5;
                        paramC2.Value = 83717; // some random number
                        paramC2.Precision = 5;
                        cmd.ExecuteNonQuery();
                    }
                }

                Assert.Throws<InvalidOperationException>(() => { DoBulkCopyDirect(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString, false, true); });
            }
            finally
            {
                //Truncate populated tables.
                TruncateTables("TabIntSourceDirect", "TabIntTargetDirect", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest4(string connectionString)
        {
            try
            {
                //Populate table TabIntSourceDirect with parameters @c1,@c2
                using (SqlConnection connection = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand($@"INSERT INTO [{tableNames["TabIntSourceDirect"]}] ([c1],[c2]) VALUES (@c1, @c2)", connection))
                    {
                        SqlParameter paramC1 = cmd.Parameters.AddWithValue(@"@c1", 4);

                        SqlParameter paramC2 = cmd.CreateParameter();
                        paramC2.ParameterName = @"@c2";
                        paramC2.DbType = DbType.Int32;
                        paramC2.Direction = ParameterDirection.Input;
                        paramC2.Precision = 3;
                        paramC2.Value = -500;
                        cmd.Parameters.Add(paramC2);

                        cmd.ExecuteNonQuery();
                    }
                }
                // Test case when source and target are disabled
                DoBulkCopyDirect(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString, false, false);

                VerifyTablesEqual(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString);
            }
            finally
            {
                //Truncate populated tables.
                TruncateTables("TabIntSourceDirect", "TabIntTargetDirect", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyDatetime2Tests(string connectionString)
        {
            PopulateTable("TabDatetime2Source", @"1, '1968-10-23 12:45:37.123456'", connectionString);

            try
            {
                DoBulkCopy(tableNames["TabDatetime2Source"], tableNames["TabDatetime2Target"], connectionString);

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlConnection.ClearPool(connection);

                    using (SqlCommand cmd = new SqlCommand($@"SELECT [c2] from [dbo].[{tableNames["TabDatetime2Target"]}]", connection,
                        null,
                        SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        // Read the target table and verify the string was truncated indeed!
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime datetime = reader.GetDateTime(0);
                                TimeSpan timespan = datetime.TimeOfDay;

                                Assert.Equal(12, Convert.ToInt32(datetime.Hour));
                                Assert.Equal(45, Convert.ToInt32(datetime.Minute));
                                Assert.Equal(37, Convert.ToInt32(datetime.Second));

                                Assert.Equal(1968, Convert.ToInt32(datetime.Year));
                                Assert.Equal(10, Convert.ToInt32(datetime.Month));
                                Assert.Equal(23, Convert.ToInt32(datetime.Day));

                                // To verify the milliseconds, we need to look at the timespan
                                string millisec = timespan.ToString();
                                Assert.True(millisec.Equals("12:45:37.1200000"), "Unexpected milliseconds");
                            }
                        }
                    }
                }
            }
            finally
            {
                TruncateTables("TabDatetime2Source", "TabDatetime2Target", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyDecimal(string connectionString)
        {
            PopulateTable("TabDecimalSource", "1,12345.6789", connectionString);

            try
            {
                Assert.Throws<InvalidOperationException>(() => DoBulkCopy(tableNames["TabDecimalSource"], tableNames["TabDecimalTarget"], connectionString));
            }
            finally
            {
                //Truncate tables for next try
                TruncateTables("TabDecimalSource", "TabDecimalTarget", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyVarchar(string connectionString)
        {
            PopulateTable("TabVarCharSmallSource", @"1,'abcdefghij'", connectionString);

            try
            {
                DoBulkCopy(tableNames["TabVarCharSmallSource"], tableNames["TabVarCharTarget"], connectionString);

                using (SqlConnection connection = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                using (SqlCommand cmd = new SqlCommand($@"SELECT [c2] from [{tableNames["TabVarCharTarget"]}]", connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Assert.True(reader.GetString(0).Equals("ab"), "String was not truncated as expected!");
                        }
                    }
                }
            }
            finally
            {
                TruncateTables("TabVarCharSmallSource", "TabVarCharTarget", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyVarcharMax(string connectionString)
        {
            PopulateTable("TabVarCharMaxSource", $@"1,'{new string('a', 8003)}'", connectionString);

            try
            {
                DoBulkCopy(tableNames["TabVarCharMaxSource"], tableNames["TabVarCharMaxTarget"], connectionString);

                using (SqlConnection connection = new SqlConnection(connectionString))
                using (SqlCommand cmd = new SqlCommand($@"SELECT [c2] from [{tableNames["TabVarCharMaxTarget"]}]", connection, null, SqlCommandColumnEncryptionSetting.Enabled))
                {
                    connection.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Assert.True(reader.GetString(0).Equals(new string('a', 7000)), "String was not truncated as expected!");
                        }
                    }
                }
            }
            finally
            {
                TruncateTables("TabVarCharMaxSource", "TabVarCharMaxTarget", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyNVarchar(string connectionString)
        {
            PopulateTable("TabNVarCharMaxSource", $@"1, N'{new string('a', 4003)}'", connectionString);

            try
            {
                // Will fail (NVarchars are not truncated)!
                Assert.Throws<InvalidOperationException>(() => DoBulkCopy(tableNames["TabNVarCharMaxSource"], tableNames["TabNVarCharTarget"], connectionString));
            }
            finally
            {
                TruncateTables("TabNVarCharMaxSource", "TabNVarCharTarget", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyNVarcharMax(string connectionString)
        {
            PopulateTable("TabNVarCharMaxSource", $@"1,N'{new string('a', 4003)}'", connectionString);

            try
            {
                // Will fail (NVarchars are not truncated)!
                Assert.Throws<InvalidOperationException>(() => DoBulkCopy(tableNames["TabNVarCharMaxSource"], tableNames["TabNVarCharTarget"], connectionString));
            }
            finally
            {
                TruncateTables("TabNVarCharMaxSource", "TabNVarCharTarget", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyBinaryMax(string connectionString)
        {
            PopulateTable("TabBinaryMaxSource", $"1, 0x{new string('e', 14000)}", connectionString);

            try
            {
                // Will fail (NVarchars are not truncated)!
                DoBulkCopy(tableNames["TabBinaryMaxSource"], tableNames["TabBinaryTarget"], connectionString);

                // Verify the target column has (infact) the truncated value
                using (SqlConnection connection = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    connection.Open();

                    using (SqlCommand cmd = new SqlCommand($@"SELECT c2 from [{tableNames["TabBinaryTarget"]}]", connection))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                byte[] columnValue = (byte[])reader[0];
                                Assert.True(3000 == columnValue.Length, "Unexpected array length!");

                                foreach (byte b in columnValue)
                                {
                                    Assert.Equal(0xee, b);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                TruncateTables("TabBinaryMaxSource", "TabBinaryTarget", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopySmallBinary(string connectionString)
        {
            PopulateTable("TabSmallBinarySource", $"1, 0X{new string('e', 6000)}", connectionString);

            try
            {
                DoBulkCopy(tableNames["TabSmallBinarySource"], tableNames["TabSmallBinaryTarget"], connectionString);
                // Verify its 8000            
                using (SqlConnection conn = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand($"SELECT c2 from [{tableNames["TabSmallBinaryTarget"]}]", conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                byte[] columnValue = (byte[])reader[0];
                                int count = 0;
                                foreach (byte b in columnValue)
                                {
                                    // upto 3000 is as is and there after its padded
                                    if (count < 3000)
                                    {
                                        Assert.True(b == 0xee, "Unexpected value in TabSmallBinaryTarget!");
                                    }
                                    else
                                    {
                                        Assert.True(b == 0x00, "Unexpected value in TabSmallBinaryTarget!");
                                    }
                                    count++;
                                }
                            }
                        }
                    }
                }

                DoBulkCopy(tableNames["TabSmallBinarySource"], tableNames["TabSmallBinaryMaxTarget"], connectionString);

                // Verify its 3000
                using (SqlConnection conn = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand($"SELECT c2 from [{tableNames["TabSmallBinaryMaxTarget"]}]", conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                byte[] columnValue = (byte[])reader[0];
                                Assert.True(3000 == columnValue.Length, "Unexpected length for varbinary TabSmallBinaryMaxTarget!");
                                foreach (byte b in columnValue)
                                {
                                    Assert.True(0xee == b, "unexpected element read!");
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                TruncateTables("TabSmallBinarySource", "TabSmallBinaryTarget", connectionString);
                TruncateTables("TabSmallBinaryMaxTarget", "", connectionString);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopySmallChar(string connectionString)
        {
            PopulateTable("TabSmallCharSource", $@"1, '{new string('a', 8000)}'", connectionString);

            try
            {
                // should succeed!
                DoBulkCopy($"{tableNames["TabSmallCharSource"]}", $"{tableNames["TabSmallCharTarget"]}", connectionString);

                // Verify the truncated value
                using (SqlConnection connection = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    connection.Open();

                    using (SqlCommand cmd = new SqlCommand($@"SELECT c2 from [{tableNames["TabSmallCharTarget"]}]", connection))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string columnValue = reader.GetString(0);
                                Assert.True(columnValue.Equals(new string('a', 3000)), "Unexpected value read");
                            }
                        }
                    }
                }

                DoBulkCopy($"{tableNames["TabSmallCharSource"]}", $"{tableNames["TabSmallCharMaxTarget"]}", connectionString);

                using (SqlConnection conn = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand($"SELECT c2 from [{tableNames["TabSmallCharMaxTarget"]}]", conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var columnValue = reader.GetString(0);
                                Assert.True(columnValue.Equals(new string('a', 8000)), "Unexpected value read");
                            }
                        }
                    }
                }
            }
            finally
            {
                TruncateTables("TabSmallCharSource", "TabSmallCharTarget", connectionString);
                TruncateTables("TabSmallCharMaxTarget", "", connectionString);
            }
        }

        internal void TruncateTables(string sourceName, string targetName, string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                if (!string.IsNullOrEmpty(sourceName))
                {
                    SilentRunCommand($@"TRUNCATE TABLE [{tableNames[sourceName]}]", connection);
                }

                if (!string.IsNullOrEmpty(targetName))
                {
                    SilentRunCommand($@"TRUNCATE TABLE [{tableNames[targetName]}]", connection);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="commandText"></param>
        internal void ExecuteQuery(SqlConnection connection, string commandText)
        {
            using (SqlCommand cmd = new SqlCommand(commandText,
                connection: connection,
                transaction: null, columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
            {
                cmd.ExecuteNonQuery();
            }
        }

        internal void DoBulkCopy(string sourceTable, string targetTable, string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(GetOpenConnectionString(connectionString, true)))
            {
                connection.Open();

                using (SqlConnection bulkCopyConnection = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    bulkCopyConnection.Open();
                    using (SqlCommand cmd = new SqlCommand($"SELECT c1, c2 FROM [dbo].[{sourceTable}]", connection))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            using (SqlBulkCopy copy = new SqlBulkCopy(bulkCopyConnection))
                            {
                                copy.EnableStreaming = true;
                                copy.DestinationTableName = $"[dbo].[{targetTable}]";
                                copy.WriteToServer(reader);
                            }
                        }
                    }
                }
            }
        }

        internal void DoBulkCopyDirect(string sourceTable, string targetTable, string connectionString, bool isEncryptionEnabledOnSource, bool isEncryptionEnabledOnTarget)
        {
            using (SqlConnection connSource = new SqlConnection(GetOpenConnectionString(connectionString, isEncryptionEnabledOnSource)))
            {
                connSource.Open();
                using (SqlCommand cmd = new SqlCommand($"SELECT c1, c2 FROM [dbo].[{sourceTable}]", connSource))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            using (SqlBulkCopy copy = new SqlBulkCopy(GetOpenConnectionString(connectionString, isEncryptionEnabledOnTarget), SqlBulkCopyOptions.AllowEncryptedValueModifications))
                            {
                                copy.EnableStreaming = true;
                                copy.DestinationTableName = $"[{targetTable}]";
                                copy.WriteToServer(reader);
                            }
                        }
                    }
                }
            }
        }

        internal void VerifyTablesEqual(string sourceTable, string targetTable, string connectionString)
        {
            using (SqlConnection connSource = new SqlConnection(GetOpenConnectionString(connectionString, true)))
            {
                connSource.Open();

                using (SqlConnection connTarget = new SqlConnection(GetOpenConnectionString(connectionString, true)))
                {
                    connTarget.Open();

                    using (SqlCommand cmdSource = new SqlCommand($"SELECT [c1], [c2] FROM [{tableNames["TabIntSourceDirect"]}] ORDER BY c1", connSource))
                    using (SqlCommand cmdTarget = new SqlCommand($"SELECT [c1], [c2] FROM [{tableNames["TabIntTargetDirect"]}] ORDER BY c1", connTarget))
                    {
                        using (SqlDataReader sourceReader = cmdSource.ExecuteReader())
                        using (SqlDataReader targetReader = cmdTarget.ExecuteReader())
                        {
                            while (sourceReader.Read() && targetReader.Read())
                            {
                                Assert.True(sourceReader.GetInt32(0) == targetReader.GetInt32(0), "Index mismatch");
                                Assert.True(sourceReader.GetInt32(1) == targetReader.GetInt32(1), "Row mismatch");
                            }
                            sourceReader.Close();
                            targetReader.Close();
                        }
                    }
                }
            }
        }

        internal void SilentRunCommand(string commandText, SqlConnection connection)
        {
            // Execute the command and swallow all exceptions
            try
            {
                ExecuteQuery(connection, commandText);
            }
            catch (SqlException)
            {
                // Swallow!
            }
        }

        internal void PopulateTable(string tableName, string values, string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                ExecuteQuery(connection, $@"INSERT INTO [dbo].[{tableNames[tableName]}] values ({values})");
            }
        }

        public string GetOpenConnectionString(string connectionString, bool encryptionEnabled)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);

            if (encryptionEnabled)
            {
                builder.ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled;
            }
            else
            {
                builder.ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Disabled;
            }

            return builder.ToString();
        }
    }
}
