// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    /// <summary>
    /// Tests immutable connection option parsing and validation.
    /// </summary>
    [Collection(AppContextSwitchTestCollection.Name)]
    public class SqlConnectionOptionsTest : IDisposable
    {
        // Ensure we restore the original app context switch values after each
        // test.
        private readonly LocalAppContextSwitchesHelper _appContextSwitchHelper = new();

        public void Dispose()
        {
            _appContextSwitchHelper.Dispose();
        }

#if NETFRAMEWORK
        [Theory]
        [InlineData("test.database.windows.net", true, true, true)]
        [InlineData("test.database.windows.net", false, true, false)]
        [InlineData("test.database.windows.net", null, true, false)]
        [InlineData("test.database.windows.net", true, false, true)]
        [InlineData("test.database.windows.net", false, false, false)]
        [InlineData("test.database.windows.net", null, false, true)]
        [InlineData("test.database.windows.net", true, null, true)]
        [InlineData("test.database.windows.net", false, null, false)]
        [InlineData("test.database.windows.net", null, null, true)]
        [InlineData("my.test.server", true, true, true)]
        [InlineData("my.test.server", false, true, false)]
        [InlineData("my.test.server", null, true, false)]
        [InlineData("my.test.server", true, false, true)]
        [InlineData("my.test.server", false, false, false)]
        [InlineData("my.test.server", null, false, true)]
        [InlineData("my.test.server", true, null, true)]
        [InlineData("my.test.server", false, null, false)]
        [InlineData("my.test.server", null, null, true)]
        public void TestDefaultTnir(string dataSource, bool? tnirEnabledInConnString, bool? tnirDisabledAppContext, bool expectedValue)
        {
            // Note: TNIR is only supported on .NET Framework.
            // Note: TNIR is disabled by default for Azure SQL Database servers (i.e. *.database.windows.net)
            // and when using federated auth unless explicitly set in the connection string.
            // However, this evaluation only happens at login time so TNIR behavior may not match
            // the value of TransparentNetworkIPResolution property in SqlConnectionOptions.

            // Arrange
            _appContextSwitchHelper.DisableTnirByDefault = tnirDisabledAppContext;

            // Act
            SqlConnectionStringBuilder builder = new();
            builder.DataSource = dataSource;
            if (tnirEnabledInConnString.HasValue)
            {
                builder.TransparentNetworkIPResolution = tnirEnabledInConnString.Value;
            }
            SqlConnectionOptions connectionString = new(builder.ConnectionString);

            // Assert
            Assert.Equal(expectedValue, connectionString.TransparentNetworkIPResolution);
        }
#endif
        /// <summary>
        /// Test MSF values when set through connection string and through app context switch.
        /// </summary>
        [Theory]
        [InlineData(true, true, true)]
        [InlineData(false, true, false)]
        [InlineData(null, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, false, false)]
        [InlineData(null, false, false)]
        [InlineData(null, null, false)]
        public void TestDefaultMultiSubnetFailover(bool? msfInConnString, bool? msfEnabledAppContext, bool expectedValue)
        {
            _appContextSwitchHelper.EnableMultiSubnetFailoverByDefault = msfEnabledAppContext;

            SqlConnectionStringBuilder builder = new();
            if (msfInConnString.HasValue)
            {
                builder.MultiSubnetFailover = msfInConnString.Value;
            }
            SqlConnectionOptions connectionString = new(builder.ConnectionString);

            Assert.Equal(expectedValue, connectionString.MultiSubnetFailover);
        }

        /// <summary>
        /// Tests that MultiSubnetFailover=true cannot be used with FailoverPartner.
        /// </summary>
        [Fact]
        public void TestMultiSubnetFailoverWithFailoverPartnerThrows()
        {
            _appContextSwitchHelper.EnableMultiSubnetFailoverByDefault = true;

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "server",
                FailoverPartner = "partner",
                InitialCatalog = "database"
            };

            Assert.Throws<ArgumentException>(() => new SqlConnectionOptions(builder.ConnectionString));
        }

