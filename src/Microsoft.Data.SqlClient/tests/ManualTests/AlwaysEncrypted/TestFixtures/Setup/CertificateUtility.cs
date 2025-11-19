// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    class CertificateUtility
    {
        private CertificateUtility()
        {
        }

        /// <summary>
        /// System.Data assembly.
        /// </summary>
        public static Assembly systemData = Assembly.GetAssembly(typeof(SqlConnection));
        public static Type SqlSymmetricKeyCache = systemData.GetType("Microsoft.Data.SqlClient.SqlSymmetricKeyCache");
        public static MethodInfo SqlSymmetricKeyCacheGetInstance = SqlSymmetricKeyCache.GetMethod("GetInstance", BindingFlags.Static | BindingFlags.NonPublic);
        public static FieldInfo SqlSymmetricKeyCacheFieldCache = SqlSymmetricKeyCache.GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Through reflection, clear the SqlClient cache
        /// </summary>
        internal static void CleanSqlClientCache()
        {
            object sqlSymmetricKeyCache = SqlSymmetricKeyCacheGetInstance.Invoke(null, null);
            MemoryCache cache = SqlSymmetricKeyCacheFieldCache.GetValue(sqlSymmetricKeyCache) as MemoryCache;
            ClearCache(cache);
        }

        internal static SqlConnection GetOpenConnection(bool fTceEnabled, SqlConnectionStringBuilder sb, bool fSuppressAttestation = false)
        {
            SqlConnection conn = new SqlConnection(GetConnectionString(fTceEnabled, sb, fSuppressAttestation));
            try
            {
                conn.Open();
            }
            catch (Exception)
            {
                conn.Dispose();
                throw;
            }

            SqlConnection.ClearPool(conn);
            return conn;
        }

        /// <summary>
        /// Fetches a connection string that can be used to connect to SQL Server
        /// </summary>
        public static string GetConnectionString(bool fTceEnabled, SqlConnectionStringBuilder sb, bool fSuppressAttestation = false)
        {
            SqlConnectionStringBuilder builder = sb;
            if (fTceEnabled)
            {
                builder.ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled;
            }
            if (!fSuppressAttestation && DataTestUtility.EnclaveEnabled)
            {
                builder.EnclaveAttestationUrl = sb.EnclaveAttestationUrl;
                builder.AttestationProtocol = sb.AttestationProtocol;
            }
            builder.ConnectTimeout = 10000;
            return builder.ToString();
        }

        /// <summary>
        /// Turns on/off the TCE feature on server via traceflag
        /// </summary>
        public static void ChangeServerTceSetting(bool fEnable, SqlConnectionStringBuilder sb)
        {
            using (SqlConnection conn = GetOpenConnection(false, sb, fSuppressAttestation: true))
            {
                using (SqlCommand cmd = new SqlCommand("", conn))
                {
                    if (fEnable)
                    {
                        cmd.CommandText = "dbcc traceoff(4053, -1)";
                    }
                    else
                    {
                        cmd.CommandText = "dbcc traceon(4053, -1)"; // traceon disables feature
                    }
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void ClearCache(MemoryCache cache)
        {
#if NET
            cache.Clear();
#else
            // Compact with a target of 100% of objects is equivalent to clearing the cache
            cache.Compact(1);
#endif
        }
    }
}
