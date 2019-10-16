// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.KeyVault.WebKey;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest.Azure;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SQLSetupStrategy : IDisposable
    {
        internal const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA256";
        internal const string AKVKeyName = "TestSqlClientAzureKeyVaultProvider";

        private readonly X509Certificate2 certificate;
        private static readonly Uri AKVUri = new Uri(new Uri(DataTestUtility.AKVUrl), "/");
        public string keyPath { get; private set; }
        public readonly string AKVKeyURL = (new Uri(AKVUri, $"/keys/{AKVKeyName}")).AbsoluteUri;
        public Table ApiTestTable { get; private set; }
        public Table AKVTestTable { get; private set; }
        public Table BulkCopyAETestTable { get; private set; }
        public Table SqlParameterPropertiesTable { get; private set; }
        public Table End2EndSmokeTable { get; private set; }

        public SqlColumnEncryptionAzureKeyVaultProvider akvStoreProvider;
        public SqlColumnEncryptionCertificateStoreProvider certStoreProvider;
        public CspColumnMasterKey cspColumnMasterKey;

        protected List<DbObject> databaseObjects = new List<DbObject>();

        public SQLSetupStrategy()
        {
            certificate = CertificateUtility.CreateCertificate();

            KeyVaultClient keyVaultClient = keyVaultClient = new KeyVaultClient(authenticationCallback);
            IPage<KeyItem> keys = keyVaultClient.GetKeysAsync(AKVUri.AbsoluteUri).Result;
            bool testAKVKeyExists = false;
            while (true)
            {
                foreach (KeyItem ki in keys)
                {
                    if (ki.Identifier.Name.Equals(AKVKeyName))
                    {
                        testAKVKeyExists = true;
                    }
                }

                if (!string.IsNullOrEmpty(keys.NextPageLink))
                {
                    keys = keyVaultClient.GetKeysNextAsync(keys.NextPageLink).Result;
                }
                else
                {
                    break;
                }
            }

            if (!testAKVKeyExists)
            {
                RSAParameters p = certificate.GetRSAPrivateKey().ExportParameters(true);
                KeyBundle kb = new KeyBundle()
                {
                    Key = new Azure.KeyVault.WebKey.JsonWebKey
                    {
                        Kty = JsonWebKeyType.Rsa,
                        D = p.D,
                        DP = p.DP,
                        DQ = p.DQ,
                        P = p.P,
                        Q = p.Q,
                        QI = p.InverseQ,
                        N = p.Modulus,
                        E = p.Exponent,
                    },
                };
                keyVaultClient.ImportKeyAsync(AKVUri.AbsoluteUri, AKVKeyName, kb);
            }

            certStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();
            akvStoreProvider = new SqlColumnEncryptionAzureKeyVaultProvider(authenticationCallback: authenticationCallback);
            SetupDatabase();
        }

        protected SQLSetupStrategy(string customKeyPath) => keyPath = customKeyPath;

        internal virtual void SetupDatabase()
        {
            cspColumnMasterKey = new CspColumnMasterKey(GenerateUniqueName("CMK"), certificate.Thumbprint);
            databaseObjects.Add(cspColumnMasterKey);

            ColumnMasterKey akvColumnMasterKey = new AkvColumnMasterKey(GenerateUniqueName("AKVCMK"), akvUrl:AKVKeyURL);
            databaseObjects.Add(akvColumnMasterKey);

            List<ColumnEncryptionKey> columnEncryptionKeys = CreateColumnEncryptionKeys(cspColumnMasterKey, 2, certStoreProvider);
            databaseObjects.AddRange(columnEncryptionKeys);

            List<ColumnEncryptionKey> akcColumnEncryptionKeys = CreateColumnEncryptionKeys(akvColumnMasterKey, 2, akvStoreProvider);
            databaseObjects.AddRange(akcColumnEncryptionKeys);

            List<Table> tables = CreateTables(columnEncryptionKeys, akcColumnEncryptionKeys);
            databaseObjects.AddRange(tables);

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                databaseObjects.ForEach(o => o.Create(sqlConnection));
            }
        }

        public async Task<string> authenticationCallback(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(DataTestUtility.ClientId, DataTestUtility.ClientSecret);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to retrieve access token for Key Vault");
            }

            return result.AccessToken;
        }

        protected List<ColumnEncryptionKey> CreateColumnEncryptionKeys(ColumnMasterKey columnMasterKey, int count, SqlColumnEncryptionKeyStoreProvider columnEncryptionKeyStoreProvider)
        {
            List<ColumnEncryptionKey> columnEncryptionKeys = new List<ColumnEncryptionKey>();

            for (int i = 0; i < count; i++)
            {
                ColumnEncryptionKey columnEncryptionKey = new ColumnEncryptionKey(GenerateUniqueName("CEK"), columnMasterKey, columnEncryptionKeyStoreProvider);
                columnEncryptionKeys.Add(columnEncryptionKey);
            }

            return columnEncryptionKeys;
        }

        private List<Table> CreateTables(IList<ColumnEncryptionKey> columnEncryptionKeys, IList<ColumnEncryptionKey> akvColumnEncryptionKeys)
        {
            List<Table> tables = new List<Table>();

            ApiTestTable = new ApiTestTable(GenerateUniqueName("ApiTestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(ApiTestTable);

            AKVTestTable = new AKVTestTable(GenerateUniqueName("AKVTestTable"), akvColumnEncryptionKeys[0], akvColumnEncryptionKeys[1]);
            tables.Add(AKVTestTable);

            BulkCopyAETestTable = new BulkCopyAETestTable(GenerateUniqueName("BulkCopyAETestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(BulkCopyAETestTable);

            SqlParameterPropertiesTable = new SqlParameterPropertiesTable(GenerateUniqueName("SqlParameterPropertiesTable"));
            tables.Add(SqlParameterPropertiesTable);

            End2EndSmokeTable = new ApiTestTable(GenerateUniqueName("End2EndSmokeTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(End2EndSmokeTable);

            return tables;
        }

        protected string GenerateUniqueName(string baseName) => string.Concat("AE-", baseName, "-", Guid.NewGuid().ToString());

        public void Dispose()
        {
            databaseObjects.Reverse();
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                databaseObjects.ForEach(o => o.Drop(sqlConnection));
            }
        }
    }
}
