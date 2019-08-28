// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    class TestFixtures
    {
        public static byte[] GenerateTestEncryptedBytes(byte version, short keyPathLength, short ciphertextLength, short signature)
        {
            byte[] v = new byte[] { version };
            byte[] kpl = BitConverter.GetBytes(keyPathLength);
            byte[] ctl = BitConverter.GetBytes(ciphertextLength);

            int index = 0;
            byte[] data = new byte[v.Length + kpl.Length + ctl.Length + keyPathLength + ciphertextLength + signature];
            Buffer.BlockCopy(v, 0, data, index, v.Length);
            index += v.Length;
            Buffer.BlockCopy(kpl, 0, data, index, kpl.Length);
            index += kpl.Length;
            Buffer.BlockCopy(ctl, 0, data, index, ctl.Length);

            return data;
        }

        /// <summary>
        /// Function that would construct a connection string with default parameters.
        /// </summary>
        /// <returns></returns>
        public static string DefaultConnectionString(SqlConnectionColumnEncryptionSetting columnEncryptionSetting, bool fEnclaveEnabled, string enclaveAttestationUrl)
        {
            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            csb.DataSource = "localhost,12345";
            csb.Pooling = false;
            csb.Encrypt = false;
            csb.ConnectTimeout = 65534;
            csb.UserID = "prodUser1@FedAuthAzureSqlDb.onmicrosoft.com";
            csb.ColumnEncryptionSetting = columnEncryptionSetting;
            if (fEnclaveEnabled)
            {
                csb.EnclaveAttestationUrl = enclaveAttestationUrl;
            }
            return csb.ConnectionString;
        }
    }
}