#if NET
        /// <summary>
        /// Verifies that the client certificate options and no-space aliases map to their canonical values.
        /// </summary>
        [Fact]
        public void ClientCertificateOptions_ParseAliases()
        {
            SqlConnectionOptions options = new(
                "ClientCertificate=client.pem;ClientKey=client.key;ClientKeyPassword=<pwd>");

            Assert.Equal("client.pem", options.ClientCertificate);
            Assert.Equal("client.key", options.ClientKey);
            Assert.Equal("<pwd>", options.ClientKeyPassword);
            Assert.True(options.UsesClientCertificate);
        }

        /// <summary>
        /// Verifies that certificate authentication rejects connection-string credential mechanisms.
        /// </summary>
        [Theory]
        [InlineData("ClientCertificate=client.pfx;User ID=user")]
        [InlineData("ClientCertificate=client.pfx;Password=<pwd>")]
        [InlineData("ClientCertificate=client.pfx;Integrated Security=true")]
        [InlineData("ClientCertificate=client.pfx;Authentication=SqlPassword")]
        public void ClientCertificateOptions_WithOtherAuthentication_Throws(string connectionString)
        {
            Assert.Throws<ArgumentException>(() => new SqlConnectionOptions(connectionString));
        }

        /// <summary>
        /// Verifies that credential keywords present but empty (or explicitly disabled) do not
        /// conflict with certificate authentication.
        /// </summary>
        [Theory]
        [InlineData("ClientCertificate=client.pfx;User ID=")]
        [InlineData("ClientCertificate=client.pfx;Password=")]
        [InlineData("ClientCertificate=client.pfx;Integrated Security=false")]
        public void ClientCertificateOptions_EmptyCredentialKeywords_DoNotConflict(string connectionString)
        {
            SqlConnectionOptions options = new(connectionString);

            Assert.True(options.UsesClientCertificate);
        }

        /// <summary>
        /// Verifies that key material cannot be configured without a client certificate.
        /// </summary>
        [Theory]
        [InlineData("ClientKey=client.key")]
        [InlineData("ClientKey=")]
        [InlineData("ClientKeyPassword=<pwd>")]
        public void ClientCertificateOptions_KeyWithoutCertificate_Throws(string connectionString)
        {
            Assert.Throws<ArgumentException>(() => new SqlConnectionOptions(connectionString));
        }

        /// <summary>
        /// Verifies that the certificate container format is never inferred from the file extension,
        /// so a certificate may be supplied without a separate key file regardless of its name.
        /// </summary>
        [Theory]
        [InlineData("client.pem")]
        [InlineData("client.der")]
        [InlineData("client.cer")]
        [InlineData("client.pfx")]
        [InlineData("client")]
        public void ClientCertificateOptions_FormatNotInferredFromExtension(string certificatePath)
        {
            SqlConnectionOptions options = new($"ClientCertificate={certificatePath}");

            Assert.Equal(certificatePath, options.ClientCertificate);
            Assert.True(options.UsesClientCertificate);
        }

        /// <summary>
        /// Verifies that client key passwords are removed from returned and trace connection strings by default.
        /// </summary>
        [Fact]
        public void ClientKeyPassword_IsRedacted()
        {
            SqlConnectionOptions options = new(
                "Data Source=server;ClientCertificate=client.pfx;ClientKeyPassword=<pwd>");

            Assert.DoesNotContain("ClientKeyPassword", options.UsersConnectionString(hidePassword: true), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<pwd>", options.UsersConnectionStringForTrace(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that Persist Security Info affects the public string but never the trace string.
        /// </summary>
        [Fact]
        public void ClientKeyPassword_PersistSecurityInfo_DoesNotExposeTrace()
        {
            SqlConnectionOptions options = new(
                "ClientCertificate=client.pfx;ClientKeyPassword=<pwd>;Persist Security Info=true");

            Assert.Contains("<pwd>", options.UsersConnectionString(hidePassword: true), StringComparison.Ordinal);
            Assert.DoesNotContain("<pwd>", options.UsersConnectionStringForTrace(), StringComparison.Ordinal);
        }
#else
        /// <summary>
        /// Verifies that the client certificate keywords are not recognized on .NET Framework,
        /// where managed networking (and therefore certificate authentication) is unavailable.
        /// </summary>
        [Theory]
        [InlineData("ClientCertificate=client.pfx")]
        [InlineData("Client Certificate=client.pfx")]
        [InlineData("ClientKey=client.key")]
        [InlineData("Client Key=client.key")]
        [InlineData("ClientKeyPassword=<pwd>")]
        [InlineData("Client Key Password=<pwd>")]
        public void ClientCertificateOptions_NotSupportedOnNetFx(string connectionString)
        {
            Assert.Throws<ArgumentException>(() => new SqlConnectionOptions(connectionString));
        }
#endif
    }
}
