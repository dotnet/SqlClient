// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Data;
using System.Security.Authentication;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies managed SNI certificate-name selection against a configured trusted SQL Server.
    /// </summary>
    [Trait("Set", "1")]
    public class HostNameInCertificateTest
    {
        /// <summary>
        /// Requires managed SNI and a TCP configuration that validates its default server name.
        /// No certificate installation or server reconfiguration is performed by these tests.
        /// </summary>
        /// <returns>Whether the configured server can exercise hostname validation.</returns>
        public static bool IsCertificateValidationConfigured()
        {
            if (!DataTestUtility.IsTCPConnStringSetup() || !DataTestUtility.IsUsingManagedSNI())
            {
                return false;
            }

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            return builder.Encrypt != SqlConnectionEncryptOption.Optional
                && !builder.TrustServerCertificate
                && string.IsNullOrEmpty(builder.HostNameInCertificate)
                && string.IsNullOrEmpty(builder.ServerCertificate);
        }

        /// <summary>
        /// Both connection entry points enforce an explicitly configured certificate name.
        /// </summary>
        [ConditionalTheory(nameof(IsCertificateValidationConfigured))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_WithHostNameInCertificate_UsesConfiguredName(bool async)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = false,
                ConnectRetryCount = 0
            };
            using (SqlConnection connection = new(builder.ConnectionString))
            {
                if (async)
                {
                    await connection.OpenAsync();
                }
                else
                {
                    connection.Open();
                }
                Assert.Equal(ConnectionState.Open, connection.State);
            }

            builder.HostNameInCertificate = $"sqlclient-{Guid.NewGuid():N}.invalid";
            using SqlConnection mismatched = new(builder.ConnectionString);
            SqlException exception = async
                ? await Assert.ThrowsAsync<SqlException>(() => mismatched.OpenAsync())
                : Assert.Throws<SqlException>(() => mismatched.Open());

            AuthenticationException inner =
                Assert.IsType<AuthenticationException>(exception.InnerException);
            Assert.Contains("Certificate name mismatch.", inner.Message);
            Assert.Equal(ConnectionState.Closed, mismatched.State);
        }
    }
}

#endif
