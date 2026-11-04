// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    [Trait("Set", "AE")]
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

    public sealed class ExceptionGenericErrorFixture : IDisposable
    {
        static public string encryptedTableName;
        static public string encryptedProcedureName;

        private readonly List<IDisposable> _databaseObjects = new();
        private readonly List<SqlConnection> _connections = new();

        public ExceptionGenericErrorFixture()
        {
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;

            // NOTE: If setup fails part way through, this constructor never returns, so xUnit never
            //   calls Dispose and the objects created so far would be leaked. Their names embed a
            //   GUID, so anything left behind stays in the shared test database forever.
            try
            {
                CreateAndPopulateSimpleTable();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void CreateAndPopulateSimpleTable()
        {
            encryptedTableName = DatabaseHelper.GenerateUniqueName("encrypted");
            encryptedProcedureName = DatabaseHelper.GenerateUniqueName("encrypted");

            // The same table and procedure name has to exist behind every AE connection string, so
            //   the objects are created with an explicit name rather than a generated one, and the
            //   connection each was created on is held open for the lifetime of the fixture.
            foreach (string connectionStr in DataTestUtility.AEConnStringsSetup)
            {
                SqlConnection conn = CertificateUtility.GetOpenConnection(false, new SqlConnectionStringBuilder(connectionStr));
                _connections.Add(conn);

                _databaseObjects.Add(Table.WithName(conn, encryptedTableName, "(c1 int)"));

                using (SqlCommand cmdInsert = new SqlCommand($"insert into {encryptedTableName} values(1)", conn))
                {
                    cmdInsert.CommandType = CommandType.Text;
                    cmdInsert.ExecuteNonQuery();
                }

                _databaseObjects.Add(StoredProcedure.WithName(
                    conn, encryptedProcedureName, $"(@c1 int) as insert into {encryptedTableName} values (@c1)"));
            }
        }

        public void Dispose()
        {
            // Do NOT remove certificate for concurrent consistency. Certificates are used for other test cases as well.

            // Disposed in reverse creation order so that each procedure is dropped before the table
            //   it writes into.
            for (int i = _databaseObjects.Count - 1; i >= 0; i--)
            {
                DisposeSafely(_databaseObjects[i]);
            }
            _databaseObjects.Clear();

            foreach (SqlConnection conn in _connections)
            {
                DisposeSafely(conn);
            }
            _connections.Clear();

            // Only use traceoff for non-sysadmin role accounts, Azure accounts does not have the permission.
            if (DataTestUtility.IsNotAzureServer())
            {
                foreach (string connectionStr in DataTestUtility.AEConnStringsSetup)
                {
                    try
                    {
                        CertificateUtility.ChangeServerTceSetting(true, new SqlConnectionStringBuilder(connectionStr));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{nameof(ExceptionGenericErrorFixture)}: failed to reset TCE setting: {ex.Message}");
                    }
                }
            }
        }

        private static void DisposeSafely(IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(ExceptionGenericErrorFixture)}: cleanup failed: {ex.Message}");
            }
        }

    }
}


