using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    [PlatformSpecific(TestPlatforms.Windows)]
    public class SqlBulkCopyTruncation : IClassFixture<SQLSetupStrategyCertStoreProvider>, IDisposable
    {
        private SQLSetupStrategyCertStoreProvider _fixture;

        private readonly Dictionary<string, string> tableNames = new Dictionary<string, string>();

        public SqlBulkCopyTruncation(SQLSetupStrategyCertStoreProvider fixture)
        {
            this._fixture = fixture;
            this.tableNames = fixture.sqlBulkTruncationTableNames;

            PopulateTables();

        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyTestsInt(string connectionString)
        {
            Assert.Throws<InvalidOperationException>(() => DoBulkCopy(tableNames["TabIntSource"], tableNames["TabTinyIntTarget"], connectionString));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest1(string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SilentRunCommand($@"TRUNCATE TABLE [{tableNames["TabIntTargetDirect"]}]", connection);
            }

            DoBulkCopyDirect(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString, true, true);

            VerifyTablesEqual(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest2(String connectionString)
        {

            // Test case when source is enabled and target are disabled
            // Expected to fail with casting error (client will attempt to cast int to varbinary)
            Assert.Throws<InvalidOperationException>(() => { DoBulkCopyDirect(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString, true, false); });
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest3(string connectionString)
        {
            // Clean up target table (just in case)
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SilentRunCommand($@"TRUNCATE TABLE [dbo].[{tableNames["TabIntTargetDirect"]}]", connection);
            }

            Assert.Throws<InvalidOperationException>(() => { DoBulkCopyDirect(tableNames["TabIntSourceDirect"], tableNames["TabIntTargetDirect"], connectionString, false, true); });
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyDatetime2Tests(string connectionString)
        {
            DoBulkCopy(tableNames["TabDatetime2Source"], tableNames["TabDatetime2Target"], connectionString);
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand($@"SELECT [c2] from [dbo].[{tableNames["TabDatetime2Target"]}]", connection))
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

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyDecimal(string connectionString)
        {
            Assert.Throws<InvalidOperationException>(() => DoBulkCopy(tableNames["TabDecimalSource"], tableNames["TabDecimalTarget"], connectionString));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyVarchar(string connectionString)
        {
            DoBulkCopy(tableNames["TabVarCharSmallSource"], tableNames["TabVarCharTarget"], connectionString);

            using (SqlConnection connection = new SqlConnection(connectionString))
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

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyVarcharMax(string connectionString)
        {
            DoBulkCopy(tableNames["TabVarCharMaxSource"],tableNames["TabVarCharMaxTarget"], connectionString);

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

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyNVarchar(string connectionString)
        {
            // Will fail (NVarchars are not truncated)!
            Assert.Throws<InvalidOperationException>(() => DoBulkCopy(tableNames["TabNVarCharMaxSource"], tableNames["TabNVarCharTarget"], connectionString));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyNVarcharMax(string connectionString)
        {
            // Will fail (NVarchars are not truncated)!
            Assert.Throws<InvalidOperationException>(() => DoBulkCopy(tableNames["TabNVarCharMaxSource"], tableNames["TabNVarCharTarget"], connectionString));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyBinaryMax(string connectionString)
        {
            // Will fail (NVarchars are not truncated)!
            DoBulkCopy(tableNames["TabBinaryMaxSource"], tableNames["TabBinaryTarget"], connectionString);

            // Verify the target column has (infact) the truncated value
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand($@"SELECT c2 from [{tableNames["TabBinaryTarget"]}]", connection, null, SqlCommandColumnEncryptionSetting.Enabled))
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

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopySmallChar(string connectionString)
        {
            // should succeed!
            DoBulkCopy($"{tableNames["TabSmallCharSource"]}", $"{tableNames["TabSmallCharTarget"]}", connectionString);

            // Verify the truncated value
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand($"SELECT [c2] from [{tableNames["TabSmallCharTarget"]}]", conn))
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

            DoBulkCopy($@"{tableNames["TabSmallCharSource"]}", $@"{tableNames["TabSmallCharMaxTarget"]}", connectionString);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand($"SELECT c2 from [{tableNames["TabSmallCharMaxTarget"]}]", conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            string columnValue = reader.GetString(0);
                            Assert.True(columnValue.Equals(new string('a', 8000)), "Unexpected value read");
                        }
                    }
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
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlConnection bulkCopyConnection = new SqlConnection(connectionString))
                {
                    bulkCopyConnection.Open();
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand($"SELECT [c1], [c2] FROM [{sourceTable}]",
                        connection: connection,
                        transaction: null,
                        columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            SqlBulkCopy copy = new SqlBulkCopy(bulkCopyConnection);
                            copy.EnableStreaming = true;
                            copy.DestinationTableName = "[" + targetTable + "]";
                            copy.WriteToServer(reader);
                        }
                    }
                }
            }
        }

        internal void DoBulkCopyDirect(string sourceTable, string targetTable, string connectionString, bool isEnclaveActivatedOnTarget, bool isEnclaveActivatedOnSource)
        {
            SqlConnectionStringBuilder trgBuilder = new SqlConnectionStringBuilder(connectionString);
            trgBuilder.ColumnEncryptionSetting = isEnclaveActivatedOnTarget ? SqlConnectionColumnEncryptionSetting.Enabled : SqlConnectionColumnEncryptionSetting.Disabled;

            SqlConnectionStringBuilder srcBuilder = new SqlConnectionStringBuilder(connectionString);
            srcBuilder.ColumnEncryptionSetting = isEnclaveActivatedOnSource ? SqlConnectionColumnEncryptionSetting.Enabled : SqlConnectionColumnEncryptionSetting.Disabled;
            if (isEnclaveActivatedOnSource is false)
            {
                srcBuilder.AttestationProtocol = SqlConnectionAttestationProtocol.NotSpecified;
            }

            using (SqlConnection connSource = new SqlConnection(srcBuilder.ToString()))
            {
                connSource.Open();
                using (SqlCommand cmd = new SqlCommand($"SELECT [c1], [c2] FROM [{sourceTable}]", connSource))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        SqlBulkCopy copy = new SqlBulkCopy(trgBuilder.ToString());
                        copy.EnableStreaming = true;
                        copy.DestinationTableName = targetTable;
                        copy.WriteToServer(reader);
                    }
                }
            }
        }

        internal void VerifyTablesEqual(string sourceTable, string targetTable, string connectionString)
        {
            using (SqlConnection connSource = new SqlConnection(connectionString))
            {
                connSource.Open();

                using (SqlConnection connTarget = new SqlConnection(connectionString))
                {
                    connTarget.Open();

                    using (SqlCommand cmdSource = new SqlCommand($"SELECT [c1], [c2] FROM [{tableNames["TabIntSourceDirect"]}] ORDER BY c1", connSource))
                    using (SqlCommand cmdTarget = new SqlCommand($"SELECT [c1], [c2] FROM [{tableNames["TabIntSourceDirect"]}] ORDER BY c1", connTarget))
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

        internal void PopulateTables()
        {
            foreach(string connString  in DataTestUtility.AEConnStrings)
            {
                using(SqlConnection connection = new SqlConnection(connString))
                {
                    connection.Open();

                    ExecuteQuery(connection, $"INSERT INTO [{tableNames["TabIntSource"]}] ([c1],[c2]) VALUES(2, 300)");

                    using(SqlCommand cmd = new SqlCommand($@"INSERT INTO [{tableNames["TabIntSourceDirect"]}] VALUES (1, @c2)", connection))
                    {
                        SqlParameter paramC2 = cmd.CreateParameter();
                        paramC2.ParameterName = @"@c2";
                        paramC2.DbType = DbType.Int32;
                        paramC2.Direction = ParameterDirection.Input;
                        paramC2.Precision = 3;
                        paramC2.Value = -500;
                        cmd.Parameters.Add(paramC2);

                        cmd.ExecuteNonQuery();
                    }

                    // Datetime2(6)
                    ExecuteQuery(connection,
                        $@"INSERT INTO [{tableNames["TabDatetime2Source"]}] VALUES (1, '1968-10-23 12:45:37.123456')");

                    // Decimal(10,4)
                    ExecuteQuery(connection,
                        $@"INSERT INTO [{tableNames["TabDecimalSource"]}] VALUES (1,12345.6789)");

                    // Varchar(10)
                    ExecuteQuery(connection,
                        $@"INSERT INTO [{tableNames["TabVarCharSmallSource"]}] VALUES (1,'abcdefghij')");

                    // Varchar(max)
                    ExecuteQuery(connection,
                        $@"INSERT INTO [{tableNames["TabVarCharMaxSource"]}] VALUES (1,'{new string('a', 8003)}')"); // 8003 is above the max fixedlen permissible size of 8000

                    // NVarchar(10)
                    ExecuteQuery(connection,
                        $@"INSERT INTO [{tableNames["TabNVarCharSmallSource"]}] VALUES (1,N'{new string('a', 10)}')");

                    // NVarchar(max)
                    ExecuteQuery(connection,
                       $@"INSERT INTO [{tableNames["TabNVarCharMaxSource"]}] VALUES (1,N'{new string('a', 4003)}')"); // 4003 is above the max fixedlen permissible size of 4000

                    // varbinary(max)
                    ExecuteQuery(connection,
                        $@"INSERT INTO [{tableNames["TabVarBinaryMaxSource"]}] VALUES (1, 0x{new string('e', 16004)})"); // this will bring varbinary size of 8002, above the fixedlen permissible size of 8000

                    // binary(7000)TabBinaryMaxSource
                    ExecuteQuery(connection,
                        $@"INSERT INTO [{tableNames["TabBinaryMaxSource"]}] VALUES (1, 0x{new string('e', 14000)})"); // size of 7000

                    // binary (3000)
                    ExecuteQuery(connection,
                        $@"INSERT INTO [{tableNames["TabSmallBinarySource"]}] VALUES (1, 0x{new string('e', 6000)})"); // size of 3000

                    // char(8000)
                    ExecuteQuery(connection,
                        $@"INSERT INTO [{tableNames["TabSmallCharSource"]}] VALUES (1, '{new string('a', 8000)}')"); // size of 8000  
                }
            }
        }

        public void Dispose()
        {
            foreach (string connection in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connection))
                {
                    sqlConnection.Open();
                    foreach (string val in tableNames.Values)
                    {
                        Table.DeleteData(val, sqlConnection);
                    }
                }
            }
        }
    }
}
