using System;
using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class ExceptionsGenericErrors : IClassFixture<ExceptionGenericErrorFixture>
    {
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE), nameof(DataTestUtility.IsNotAzureServer), Skip = "ActiveIssue 10036")]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TestCommandOptionWithNoTceFeature(string connectionString)
        {
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(connectionString);
            CertificateUtility.ChangeServerTceSetting(false, sb); // disable TCE on engine.
            using (SqlConnection conn = CertificateUtility.GetOpenConnection(false, sb, fSuppressAttestation: true))
            {
                using (SqlCommand cmd = new SqlCommand(ExceptionGenericErrorFixture.encryptedProcedureName, conn, null, SqlCommandColumnEncryptionSetting.Enabled))
                {
                    SqlParameter param = cmd.Parameters.AddWithValue("@c1", 2);
                    cmd.CommandType = CommandType.StoredProcedure;
                    string expectedErrorMessage = "SQL Server instance in use does not support column encryption.";
                    InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
                    Assert.Contains(expectedErrorMessage, e.Message);
                }
            }
            // Turn on TCE now
            CertificateUtility.ChangeServerTceSetting(true, sb); // enable tce
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE), nameof(DataTestUtility.IsNotAzureServer))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TestDataAdapterAndEncryptionSetting(string connectionString)
        {
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(connectionString);
            // Create a new SqlCommand for select and delete
            using (SqlConnection conn = CertificateUtility.GetOpenConnection(false, sb))
            {
                using (SqlCommand cmdInsert = new SqlCommand(ExceptionGenericErrorFixture.encryptedProcedureName, conn, null, SqlCommandColumnEncryptionSetting.Enabled))
                using (SqlCommand cmdDelete = new SqlCommand($"delete {ExceptionGenericErrorFixture.encryptedTableName} where c1 = @c1", conn, null, SqlCommandColumnEncryptionSetting.Disabled))
                using (SqlDataAdapter adapter = new SqlDataAdapter($"select c1 from {ExceptionGenericErrorFixture.encryptedTableName}", conn))
                {
                    cmdInsert.CommandType = CommandType.StoredProcedure;
                    cmdInsert.Parameters.Add("@c1", SqlDbType.Int, 4, "c1");
                    cmdInsert.UpdatedRowSource = UpdateRowSource.None;
                    cmdDelete.Parameters.Add("@c1", SqlDbType.Int, 4, "c1");
                    cmdDelete.UpdatedRowSource = UpdateRowSource.None;
                    adapter.InsertCommand = cmdInsert;
                    adapter.DeleteCommand = cmdDelete;

                    DataSet dataset = new DataSet();
                    adapter.Fill(dataset);
                    DataTable table = dataset.Tables[0];
                    foreach (DataRow row in table.Rows)
                    {
                        row.Delete();
                    }
                    DataRow rowInserted = table.NewRow();
                    rowInserted["c1"] = 5;
                    table.Rows.Add(rowInserted);
                    adapter.UpdateBatchSize = 0; // remove batch size limit
                                                 // run batch update

                    string expectedErrorMessage = "SqlCommandColumnEncryptionSetting should be identical on all commands (SelectCommand, InsertCommand, UpdateCommand, DeleteCommand) when doing batch updates.";
                    InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => adapter.Update(dataset));
                    Assert.Contains(expectedErrorMessage, e.Message);
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE), nameof(DataTestUtility.IsNotAzureServer))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TestInvalidForceColumnEncryptionSetting(string connectionString)
        {
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(connectionString);
            using (SqlConnection conn = CertificateUtility.GetOpenConnection(false, sb))
            {
                using (SqlCommand cmd = new SqlCommand(ExceptionGenericErrorFixture.encryptedProcedureName, conn))
                {
                    SqlParameter param = cmd.Parameters.AddWithValue("@c1", 2);
                    param.ForceColumnEncryption = true;
                    cmd.CommandType = CommandType.StoredProcedure;
                    string expectedErrorMessage = $"Cannot set ForceColumnEncryption(true) for SqlParameter '@c1' because encryption is not enabled for the statement or procedure '{ExceptionGenericErrorFixture.encryptedProcedureName}'.";
                    InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
                    Assert.Contains(expectedErrorMessage, e.Message);
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE), nameof(DataTestUtility.IsNotAzureServer))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TestParamUnexpectedEncryptionMD(string connectionString)
        {
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(connectionString);
            using (SqlConnection conn = CertificateUtility.GetOpenConnection(true, sb))
            {
                using (SqlCommand cmd = new SqlCommand(ExceptionGenericErrorFixture.encryptedProcedureName, conn))
                {
                    SqlParameter param = cmd.Parameters.AddWithValue("@c1", 2);
                    param.ForceColumnEncryption = true;
                    cmd.CommandType = CommandType.StoredProcedure;
                    string expectedErrorMessage = $"Cannot execute statement or procedure '{ExceptionGenericErrorFixture.encryptedProcedureName}' because ForceColumnEncryption(true) was set for SqlParameter '@c1' and the database expects this parameter to be sent as plaintext. This may be due to a configuration error.";
                    InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
                    Assert.Contains(expectedErrorMessage, e.Message);
                }
            }
        }
    }

    public class ExceptionGenericErrorFixture : IDisposable
    {
        static public string encryptedTableName;
        static public string encryptedProcedureName;

        public ExceptionGenericErrorFixture()
        {
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;
            CreateAndPopulateSimpleTable();
        }

        private void CreateAndPopulateSimpleTable()
        {
            encryptedTableName = DatabaseHelper.GenerateUniqueName("encrypted");
            encryptedProcedureName = DatabaseHelper.GenerateUniqueName("encrypted");
            foreach (string connectionStr in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection conn = CertificateUtility.GetOpenConnection(false, new SqlConnectionStringBuilder(connectionStr)))
                {
                    using (SqlCommand cmdCreate = new SqlCommand($"create table {encryptedTableName}(c1 int)", conn))
                    {
                        cmdCreate.CommandType = CommandType.Text;
                        cmdCreate.ExecuteNonQuery();
                    }
                    using (SqlCommand cmdInsert = new SqlCommand($"insert into {encryptedTableName} values(1)", conn))
                    {
                        cmdInsert.CommandType = CommandType.Text;
                        cmdInsert.ExecuteNonQuery();
                    }
                    using (SqlCommand cmdCreateProc = new SqlCommand($"create procedure {encryptedProcedureName}(@c1 int) as insert into {encryptedTableName} values (@c1)", conn))
                    {
                        cmdCreateProc.CommandType = CommandType.Text;
                        cmdCreateProc.ExecuteNonQuery();
                    }
                }
            }
        }

        public void Dispose()
        {
            // Do NOT remove certificate for concurrent consistency. Certificates are used for other test cases as well.
            foreach (string connectionStr in DataTestUtility.AEConnStringsSetup)
            {
                SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(connectionStr);
                using (SqlConnection conn = CertificateUtility.GetOpenConnection(false, sb))
                {
                    using (SqlCommand cmd = new SqlCommand($"drop table {encryptedTableName}", conn))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = $"drop procedure {encryptedProcedureName}";
                        cmd.ExecuteNonQuery();
                    }
                }

                // Only use traceoff for non-sysadmin role accounts, Azure accounts does not have the permission.
                if (DataTestUtility.IsNotAzureServer())
                {
                    CertificateUtility.ChangeServerTceSetting(true, sb);
                }
            }
        }
    }
}


