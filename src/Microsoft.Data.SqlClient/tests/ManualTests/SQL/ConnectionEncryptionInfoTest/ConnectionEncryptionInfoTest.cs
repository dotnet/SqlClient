// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies the negotiated encryption mode and TLS protocol that
    /// <see cref="SqlConnection.EncryptionInfo"/> and <see cref="SqlException.EncryptionInfo"/>
    /// report against a real SQL Server.
    /// </summary>
    [Trait("Set", "1")]
    public class ConnectionEncryptionInfoTest
    {
        /// <summary>
        /// The TLS 1.3 protocol value. <c>SslProtocols.Tls13</c> is not defined on .NET Framework.
        /// </summary>
        private const SslProtocols Tls13 = (SslProtocols)0x3000;

        /// <summary>
        /// The SQL Server error number for a failed login.
        /// </summary>
        private const int LoginFailedErrorNumber = 18456;

        #region Successful open

        /// <summary>
        /// A synchronous open with optional encryption reports full encryption when the server
        /// encrypts the session, and login-only encryption otherwise.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void Open_OptionalEncryption_ReportsServerEncryptionState()
        {
            // Arrange
            using SqlConnection connection = CreateConnection(SqlConnectionEncryptOption.Optional);

            // Act
            connection.Open();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, ExpectedOptionalMode(connection));
        }

        /// <summary>
        /// An asynchronous open with optional encryption reports full encryption when the server
        /// encrypts the session, and login-only encryption otherwise.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public async Task OpenAsync_OptionalEncryption_ReportsServerEncryptionState()
        {
            // Arrange
            using SqlConnection connection = CreateConnection(SqlConnectionEncryptOption.Optional);

            // Act
            await connection.OpenAsync();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, ExpectedOptionalMode(connection));
        }

        /// <summary>
        /// A synchronous open with mandatory encryption reports full encryption.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void Open_MandatoryEncryption_ReportsFull()
        {
            // Arrange
            using SqlConnection connection = CreateConnection(SqlConnectionEncryptOption.Mandatory);

            // Act
            connection.Open();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, SqlConnectionEncryptionMode.Full);
        }

        /// <summary>
        /// An asynchronous open with mandatory encryption reports full encryption.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public async Task OpenAsync_MandatoryEncryption_ReportsFull()
        {
            // Arrange
            using SqlConnection connection = CreateConnection(SqlConnectionEncryptOption.Mandatory);

            // Act
            await connection.OpenAsync();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, SqlConnectionEncryptionMode.Full);
        }

        /// <summary>
        /// A synchronous open with strict (TDS 8) encryption reports strict encryption.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTDS8Supported),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void Open_StrictEncryption_ReportsStrict()
        {
            // Arrange
            using SqlConnection connection = CreateConnection(SqlConnectionEncryptOption.Strict);

            // Act
            connection.Open();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, SqlConnectionEncryptionMode.Strict);
        }

        /// <summary>
        /// An asynchronous open with strict (TDS 8) encryption reports strict encryption.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTDS8Supported),
            nameof(DataTestUtility.IsNotAzureServer))]
        public async Task OpenAsync_StrictEncryption_ReportsStrict()
        {
            // Arrange
            using SqlConnection connection = CreateConnection(SqlConnectionEncryptOption.Strict);

            // Act
            await connection.OpenAsync();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, SqlConnectionEncryptionMode.Strict);
        }

        #endregion

        #region Failed open

        /// <summary>
        /// A synchronous open whose login is rejected after the TLS handshake attaches the
        /// negotiated mode and protocol to the exception, and leaves the connection empty.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void Open_LoginFailed_ExceptionReportsNegotiatedValues()
        {
            // Arrange
            using SqlConnection connection = CreateInvalidLoginConnection();

            // Act
            SqlException ex = Assert.Throws<SqlException>(connection.Open);

            // Assert
            Assert.Equal(LoginFailedErrorNumber, ex.Number);
            AssertNegotiated(ex.EncryptionInfo, SqlConnectionEncryptionMode.Full);
            AssertEmpty(connection.EncryptionInfo);
        }

        /// <summary>
        /// An asynchronous open whose login is rejected after the TLS handshake attaches the
        /// negotiated mode and protocol to the exception, and leaves the connection empty.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public async Task OpenAsync_LoginFailed_ExceptionReportsNegotiatedValues()
        {
            // Arrange
            using SqlConnection connection = CreateInvalidLoginConnection();

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(connection.OpenAsync);

            // Assert
            Assert.Equal(LoginFailedErrorNumber, ex.Number);
            AssertNegotiated(ex.EncryptionInfo, SqlConnectionEncryptionMode.Full);
            AssertEmpty(connection.EncryptionInfo);
        }

        #endregion

        #region Connection lifetime

        /// <summary>
        /// Closing a connection resets its encryption info to empty without changing a snapshot
        /// the caller already holds.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void Close_AfterOpen_ResetsToEmptyAndKeepsHeldSnapshot()
        {
            // Arrange
            using SqlConnection connection = CreateConnection(SqlConnectionEncryptOption.Mandatory);
            connection.Open();
            SqlConnectionEncryptionInfo held = connection.EncryptionInfo;

            // Act
            connection.Close();

            // Assert
            AssertEmpty(connection.EncryptionInfo);
            AssertNegotiated(held, SqlConnectionEncryptionMode.Full);
        }

        /// <summary>
        /// Reusing a pooled physical connection publishes the snapshot captured when that
        /// physical connection logged in.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.IsTCPConnStringSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void Open_PooledReuse_PublishesSavedSnapshot()
        {
            // Arrange
            // A unique application name and a single-connection pool force reuse of one
            // physical connection.
            SqlConnectionStringBuilder builder = CreateBuilder(SqlConnectionEncryptOption.Mandatory);
            builder.Pooling = true;
            builder.MaxPoolSize = 1;
            builder.ApplicationName = $"EncryptionInfo-{Guid.NewGuid():N}";
            SqlConnectionEncryptionInfo first;
            using (SqlConnection connection = DataTestUtility.CreateConnection(builder.ConnectionString))
            {
                connection.Open();
                first = connection.EncryptionInfo;
            }

            using SqlConnection reused = DataTestUtility.CreateConnection(builder.ConnectionString);
            try
            {
                // Act
                reused.Open();

                // Assert
                Assert.Same(first, reused.EncryptionInfo);
                AssertNegotiated(reused.EncryptionInfo, SqlConnectionEncryptionMode.Full);
            }
            finally
            {
                // Cleanup
                SqlConnection.ClearPool(reused);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates a non-pooled connection string builder for the configured TCP server with the
        /// given encryption option. Certificate validation is skipped except under strict
        /// encryption, where the server certificate must be trusted.
        /// </summary>
        private static SqlConnectionStringBuilder CreateBuilder(SqlConnectionEncryptOption encrypt)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Encrypt = encrypt,
                Pooling = false,
            };
            if (encrypt != SqlConnectionEncryptOption.Strict)
            {
                builder.TrustServerCertificate = true;
            }
            return builder;
        }

        /// <summary>
        /// Creates a connection to the configured TCP server with the given encryption option.
        /// </summary>
        private static SqlConnection CreateConnection(SqlConnectionEncryptOption encrypt) =>
            DataTestUtility.CreateConnection(CreateBuilder(encrypt).ConnectionString);

        /// <summary>
        /// Creates a connection with mandatory encryption that uses SQL authentication with a
        /// login and password generated at run time, so the server rejects the login after the
        /// TLS handshake.
        /// </summary>
        private static SqlConnection CreateInvalidLoginConnection()
        {
            SqlConnectionStringBuilder builder = CreateBuilder(SqlConnectionEncryptOption.Mandatory);
            builder.IntegratedSecurity = false;
            builder.Authentication = SqlAuthenticationMethod.NotSpecified;
            builder.UserID = $"EncryptionInfo_{Guid.NewGuid():N}";
            builder.Password = Guid.NewGuid().ToString("N");
            return new SqlConnection(builder.ConnectionString);
        }

        /// <summary>
        /// Returns the mode an open connection with optional encryption is expected to report,
        /// based on whether the server encrypts the whole session.
        /// </summary>
        private static SqlConnectionEncryptionMode ExpectedOptionalMode(SqlConnection connection)
        {
            using SqlCommand command = new(
                "SELECT encrypt_option FROM sys.dm_exec_connections WHERE session_id = @@SPID",
                connection);
            string encryptOption = (string)command.ExecuteScalar();
            return string.Equals(encryptOption, "TRUE", StringComparison.OrdinalIgnoreCase)
                ? SqlConnectionEncryptionMode.Full
                : SqlConnectionEncryptionMode.LoginOnly;
        }

        /// <summary>
        /// Asserts the negotiated mode, and that a TLS 1.2 or TLS 1.3 protocol was negotiated.
        /// </summary>
        private static void AssertNegotiated(
            SqlConnectionEncryptionInfo info,
            SqlConnectionEncryptionMode expectedMode)
        {
            Assert.Equal(expectedMode, info.NegotiatedEncryptionMode);
            Assert.True(
                info.NegotiatedEncryptionProtocol is SslProtocols.Tls12 or Tls13,
                $"Unexpected protocol: {info.NegotiatedEncryptionProtocol}");
        }

        /// <summary>
        /// Asserts that encryption info reports no values.
        /// </summary>
        private static void AssertEmpty(SqlConnectionEncryptionInfo info)
        {
            Assert.Null(info.NegotiatedEncryptionMode);
            Assert.Null(info.NegotiatedEncryptionProtocol);
            Assert.Null(info.NegotiatedCipherSuiteId);
            Assert.Null(info.ValidationErrors);
            Assert.Null(info.ChainStatus);
            Assert.Null(info.GetServerCertificate());
        }

        #endregion
    }
}
