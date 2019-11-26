using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SqlBulkCopyTruncation : IDisposable
    {
        private const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA_256";
        private readonly X509Certificate2 certificate;
        private ColumnMasterKey columnMasterKey;
        private ColumnEncryptionKey columnEncryptionKey;
        private SqlColumnEncryptionCertificateStoreProvider certStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();
        protected List<DbObject> databaseObjects = new List<DbObject>();
        private string _tabIntSource = "TabIntSource";
        private string _tabIntSourceDirect = "TabIntSourceDirect";
        private string _tabIntTargetDirect = "TabIntTargetDirect";
        private string _tabDatetime2Source = "TabDatetime2Source";
        private string _tabDatetime2Target = "TabDatetime2Target";
        private string _tabDecimalSource = "TabDecimalSource";
        private string _tabDecimalTarget = "TabDecimalTarget";
        private string _tabVarCharSmallSource = "TabVarCharSmallSource";
        private string _tabVarCharTarget = "TabVarCharTarget";
        private string _tabVarCharMaxSource = "TabVarCharMaxSource";
        private string _tabVarCharMaxTarget = "TabVarCharMaxTarget";
        private string _tabNVarCharSmallSource = "TabNVarCharSmallSource";
        private string _tabNVarCharSmallTarget = "TabNVarCharSmallTarget";
        private string _tabNVarCharMaxSource = "TabNVarCharMaxSource";
        private string _tabNVarCharTarget = "TabNVarCharTarget";
        private string _tabVarBinaryMaxSource = "TabVarBinaryMaxSource";
        private string _tabVarBinaryTarget = "TabVarBinaryTarget";
        private string _tabBinaryMaxSource = "TabBinaryMaxSource";
        private string _tabBinaryTarget = "TabBinaryTarget";
        private string _tabSmallBinarySource = "TabSmallBinarySource";
        private string _tabSmallBinaryTarget = "TabSmallBinaryTarget";
        private string _tabSmallBinaryMaxTarget = "TabSmallBinaryMaxTarget";
        private string _tabSmallCharSource = "TabSmallCharSource";
        private string _tabSmallCharTarget = "TabSmallCharTarget";
        private string _tabSmallCharMaxTarget = "TabSmallCharMaxTarget";
        private string _tabTinyIntTarget = "TabTinyIntTarget";


        public SqlBulkCopyTruncation()
        {
            certificate = CertificateUtility.CreateCertificate();
            columnMasterKey = new CspColumnMasterKey(DatabaseHelper.GenerateUniqueName("CMK"), certificate.Thumbprint, certStoreProvider, DataTestUtility.EnclaveEnabled);
            databaseObjects.Add(columnMasterKey);

            columnEncryptionKey = new ColumnEncryptionKey(DatabaseHelper.GenerateUniqueName("CEK"),
                                                          columnMasterKey,
                                                          certStoreProvider);
            databaseObjects.Add(columnEncryptionKey);

            foreach (string connectionStr in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connectionStr))
                {
                    sqlConnection.Open();

                    databaseObjects.ForEach(o => o.Create(sqlConnection));

                }
            }

            // Define unique name for each table on each test run.
            _tabBinaryMaxSource += "-" + Guid.NewGuid().ToString();
            _tabBinaryTarget += "-" + Guid.NewGuid().ToString();
            _tabDatetime2Source += "-" + Guid.NewGuid().ToString();
            _tabDatetime2Target += "-" + Guid.NewGuid().ToString();
            _tabDecimalSource += "-" + Guid.NewGuid().ToString();
            _tabDecimalTarget += "-" + Guid.NewGuid().ToString();
            _tabIntSource += "-" + Guid.NewGuid().ToString();
            _tabIntSourceDirect += "-" + Guid.NewGuid().ToString();
            _tabIntTargetDirect += "-" + Guid.NewGuid().ToString();
            _tabNVarCharMaxSource += "-" + Guid.NewGuid().ToString();
            _tabNVarCharSmallSource += "-" + Guid.NewGuid().ToString();
            _tabNVarCharSmallTarget += "-" + Guid.NewGuid().ToString();
            _tabNVarCharTarget += "-" + Guid.NewGuid().ToString();
            _tabSmallBinaryMaxTarget += "-" + Guid.NewGuid().ToString();
            _tabSmallBinarySource += "-" + Guid.NewGuid().ToString();
            _tabSmallBinaryTarget += "-" + Guid.NewGuid().ToString();
            _tabSmallCharMaxTarget += "-" + Guid.NewGuid().ToString();
            _tabSmallCharSource += "-" + Guid.NewGuid().ToString();
            _tabSmallCharTarget += "-" + Guid.NewGuid().ToString();
            _tabVarBinaryMaxSource += "-" + Guid.NewGuid().ToString();
            _tabVarBinaryTarget += "-" + Guid.NewGuid().ToString();
            _tabVarCharMaxSource += "-" + Guid.NewGuid().ToString();
            _tabVarCharMaxTarget += "-" + Guid.NewGuid().ToString();
            _tabVarCharSmallSource += "-" + Guid.NewGuid().ToString();
            _tabVarCharTarget += "-" + Guid.NewGuid().ToString();
            _tabTinyIntTarget += "-" + Guid.NewGuid().ToString();


            //Destroy any existing table
            Dispose();

            //Create all needed tables
            CreateTables();

            //populate tables
            PopulateTables();
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyTestsInt(string connectionString)
        {
            Assert.Throws<InvalidOperationException>(() => DoBulkCopy(_tabIntSource,_tabTinyIntTarget, connectionString));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest1(string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SilentRunCommand($@"TRUNCATE TABLE [{_tabIntTargetDirect}]", connection);
            }

            DoBulkCopyDirect(_tabIntSourceDirect,_tabIntTargetDirect, connectionString, true, true);

            VerifyTablesEqual(_tabIntSourceDirect, _tabIntTargetDirect, connectionString);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest2(String connectionString)
        {

            // Test case when source is enabled and target are disabled
            // Expected to fail with casting error (client will attempt to cast int to varbinary)
            Assert.Throws<InvalidOperationException>(() => { DoBulkCopyDirect(_tabIntSourceDirect, _tabIntTargetDirect, connectionString, true, false); });
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void DirectInsertTest3(string connectionString)
        {
            // Clean up target table (just in case)
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SilentRunCommand($@"TRUNCATE TABLE [dbo].[{_tabIntTargetDirect}]", connection);
            }

            Assert.Throws<InvalidOperationException>(() => { DoBulkCopyDirect(_tabIntSourceDirect, _tabIntTargetDirect, connectionString, false, true); });
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyDatetime2Tests(string connectionString)
        {
            DoBulkCopy(_tabDatetime2Source, _tabDatetime2Target, connectionString);
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand($@"SELECT [c2] from [dbo].[{_tabDatetime2Target}]", connection))
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
            Assert.Throws<InvalidOperationException>(() => DoBulkCopy(_tabDecimalSource, _tabDecimalTarget, connectionString));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyVarchar(string connectionString)
        {
            DoBulkCopy(_tabVarCharSmallSource,_tabVarCharTarget, connectionString);

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand($@"SELECT [c2] from [dbo].[{_tabVarCharTarget}]", connection))
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
            DoBulkCopy(_tabVarCharMaxSource,_tabVarCharMaxTarget, connectionString);

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand($@"SELECT [c2] from [{_tabVarCharMaxTarget}]", connection, null, SqlCommandColumnEncryptionSetting.Enabled))
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
            Assert.Throws<InvalidOperationException>(() => DoBulkCopy(_tabNVarCharMaxSource, _tabNVarCharTarget, connectionString));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyNVarcharMax(string connectionString)
        {
            // Will fail (NVarchars are not truncated)!
            Assert.Throws<InvalidOperationException>(() => DoBulkCopy(_tabNVarCharMaxSource,_tabNVarCharTarget, connectionString));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void BulkCopyBinaryMax(string connectionString)
        {
            // Will fail (NVarchars are not truncated)!
            DoBulkCopy(_tabBinaryMaxSource, _tabBinaryTarget, connectionString);

            // Verify the target column has (infact) the truncated value
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = new SqlCommand($@"SELECT c2 from [{_tabBinaryTarget}]", connection, null, SqlCommandColumnEncryptionSetting.Enabled))
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
            DoBulkCopy($"{_tabSmallCharSource}", $"{_tabSmallCharTarget}", connectionString);

            // Verify the truncated value
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand($@"SELECT c2 from [{_tabSmallCharTarget}]", conn))
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

            DoBulkCopy($@"{_tabSmallCharSource}", $@"{_tabSmallCharMaxTarget}", connectionString);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand($@"SELECT c2 from [{_tabSmallCharMaxTarget}]", conn))
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
            using (SqlConnection bulkCopyConnection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand($@"SELECT [c1], [c2] FROM [dbo].[{sourceTable}]",
                    connection: connection,
                    transaction: null,
                    columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        SqlBulkCopy copy = new SqlBulkCopy(connectionString);
                        copy.EnableStreaming = true;
                        copy.DestinationTableName = "[dbo].[" + targetTable + "]";
                        copy.WriteToServer(reader);
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
                using (SqlCommand cmd = new SqlCommand($@"SELECT [c1], [c2] FROM [{sourceTable}]", connSource))
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

                    using (SqlCommand cmdSource = new SqlCommand($@"SELECT [c1], [c2] FROM [{_tabIntSourceDirect}] ORDER BY c1", connSource))
                    using (SqlCommand cmdTarget = new SqlCommand($@"SELECT [c1], [c2] FROM [{_tabIntSourceDirect}] ORDER BY c1", connTarget))
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

        internal void PopulateTables()
        {
            foreach (string connectionString in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    //int
                    ExecuteQuery(connection, $@"INSERT INTO [dbo].[{_tabIntSource}] ([c1],[c2]) VALUES (1,300);");

                    //TabIntSourceDirect
                    using (SqlCommand cmd = new SqlCommand($@"INSERT INTO [dbo].[{_tabIntSourceDirect}] ([c1],[c2]) VALUES (@c1, @c2);",
                        connection: connection,
                        transaction: null,
                        columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))

                    {
                        cmd.Parameters.AddWithValue(@"@c1", 1);

                        SqlParameter paramC2 = cmd.CreateParameter();
                        paramC2.ParameterName = @"@c2";
                        paramC2.DbType = DbType.Int32;
                        paramC2.Value = -500;
                        paramC2.Precision = 3;
                        cmd.Parameters.Add(paramC2);

                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception) { }
                    }

                    // Datetime2(6)
                    ExecuteQuery(connection, $@"INSERT INTO [dbo].[{_tabDatetime2Source}] ([c1],[c2])   VALUES (1, '1968-10-23 12:45:37.123456');");

                    // Decimal(10,4)
                    ExecuteQuery(connection, $@"INSERT INTO [dbo].[{_tabDecimalSource}] ([c1],[c2])  VALUES (1,12345.6789);");

                    // Varchar(10)
                    ExecuteQuery(connection, $@"INSERT INTO [dbo].[{_tabVarCharSmallSource}]  ([c1],[c2])  VALUES (1,'abcdefghij');");

                    // Varchar(max)
                    ExecuteQuery(connection, $@"INSERT INTO [dbo].[{_tabVarCharMaxSource}] ([c1],[c2])  VALUES (1,'{new string('a', 8003)}');"); // 8003 is above the max fixedlen permissible size of 8000

                    // NVarchar(10)
                    ExecuteQuery(connection, $@"INSERT INTO [dbo].[{_tabNVarCharSmallSource}] ([c1],[c2])  VALUES (1,N'{new string('a', 10)}');");

                    // NVarchar(max)
                    ExecuteQuery(connection, $@"INSERT INTO [dbo].[{_tabNVarCharMaxSource}] ([c1],[c2])  VALUES (1,N'{new string('a', 4003)}');"); // 4003 is above the max fixedlen permissible size of 4000

                    // varbinary(max);
                    ExecuteQuery(connection, $@"INSERT INTO [dbo].[{_tabVarBinaryMaxSource}] ([c1],[c2])  VALUES (1, 0x{new string('e', 16004)});"); // this will bring varbinary size of 8002, above the fixedlen permissible size of 8000

                    // binary(7000)TabBinaryMaxSource
                    ExecuteQuery(connection, $@"INSERT INTO [dbo].[{_tabBinaryMaxSource}] ([c1],[c2])  VALUES (1, 0x{new string('e', 14000)});"); // size of 7000

                    // binary (3000)
                    ExecuteQuery(connection, $@"INSERT INTO [{_tabSmallBinarySource}] ([c1],[c2])  VALUES (1, 0x{new string('e', 6000)});"); // size of 3000

                    // char(8000)
                    ExecuteQuery(connection, $@"INSERT INTO [{_tabSmallCharSource}] ([c1],[c2])  VALUES (1, '{new string('a', 8000)}');");// size of 8000     
                }
            }
        }

        internal void CreateTables()
        {
            foreach (string connectionStr in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection connection = new SqlConnection(connectionStr))
                {
                    connection.Open();
                    // int -> tinyint
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabIntSource}] ([c1] [int] PRIMARY KEY, [c2] [int]);");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabTinyIntTarget}] ([c1] [int] PRIMARY KEY, [c2] [TINYINT] ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY=[{columnEncryptionKey.Name}], ENCRYPTION_TYPE  = RANDOMIZED, algorithm='{ColumnEncryptionAlgorithmName}'));");

                    // Tables for direct insert using bulk copy
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabIntSourceDirect}] ([c1] [int] PRIMARY KEY, [c2] [int] ENCRYPTED WITH (column_encryption_key = [{columnEncryptionKey.Name}], encryption_type = RANDOMIZED, algorithm='{ColumnEncryptionAlgorithmName}'));");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabIntTargetDirect}] ([c1] [int] PRIMARY KEY, [c2] [int] ENCRYPTED WITH (column_encryption_key = [{columnEncryptionKey.Name}], encryption_type = RANDOMIZED, algorithm='{ColumnEncryptionAlgorithmName}'));");

                    // Datetime2(6)->Datetime2(2)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabDatetime2Source}]([c1] [int]  PRIMARY KEY, [c2] datetime2(6));");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabDatetime2Target}]([c1] [INT]  PRIMARY KEY, [c2] datetime2(2) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    // Decimal(10, 4) -> Decimal (5,2) (tests scale and precision)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabDecimalSource}] ([c1] [int]  PRIMARY KEY, [c2] [decimal](10,4));");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabDecimalTarget}] ([c1] [int]  PRIMARY KEY, [c2] [decimal](5,2) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'));");

                    // varchar(10)->varchar(2)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabVarCharSmallSource}] ([c1] [int]  PRIMARY KEY, [c2] [VARCHAR](10));");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabVarCharTarget}] ([c1] [int] PRIMARY KEY, [c2] [VARCHAR](2) COLLATE Latin1_General_BIN2  encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    // varchar(max)->varchar(7000)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabVarCharMaxSource}]([c1] int primary key, [c2] [varchar](max))");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabVarCharMaxTarget}] ([c1] [int] primary key, [c2] [varchar](7000) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    // nvarchar(10)->nvarchar(2)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabNVarCharSmallSource}] (c1 int primary key, c2 nvarchar(10))");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabNVarCharSmallTarget}](c1 int primary key, c2 nvarchar(2) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    // nvarchar(max)->nvarchar(4000)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabNVarCharMaxSource}] ([c1] int primary key, [c2] [nvarchar](max))");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabNVarCharTarget}] ([c1] int primary key, [c2] [nvarchar](4000) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    // varbinary(max)->varbinary(3000)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabVarBinaryMaxSource}] (c1 int primary key, c2 varbinary(max))");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabVarBinaryTarget}] (c1 int primary key, c2 varbinary(3000) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    // binary(7000) -> binary (3000)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabBinaryMaxSource}](c1 int primary key, c2 binary(7000))");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabBinaryTarget}](c1 int primary key, c2 binary(3000) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    // binary(3000)-> binary(8000) and varbinary(max)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabSmallBinarySource}] (c1 int primary key, c2 binary(3000))");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabSmallBinaryTarget}] (c1 int primary key, c2 binary(8000) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabSmallBinaryMaxTarget}](c1 int primary key, c2 varbinary(max) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    // char(8000)->char(3000) and varchar(max)
                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabSmallCharSource}](c1 int primary key, c2 char(8000))");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabSmallCharTarget}] (c1 int primary key, c2 char(3000) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");

                    ExecuteQuery(connection, $@"CREATE TABLE [dbo].[{_tabSmallCharMaxTarget}] (c1 int primary key, c2 varchar(max) encrypted with (column_encryption_key=[{columnEncryptionKey.Name}], encryption_type=randomized, algorithm='{ColumnEncryptionAlgorithmName}'))");
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

        public void Dispose()
        {
            foreach (string connectionStr in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connectionStr))
                {
                    sqlConnection.Open();

                    SilentRunCommand($@"DROP TABLE [{_tabIntSource}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabIntSourceDirect}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabIntTargetDirect}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabBinaryTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabVarCharSmallSource}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabVarCharTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabDecimalSource}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabDecimalTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabDatetime2Source}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabDatetime2Target}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabNVarCharTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabNVarCharMaxSource}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabVarCharMaxSource}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabVarCharMaxTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabNVarCharSmallTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabVarBinaryMaxSource}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabVarBinaryTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabBinaryMaxSource}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabSmallBinarySource}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabSmallBinaryTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabSmallBinaryMaxTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabSmallCharSource}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabSmallCharTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE [{_tabSmallCharMaxTarget}]", sqlConnection);

                    SilentRunCommand($@"DROP TABLE[{_tabTinyIntTarget}]", sqlConnection);
                }
            }
        }
    }
}
