// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SQLSetupStrategyCspProvider : SQLSetupStrategy
    {
        private const int KeySize = 2048;

        private readonly List<CspParameters> _cspKeyParameters = new List<CspParameters>();

        public SQLSetupStrategyCspProvider(CspParameters cspParameters)
            : base(cspParameters.ProviderName + "/" + cspParameters.KeyContainerName)
        {
            // Create a new instance of RSACryptoServiceProvider to generate 
            // a new key pair.  Pass the CspParameters class to persist the  
            // key in the container.
            using RSACryptoServiceProvider rsaAlg = new RSACryptoServiceProvider(KeySize, cspParameters);
            rsaAlg.PersistKeyInCsp = true;

            _cspKeyParameters.Add(cspParameters);

            CspProvider = new SqlColumnEncryptionCspProvider();
            SetupDatabase();
        }

        public SqlColumnEncryptionCspProvider CspProvider { get; }

        internal override void SetupDatabase()
        {
            ColumnMasterKey columnMasterKey = new CspProviderColumnMasterKey(GenerateUniqueName("CspExt"), SqlColumnEncryptionCspProvider.ProviderName, ColumnMasterKeyPath);
            databaseObjects.Add(columnMasterKey);

            List<ColumnEncryptionKey> columnEncryptionKeys = CreateColumnEncryptionKeys(columnMasterKey, 2, CspProvider);
            databaseObjects.AddRange(columnEncryptionKeys);

            List<Table> tables = CreateTables(columnEncryptionKeys);
            databaseObjects.AddRange(tables);

            base.SetupDatabase();

            InsertSampleData(ApiTestTable.Name);
        }

        protected override void Dispose(bool disposing)
        {
            foreach (CspParameters cspParameters in _cspKeyParameters)
            {
                try
                {
                    // Create a new instance of RSACryptoServiceProvider.  
                    // Pass the CspParameters class to use the  
                    // key in the container.
                    using RSACryptoServiceProvider rsaAlg = new RSACryptoServiceProvider(cspParameters);

                    // Delete the key entry in the container.
                    rsaAlg.PersistKeyInCsp = false;

                    // Call Clear to release resources and delete the key from the container.
                    rsaAlg.Clear();
                }
                catch (Exception)
                {
                    continue;
                }
            }

            base.Dispose(disposing);
        }
    }
}
