using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Xtrimmer.SqlDatabaseBuilder;
using Xunit;

using static Xtrimmer.SqlDatabaseBuilder.DataType;
using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests
{
    public sealed class SqlDatabaseFixture : IDisposable
    {
        public ColumnMasterKey ColumnMasterKey { get; private set; }
        public ColumnEncryptionKey ColumnEncryptionKey { get; private set; }
        public SqlConnection SqlConnection { get; private set; }
        public SqlConnection SqlConnectionAE { get; private set; }
        public Table Table { get; private set; }
        private List<DatabaseResource> DatabaseObjects { get; set; } = new List<DatabaseResource>();

        public SqlDatabaseFixture()
        {
            string testRunId = Guid.NewGuid().ToString("N");
            RegisterAzureKeyVaultProvider();
            SqlConnection = new SqlConnection(connectionString);
            SqlConnection.Open();
            SqlConnectionAE = new SqlConnection(connectionStringAE);
            SqlConnectionAE.Open();
            ColumnMasterKey = CreateColumnMasterKey(testRunId);
            ColumnEncryptionKey = CreateColumnEncryptionKey(testRunId, ColumnMasterKey);
            Table = CreateTable(testRunId, ColumnEncryptionKey);
        }

        public void Dispose()
        {
            DatabaseObjects.Reverse();

            foreach (DatabaseResource databaseObject in DatabaseObjects)
            {
                databaseObject.Drop(SqlConnectionAE);
            }
            SqlConnectionAE.Dispose();
        }

        public void Insert(SqlParameter parameter)
        {
            if (parameter.Value == null)
            {
                parameter.Value = DBNull.Value;
            }

            using SqlCommand sqlCommandAE = SqlConnectionAE.CreateCommand();
            string sql = $"INSERT INTO [{Table.Name}] ([{parameter.SqlDbType}]) VALUES ({parameter.ParameterName});";
            sqlCommandAE.CommandText = sql;
            sqlCommandAE.Parameters.Add(parameter);
            sqlCommandAE.ExecuteNonQuery();
        }

        public object SelectPlaintext(SqlDbType type)
        {
            var result = SelectData(SqlConnectionAE, type);
            return result is DBNull ? null : result;
        }

        public byte[] SelectCiphertext(SqlDbType type)
        {
            var result = SelectData(SqlConnection, type);
            return result is DBNull ? null : (byte[])result;
        }

        public void DeleteAllDataFromTable()
        {
            using SqlCommand sqlCommand = SqlConnection.CreateCommand();
            sqlCommand.CommandText = $"DELETE FROM [{Table.Name}]";
            sqlCommand.ExecuteNonQuery();
        }

        #region Private Methods

        private void RegisterAzureKeyVaultProvider()
        {
            SqlColumnEncryptionAzureKeyVaultProvider sqlColumnEncryptionAzureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider(AzureActiveDirectoryAuthenticationCallback);
            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders: new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>(capacity: 1, comparer: StringComparer.OrdinalIgnoreCase)
                {
                    { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, sqlColumnEncryptionAzureKeyVaultProvider}
                });
        }

        private static async Task<string> AzureActiveDirectoryAuthenticationCallback(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(clientId, clientSecret);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);
            if (result == null)
            {
                throw new InvalidOperationException($"Failed to retrieve an access token for {resource}");
            }

            return result.AccessToken;
        }

        private ColumnMasterKey CreateColumnMasterKey(string testRunId)
        {
            ColumnMasterKey columnMasterKey = new ColumnMasterKey
            (
                keyName: $"MicrosoftDataEncryptionTest_CMK_{testRunId}",
                keyStoreProviderName: KeyStoreProvider.AzureKeyVaultProvider,
                keyPath: keyEncryptionKeyPath
            )
            {
                IsEnclaveEnabled = true,
                Signature = "0xA33BAC501315F4E407A5A52ED36860F286622B42D7074AD63AD5D6CB65466290DCE28DAF0AC5AFB368C6F8D7940AAC69C3A011BA85C75A22175DE397E8058DA7320B0442F9B6B51CF649A0F5E1EF9A18EFAA0EF1CB5B15435265246B41FC54808FBFF665087F7048288BBEE4FCD3B4E55D6B50226B0A120B089A0A6D571A9CDFFA25D4623FA8076F1C4B250926CB9EDE187FEDD9404934EF0846C2C0206F165EFEC402386D85C2074E5EFD98A96BD32C6F97CB84FAB2BF3C263AC0762159D4504B4645D193CC761ECB107C6EC059FD449D8D89BA98D69BAB92534FAE20B8B577BE269FD2414E8273B42D67DC3ABB0E75902A3FED3F051F2CE23206F74545BEF6"
            };
            columnMasterKey.Create(SqlConnectionAE);
            DatabaseObjects.Add(columnMasterKey);
            return columnMasterKey;
        }

        private ColumnEncryptionKey CreateColumnEncryptionKey(string testRunId, ColumnMasterKey cmk)
        {
            ColumnEncryptionKey columnEncryptionKey = new ColumnEncryptionKey
            (
                keyName: $"MicrosoftDataEncryptionTest_CEK_{testRunId}",
                columnMasterKey: cmk,
                encryptedValue: "0x01CE000001680074007400700073003A002F002F006A0065007400720069006D006D0065002D006B00650079002D007600610075006C0074002E007600610075006C0074002E0061007A007500720065002E006E00650074002F006B006500790073002F0061006C0077006100790073002D0065006E0063007200790070007400650064002D006100750074006F0031002F0061003700640035006300390038003900640035006100300034003200330066003800640033003500360066003100390035003500330036006500350039003000A4E7182E3CEF4C4A29F287D672868659FF9367C579ADA8D3367BC2804738DA1A0CA8D836B4CEA5D940E0DD7DACFD453BD70793A23E725C50E1CB538ECDCE2B660A9482CBF561B2B7B5BBAC18074145EEAB8C8DE38A0B1297413C1C5411D88602A46BE58854DFD21915FC408BD7C36A3C6A883B20D50FDC18A5800059EBD4515BDDD4B9AC3D065183740651B2DCBA43C9293C64C0F67E6DA4F14A1BC1D5760CF32EA8B0B0AD474BE3E73864B8DA4E9A53070651A9106683B5D7DCC0D01D3F53CE1E4C558CD5E466F0E713201F6327CFA06EB968E2FC2186295A1E072371C8E461928877AF3360D4DEB27114CF37BE82573C4DFF0CBD96705478D09735DFA58305128A1AF38DA9D0A95A3B2A374FB48FE927D23436754D0931277DE513E8022E4EAA55A3991EA932958A8F34D6D02B6F7A4217835E2B10A9109F953C970C5FEB728B43AB03999CE5C6144D386E7BE6BE71287D660DCE0C81DA37D7FE3EE1AE2EE40D78B0B38EE9BA6B2E366979F88547B1036EBFCB8C07756F446CFF6C523F457AF081DE50215C9B68C548E11B864E690FB64927C10D8F23328DDB663399154E4A314CB453172F7F39D36ACF7B7F5B6B6BDBEEC52E8D79E7971FF3CA4D3461272B61249642861EA484819958423CED8705A69ED671BF05E071743B800D97C2395684DD53D09AA359887E39E48EE5AF25F69D79E85C35586D8730FEC03931D05F85"
            );
            columnEncryptionKey.Create(SqlConnectionAE);
            DatabaseObjects.Add(columnEncryptionKey);
            return columnEncryptionKey;
        }

        private Table CreateTable(string testRunId, ColumnEncryptionKey columnEncryptionKey)
        {
            ColumnEncryption columnEncryption = new ColumnEncryption(columnEncryptionKey, ColumnEncryptionType.Deterministic);
            Table table = new Table($"MicrosoftDataEncryptionTestTable_{testRunId}");
            table.Columns.AddAll
            (
                new Column("bigint", BigInt()) { ColumnEncryption = columnEncryption },
                new Column("binary", Binary(10)) { ColumnEncryption = columnEncryption },
                new Column("bit", Bit()) { ColumnEncryption = columnEncryption },
                new Column("char", Char(10)) { ColumnEncryption = columnEncryption, Collation = "Latin1_General_BIN2" },
                new Column("date", Date()) { ColumnEncryption = columnEncryption },
                new Column("datetime", DateTime()) { ColumnEncryption = columnEncryption },
                new Column("datetime2", DateTime2(7)) { ColumnEncryption = columnEncryption },
                new Column("datetimeoffset", DateTimeOffset(7)) { ColumnEncryption = columnEncryption },
                new Column("decimal", Decimal(18, 0)) { ColumnEncryption = columnEncryption },
                new Column("float", Float()) { ColumnEncryption = columnEncryption },
                new Column("int", Int()) { ColumnEncryption = columnEncryption },
                new Column("money", Money()) { ColumnEncryption = columnEncryption },
                new Column("nchar", NChar(10)) { ColumnEncryption = columnEncryption, Collation = "Latin1_General_BIN2" },
                new Column("numeric", Numeric(18, 0)) { ColumnEncryption = columnEncryption },
                new Column("nvarchar", NVarChar(50)) { ColumnEncryption = columnEncryption, Collation = "Latin1_General_BIN2" },
                new Column("nvarcharMAX", NVarChar(MAX)) { ColumnEncryption = columnEncryption, Collation = "Latin1_General_BIN2" },
                new Column("real", Real()) { ColumnEncryption = columnEncryption },
                new Column("smalldatetime", SmallDateTime()) { ColumnEncryption = columnEncryption },
                new Column("smallint", SmallInt()) { ColumnEncryption = columnEncryption },
                new Column("smallmoney", SmallMoney()) { ColumnEncryption = columnEncryption },
                new Column("time", Time(7)) { ColumnEncryption = columnEncryption },
                new Column("tinyint", TinyInt()) { ColumnEncryption = columnEncryption },
                new Column("uniqueidentifier", UniqueIdentifier()) { ColumnEncryption = columnEncryption },
                new Column("varbinary", VarBinary(10)) { ColumnEncryption = columnEncryption },
                new Column("varbinaryMAX", VarBinary(MAX)) { ColumnEncryption = columnEncryption },
                new Column("varchar", VarChar(50)) { ColumnEncryption = columnEncryption, Collation = "Latin1_General_BIN2" },
                new Column("varcharMAX", VarChar(MAX)) { ColumnEncryption = columnEncryption, Collation = "Latin1_General_BIN2" }
            );
            table.Create(SqlConnectionAE);
            DatabaseObjects.Add(table);
            return table;
        }

        private object SelectData(SqlConnection connection, SqlDbType type)
        {
            SqlCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT [{type}] FROM [{Table.Name}]";
            return command.ExecuteScalar();
        }

        #endregion
    }

    [CollectionDefinition("Database collection")]
    public class DatabaseCollection : ICollectionFixture<SqlDatabaseFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
