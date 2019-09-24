using System;
using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class ExceptionsGenericErrors : IClassFixture<ExceptionGenericErrorFixture> {
        
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TestCommandOptionWithNoTceFeature () {
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(DataTestUtility.TcpConnStr);
            CertificateUtility.ChangeServerTceSetting (false, sb); // disable TCE on engine.
            SqlConnection conn = CertificateUtility.GetOpenConnection (false, sb, fSuppressAttestation: true);
            SqlCommand cmd = new SqlCommand ("ins_tab1", conn, null, SqlCommandColumnEncryptionSetting.Enabled);
            SqlParameter param = cmd.Parameters.AddWithValue("@c1", 2);
            cmd.CommandType = CommandType.StoredProcedure;
            string expectedErrorMessage = "SQL Server instance in use does not support column encryption.";
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
            Assert.Contains(expectedErrorMessage, e.Message);
            
            cmd.Dispose();
            conn.Close();
            // Turn on TCE now
            CertificateUtility.ChangeServerTceSetting (true, sb); // enable tce
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TestDataAdapterAndEncrytionSetting () {
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(DataTestUtility.TcpConnStr);
            // Create a new SqlCommand for select and delete
            SqlConnection conn = CertificateUtility.GetOpenConnection(false, sb);
            SqlCommand cmdInsert = new SqlCommand("ins_tab1", conn, null, SqlCommandColumnEncryptionSetting.Enabled);
            cmdInsert.CommandType = CommandType.StoredProcedure;
            cmdInsert.Parameters.Add("@c1", SqlDbType.Int, 4, "c1");
            cmdInsert.UpdatedRowSource = UpdateRowSource.None;

            SqlCommand cmdDelete = new SqlCommand("delete tab1 where c1 = @c1", conn, null, SqlCommandColumnEncryptionSetting.Disabled);
            cmdDelete.Parameters.Add("@c1", SqlDbType.Int, 4, "c1");
            cmdDelete.UpdatedRowSource = UpdateRowSource.None;

            SqlDataAdapter adapter = new SqlDataAdapter("select c1 from tab1", conn);
            adapter.InsertCommand = cmdInsert;
            adapter.DeleteCommand = cmdDelete;

            DataSet dataset = new DataSet();
            adapter.Fill(dataset);
            DataTable table = dataset.Tables[0];
            foreach (DataRow row in table.Rows) {
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
            adapter.Dispose();
            conn.Close();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TestInvalidForceColumnEncryptionSetting() {
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(DataTestUtility.TcpConnStr);
            SqlConnection conn = CertificateUtility.GetOpenConnection(false, sb);
            SqlCommand cmd = new SqlCommand ("ins_tab1", conn);
            SqlParameter param = cmd.Parameters.AddWithValue("@c1", 2);
            param.ForceColumnEncryption = true;
            cmd.CommandType = CommandType.StoredProcedure;
            string expectedErrorMessage = "Cannot set ForceColumnEncryption(true) for SqlParameter '@c1' because encryption is not enabled for the statement or procedure 'ins_tab1'.";
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
            Assert.Contains(expectedErrorMessage, e.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TestParamUnexpectedEncryptionMD() {
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(DataTestUtility.TcpConnStr);
            SqlConnection conn = CertificateUtility.GetOpenConnection(true, sb);
            SqlCommand cmd = new SqlCommand ("ins_tab1", conn);
            SqlParameter param = cmd.Parameters.AddWithValue("@c1", 2);
            param.ForceColumnEncryption = true;
            cmd.CommandType = CommandType.StoredProcedure;
            string expectedErrorMessage = "Cannot execute statement or procedure 'ins_tab1' because ForceColumnEncryption(true) was set for SqlParameter '@c1' and the database expects this parameter to be sent as plaintext. This may be due to a configuration error.";
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
            Assert.Contains(expectedErrorMessage, e.Message);
        }
    }

    public class ExceptionGenericErrorFixture : IDisposable
    {
        public ExceptionGenericErrorFixture()
        {
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;

            CreateAndPopulateSimpleTable();
        }

        private void CreateAndPopulateSimpleTable()
        {
            SqlConnection conn = CertificateUtility.GetOpenConnection(false, new SqlConnectionStringBuilder(DataTestUtility.TcpConnStr));
            SqlCommand cmdCreate = null;
            SqlCommand cmdInsert = null;
            SqlCommand cmdCreateProc = null;

            try
            {
                cmdCreate = new SqlCommand("create table tab1(c1 int)", conn);
                cmdCreate.CommandType = CommandType.Text;
                cmdCreate.ExecuteNonQuery();

                cmdInsert = new SqlCommand("insert into tab1 values(1)", conn);
                cmdInsert.CommandType = CommandType.Text;
                cmdInsert.ExecuteNonQuery();

                cmdCreateProc = new SqlCommand("create procedure ins_tab1(@c1 int) as insert into tab1 values (@c1)", conn);
                cmdCreateProc.CommandType = CommandType.Text;
                cmdCreateProc.ExecuteNonQuery();
                conn.Close();
            }
            finally
            {
                DisposeCommand(cmdCreate);
                DisposeCommand(cmdInsert);
                DisposeCommand(cmdCreateProc);
                if (null != conn)
                    conn.Dispose();
            }
        }
        public void DisposeCommand(SqlCommand cmd)
        {
            if (null != cmd)
            {
                cmd.Dispose();
            }
        }

        public void Dispose()
        {
            // Do NOT remove certificate for concurrent consistency. Certificates are used for other test cases as well.
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(DataTestUtility.TcpConnStr);
            SqlConnection conn = CertificateUtility.GetOpenConnection(false, sb);
            SqlCommand cmd = new SqlCommand("drop table tab1", conn);
            cmd.CommandType = CommandType.Text;
            cmd.ExecuteNonQuery();

            cmd.CommandText = "drop procedure ins_tab1";
            cmd.ExecuteNonQuery();
            cmd.Dispose();
            conn.Close();
            CertificateUtility.ChangeServerTceSetting(true, sb);
        }
    }
}


