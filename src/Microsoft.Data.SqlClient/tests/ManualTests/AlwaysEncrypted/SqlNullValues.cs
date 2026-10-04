// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
// NOTE: Aliased because the AE test fixtures declare their own Table type alongside the shared one.
using SetupTable = Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup.Table;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    [Trait("Set", "AE")]
    public sealed class SqlNullValuesTests : IClassFixture<SQLSetupStrategyCertStoreProvider>, IDisposable
    {
        private SQLSetupStrategy fixture;
        private readonly string tableName;
        private readonly List<IDisposable> _databaseObjects = new();
        private readonly List<SqlConnection> _connections = new();
        private string UdfName = DatabaseHelper.GenerateUniqueName("SqlNullValuesRetVal");
        private string UdfNameNotNull = DatabaseHelper.GenerateUniqueName("SqlNullValuesRetValNotNull");

        public SqlNullValuesTests(SQLSetupStrategyCertStoreProvider context)
        {
            fixture = context;
            tableName = fixture.SqlNullValuesTable.Name;
            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;

            // NOTE: If setup fails part way through, this constructor never returns, so xUnit never calls
            //   Dispose and the functions created so far (whose names embed a GUID) would be leaked.
            try
            {
                foreach (string connStr in DataTestUtility.AEConnStringsSetup)
                {
                    // Insert data and create functions for SqlNullValues test.
                    using (SqlConnection sqlConnection = new SqlConnection(connStr))
                    {
                        sqlConnection.Open();

                        using (SqlCommand cmd = new SqlCommand(string.Format("INSERT INTO [{0}] (c1) VALUES (@c1)", tableName), sqlConnection, null, SqlCommandColumnEncryptionSetting.Enabled))
                        {
                            SqlParameter param = cmd.Parameters.Add("@c1", SqlDbType.Int);
                            param.Value = DBNull.Value;
                            cmd.ExecuteNonQuery();

                            param.Value = 10;
                            cmd.ExecuteNonQuery();
                        }

                    }

                    // The same function names have to exist behind every AE connection string, so
                    //   they are created with an explicit name rather than a generated one, and the
                    //   connection each was created on is held open until cleanup.
                    SqlConnection functionConnection = new SqlConnection(connStr);
                    functionConnection.Open();
                    _connections.Add(functionConnection);

                    _databaseObjects.Add(ScalarFunction.WithName(functionConnection, UdfName,
                        $"() RETURNS INT AS \n BEGIN \n RETURN (SELECT c1 FROM [{tableName}] WHERE c1 IS NULL)\n END"));
                    _databaseObjects.Add(ScalarFunction.WithName(functionConnection, UdfNameNotNull,
                        $"() RETURNS INT AS \n BEGIN \n RETURN (SELECT c1 FROM [{tableName}] WHERE c1 IS NOT NULL)\n END"));
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(NullValueTestsData))]
        public void NullValueTests(string connString, ConnStringColumnEncryptionSetting connStringSetting, SqlCommandColumnEncryptionSetting commandSetting, ReturnValueSetting nullReturnValue)
        {
            switch (connStringSetting)
            {
                case ConnStringColumnEncryptionSetting.Enabled:
                    connString += "; Column Encryption Setting=Enabled";
                    break;
                case ConnStringColumnEncryptionSetting.Disabled:
                    connString += "; Column Encryption Setting=Disabled";
                    break;
            }

            using (SqlConnection sqlConn = new SqlConnection(connString))
            {
                sqlConn.Open();
                object value1;
                object value2;
                SqlParameter param;

                // Create a command similarly
                using (SqlCommand cmd = new SqlCommand(string.Format("SELECT c1 FROM [{0}] ORDER BY c2 ASC", tableName),
                    sqlConn, null, commandSetting))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        Assert.True(reader.Read(), "No rows fetched first time");

                        // Depending on the various flags verify the results
                        value1 = reader.GetProviderSpecificValue(0);

                        // Read next row
                        Assert.True(reader.Read(), "No rows fetched second time");
                        value2 = reader.GetProviderSpecificValue(0);

                    }
                }

                using (SqlCommand cmd2 = new SqlCommand((ReturnValueSetting.Null == nullReturnValue) ? UdfName : UdfNameNotNull,
                    sqlConn, null, commandSetting))
                {
                    cmd2.CommandType = CommandType.StoredProcedure;
                    param = cmd2.Parameters.Add("@foo", SqlDbType.Int);
                    param.Direction = ParameterDirection.ReturnValue;
                    param.Value = new System.Data.SqlTypes.SqlInt32(1);
                    cmd2.ExecuteNonQuery();
                }

                switch (commandSetting)
                {
                    case SqlCommandColumnEncryptionSetting.Disabled:
                        // everything should be varbinary
                        Assert.True(value1 is System.Data.SqlTypes.SqlBinary, "Unexpected type");
                        Assert.True(value2 is System.Data.SqlTypes.SqlBinary, "Unexpected type");
                        Assert.True(param.Value is System.Data.SqlTypes.SqlBinary, "Unexpected Return value");
                        break;
                    case SqlCommandColumnEncryptionSetting.Enabled:
                    // Everything should be int
                    // intentional fall through
                    case SqlCommandColumnEncryptionSetting.ResultSetOnly:
                        // Again expect int
                        Assert.True(value1 is System.Data.SqlTypes.SqlInt32, "Unexpected type");
                        Assert.True(value2 is System.Data.SqlTypes.SqlInt32, "Unexpected type");
                        Assert.True(10 == ((System.Data.SqlTypes.SqlInt32)value2).Value, "Unexpected Value");
                        if (SqlCommandColumnEncryptionSetting.ResultSetOnly == commandSetting)
                        {
                            // For ResultSetOnly we don't expect to see plaintext for return values
                            Assert.True(param.Value is System.Data.SqlTypes.SqlBinary, "Unexpected Return value");
                        }
                        else
                        {
                            Assert.True(param.Value is System.Data.SqlTypes.SqlInt32, "Unexpected Return value");
                        }
                        break;
                    case SqlCommandColumnEncryptionSetting.UseConnectionSetting:
                        // Examine the connection string setting to figure out what to expect
                        if (ConnStringColumnEncryptionSetting.Enabled == connStringSetting)
                        {
                            // Expect int
                            Assert.True(value1 is System.Data.SqlTypes.SqlInt32, "Unexpected type");
                            Assert.True(value2 is System.Data.SqlTypes.SqlInt32, "Unexpected type");
                            Assert.True(10 == ((System.Data.SqlTypes.SqlInt32)value2).Value, "Unexpected Value");
                            Assert.True(param.Value is System.Data.SqlTypes.SqlInt32, "Unexpected Return value");
                        }
                        else
                        {
                            // Expect varbinary
                            Assert.True(value1 is System.Data.SqlTypes.SqlBinary, "Unexpected type");
                            Assert.True(value2 is System.Data.SqlTypes.SqlBinary, "Unexpected type");
                            Assert.True(param.Value is System.Data.SqlTypes.SqlBinary, "Unexpected Return value");
                        }
                        break;
                }
            }
        }

        public void Dispose()
        {
            for (int i = _databaseObjects.Count - 1; i >= 0; i--)
            {
                TryCleanup(_databaseObjects[i].Dispose);
            }
            _databaseObjects.Clear();

            foreach (SqlConnection connection in _connections)
            {
                TryCleanup(connection.Dispose);
            }
            _connections.Clear();

            foreach (string connStrAE in DataTestUtility.AEConnStringsSetup)
            {
                try
                {
                    using (SqlConnection sqlConnection = new SqlConnection(connStrAE))
                    {
                        sqlConnection.Open();

                        TryCleanup(() => SetupTable.DeleteData(fixture.SqlNullValuesTable.Name, sqlConnection));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{nameof(SqlNullValuesTests)}: cleanup connection failed: {ex.Message}");
                }
            }
        }

        private static void TryCleanup(Action cleanupAction)
        {
            try
            {
                cleanupAction();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(SqlNullValuesTests)}: cleanup step failed: {ex.Message}");
            }
        }
    }

    public class NullValueTestsData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                foreach (int connStrSetting in Enum.GetValues(typeof(ConnStringColumnEncryptionSetting)))
                {
                    foreach (int commandColumnEncryption in Enum.GetValues(typeof(SqlCommandColumnEncryptionSetting)))
                    {
                        yield return new object[] { connStrAE, connStrSetting, commandColumnEncryption, ReturnValueSetting.NotNull };
                        yield return new object[] { connStrAE, connStrSetting, commandColumnEncryption, ReturnValueSetting.Null };
                    }

                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public enum ConnStringColumnEncryptionSetting
    {
        None,
        Enabled,
        Disabled
    };

    public enum ReturnValueSetting
    {
        NotNull,
        Null
    };
}
