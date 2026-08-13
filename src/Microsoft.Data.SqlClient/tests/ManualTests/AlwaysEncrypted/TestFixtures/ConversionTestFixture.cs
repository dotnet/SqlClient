// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    /// <summary>
    /// Class fixture for <see cref="ConversionTests"/>. Creates a single Column Master Key (CMK) and
    /// Column Encryption Key (CEK) once for the whole test class instead of once per test case.
    /// </summary>
    /// <remarks>
    /// xUnit constructs a test class instance for every test case, so creating the keys in the
    /// <see cref="ConversionTests"/> constructor issued a <c>CREATE COLUMN MASTER KEY</c> and
    /// <c>CREATE COLUMN ENCRYPTION KEY</c> for each of the dozens of cases in the class. On a
    /// long-lived shared database that rapidly advances SQL Server's internal per-database
    /// identifier allocator, which is never reclaimed on <c>DROP</c>, eventually causing error 3807
    /// ("Create failed because all available identifiers have been exhausted"). Creating the keys
    /// once per class via <see cref="Xunit.IClassFixture{TFixture}"/> reduces that churn by roughly
    /// two orders of magnitude.
    /// </remarks>
    public sealed class ConversionTestFixture : ColumnMasterKeyCertificateFixture
    {
        private readonly ColumnMasterKey _columnMasterKey;
        private readonly SqlColumnEncryptionCertificateStoreProvider _certStoreProvider =
            new SqlColumnEncryptionCertificateStoreProvider();

        /// <summary>
        /// The single Column Encryption Key shared by all cases in <see cref="ConversionTests"/>.
        /// </summary>
        public ColumnEncryptionKey ColumnEncryptionKey { get; }

        public ConversionTestFixture()
        {
            _columnMasterKey = new CspColumnMasterKey(
                DatabaseHelper.GenerateUniqueName("CMK"),
                ColumnMasterKeyCertificate.Thumbprint,
                _certStoreProvider,
                DataTestUtility.EnclaveEnabled);

            ColumnEncryptionKey = new ColumnEncryptionKey(
                DatabaseHelper.GenerateUniqueName("CEK"),
                _columnMasterKey,
                _certStoreProvider);

            foreach (string connectionStr in DataTestUtility.AEConnStringsSetup)
            {
                SqlConnectionStringBuilder connectionString = new SqlConnectionStringBuilder(connectionStr);
                // The AE setup often fails with a connect timeout here; ensure a reasonable minimum.
                connectionString.ConnectTimeout = Math.Max(connectionString.ConnectTimeout, 30);

                using SqlConnection sqlConnection = new SqlConnection(connectionString.ConnectionString);
                sqlConnection.Open();
                _columnMasterKey.Create(sqlConnection);
                ColumnEncryptionKey.Create(sqlConnection);
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                foreach (string connectionStr in DataTestUtility.AEConnStringsSetup)
                {
                    SqlConnectionStringBuilder connectionString = new SqlConnectionStringBuilder(connectionStr);
                    // Match the constructor's minimum timeout; AE teardown is prone to connect timeouts,
                    // and skipping cleanup would leave the shared CMK/CEK behind.
                    connectionString.ConnectTimeout = Math.Max(connectionString.ConnectTimeout, 30);

                    // Key drops are best-effort: a failure here must not prevent the base fixture
                    // from removing the test certificate from the certificate store (see finally).
                    try
                    {
                        using SqlConnection sqlConnection = new SqlConnection(connectionString.ConnectionString);
                        sqlConnection.Open();
                        ColumnEncryptionKey.Drop(sqlConnection);
                        _columnMasterKey.Drop(sqlConnection);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"ConversionTestFixture: failed to drop keys on '{connectionString.DataSource}': {ex.Message}");
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
