// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Unit tests for the <see cref="SqlConnectionEncryptionInfo"/> public API surface and the
    /// <see cref="SqlConnection.EncryptionInfo"/> and <see cref="SqlException.EncryptionInfo"/>
    /// getters.
    /// </summary>
    public class SqlConnectionEncryptionInfoTests
    {
        private const string ServerVersion = "16.00.1000";
        private const string SampleThumbprint = "0123456789ABCDEF0123456789ABCDEF01234567";

        #region Empty snapshot

        /// <summary>
        /// Verifies that the shared empty snapshot reports every field as unavailable.
        /// </summary>
        [Fact]
        public void Empty_AllPropertiesAreNull()
        {
            SqlConnectionEncryptionInfo info = SqlConnectionEncryptionInfo.Empty;

            AssertEmpty(info);
            Assert.True(info.IsEmpty);
        }

        /// <summary>
        /// Verifies that the empty snapshot is a single shared instance.
        /// </summary>
        [Fact]
        public void Empty_IsSharedInstance()
        {
            Assert.Same(SqlConnectionEncryptionInfo.Empty, SqlConnectionEncryptionInfo.Empty);
        }

        /// <summary>
        /// Verifies that constructing a snapshot with every argument null is equivalent to empty.
        /// </summary>
        [Fact]
        public void Constructor_AllNull_IsEmpty()
        {
            SqlConnectionEncryptionInfo info = new(null, null, null, null, null);

            AssertEmpty(info);
            Assert.True(info.IsEmpty);
        }

        /// <summary>
        /// Verifies the type is sealed and exposes no public constructors.
        /// </summary>
        [Fact]
        public void Type_IsSealedWithNoPublicConstructors()
        {
            Type type = typeof(SqlConnectionEncryptionInfo);

            Assert.True(type.IsSealed);
            Assert.Empty(type.GetConstructors());
        }

        /// <summary>
        /// Verifies every public property is get-only so the snapshot is immutable.
        /// </summary>
        [Fact]
        public void Type_PublicPropertiesAreReadOnly()
        {
            foreach (var property in typeof(SqlConnectionEncryptionInfo).GetProperties())
            {
                Assert.True(property.CanRead, property.Name);
                Assert.Null(property.GetSetMethod(nonPublic: true));
            }
        }

        #endregion

        #region SqlConnection getter

        /// <summary>
        /// Verifies the getter returns a non-null empty snapshot on a new connection.
        /// </summary>
        [Fact]
        public void SqlConnection_New_ReturnsEmptySnapshot()
        {
            using SqlConnection connection = new();

            Assert.NotNull(connection.EncryptionInfo);
            Assert.True(connection.EncryptionInfo.IsEmpty);
        }

        /// <summary>
        /// Verifies the getter returns a non-null empty snapshot on a connection with a
        /// connection string that has not been opened.
        /// </summary>
        [Fact]
        public void SqlConnection_WithConnectionString_ReturnsEmptySnapshot()
        {
            using SqlConnection connection = new("Data Source=localhost;Encrypt=Strict");

            AssertEmpty(connection.EncryptionInfo);
        }

        /// <summary>
        /// Verifies the getter returns a non-null empty snapshot after the connection is closed.
        /// </summary>
        [Fact]
        public void SqlConnection_Closed_ReturnsEmptySnapshot()
        {
            using SqlConnection connection = new("Data Source=localhost");
            connection.Close();

            AssertEmpty(connection.EncryptionInfo);
        }

        /// <summary>
        /// Verifies the getter does not throw and returns a non-null empty snapshot after the
        /// connection is disposed.
        /// </summary>
        [Fact]
        public void SqlConnection_Disposed_ReturnsEmptySnapshot()
        {
            SqlConnection connection = new("Data Source=localhost");
            connection.Dispose();

            AssertEmpty(connection.EncryptionInfo);
        }

        #endregion

        #region SqlException getter

        /// <summary>
        /// Verifies exceptions created via the error-collection factory return an empty snapshot.
        /// </summary>
        [Fact]
        public void SqlException_CreateFromCollection_ReturnsEmptySnapshot()
        {
            SqlException exception = SqlException.CreateException(CreateErrorCollection(), ServerVersion);

            AssertEmpty(exception.EncryptionInfo);
        }

        /// <summary>
        /// Verifies exceptions created via the single-error factory return an empty snapshot.
        /// </summary>
        [Fact]
        public void SqlException_CreateFromError_ReturnsEmptySnapshot()
        {
            SqlException exception = SqlException.CreateException(
                CreateError(),
                ServerVersion,
                internalConnection: null,
                innerException: new InvalidOperationException("inner"));

            AssertEmpty(exception.EncryptionInfo);
        }

        /// <summary>
        /// Verifies exceptions created via the connection-id factory return an empty snapshot.
        /// </summary>
        [Fact]
        public void SqlException_CreateWithConnectionId_ReturnsEmptySnapshot()
        {
            SqlException exception = SqlException.CreateException(
                CreateErrorCollection(),
                ServerVersion,
                Guid.NewGuid(),
                innerException: null);

            AssertEmpty(exception.EncryptionInfo);
        }

        /// <summary>
        /// Verifies exceptions created via the batch-command factory return an empty snapshot.
        /// </summary>
        [Fact]
        public void SqlException_CreateWithBatchCommand_ReturnsEmptySnapshot()
        {
            SqlException exception = SqlException.CreateException(
                CreateErrorCollection(),
                ServerVersion,
                new SqlBatchCommand("SELECT 1"));

            AssertEmpty(exception.EncryptionInfo);
        }

        /// <summary>
        /// Verifies that a populated snapshot set internally is returned by the getter and is
        /// preserved by <see cref="SqlException.InternalClone"/>.
        /// </summary>
        [Fact]
        public void SqlException_PopulatedSnapshot_PreservedByInternalClone()
        {
            SqlConnectionEncryptionInfo info = CreatePopulated();
            SqlException exception = SqlException.CreateException(CreateErrorCollection(), ServerVersion);
            exception.EncryptionInfo = info;

            SqlException clone = exception.InternalClone();

            Assert.Same(info, exception.EncryptionInfo);
            Assert.Same(info, clone.EncryptionInfo);
        }

        /// <summary>
        /// Verifies that clearing the snapshot internally falls back to the empty snapshot.
        /// </summary>
        [Fact]
        public void SqlException_SetNull_ReturnsEmptySnapshot()
        {
            SqlException exception = SqlException.CreateException(CreateErrorCollection(), ServerVersion);
            exception.EncryptionInfo = CreatePopulated();
            exception.EncryptionInfo = null!;

            AssertEmpty(exception.EncryptionInfo);
        }

        /// <summary>
        /// Verifies that a deserialized exception returns a non-null empty snapshot and that the
        /// snapshot is not written to the serialized payload.
        /// </summary>
        [Fact]
        public void SqlException_DataContractRoundTrip_ReturnsEmptySnapshot()
        {
            SqlException original = SqlException.CreateException(CreateErrorCollection(), ServerVersion);
            original.EncryptionInfo = CreatePopulated();

            // Exception.Data is backed by an internal dictionary type that must be a known type.
            DataContractSerializer serializer = new(
                typeof(SqlException),
                new[] { original.Data.GetType() });
            using MemoryStream stream = new();
            serializer.WriteObject(stream, original);

            stream.Position = 0;
            string payload = new StreamReader(stream).ReadToEnd();
            Assert.DoesNotContain(nameof(SqlException.EncryptionInfo), payload);
            Assert.DoesNotContain(SampleThumbprint, payload);

            stream.Position = 0;
            SqlException? roundTripped = serializer.ReadObject(stream) as SqlException;

            Assert.NotNull(roundTripped);
            Assert.Equal(original.Message, roundTripped.Message);
            AssertEmpty(roundTripped.EncryptionInfo);
        }

#if NETFRAMEWORK
        /// <summary>
        /// Verifies BinaryFormatter serialization is unaffected by the new non-serialized field and
        /// that the deserialized exception returns a non-null empty snapshot.
        /// </summary>
        [Fact]
        public void SqlException_BinaryFormatterRoundTrip_ReturnsEmptySnapshot()
        {
            SqlException original = SqlException.CreateException(CreateErrorCollection(), ServerVersion);
            original.EncryptionInfo = CreatePopulated();

            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            using MemoryStream stream = new();
            formatter.Serialize(stream, original);
            stream.Position = 0;
            SqlException roundTripped = (SqlException)formatter.Deserialize(stream);

            Assert.Equal(original.Message, roundTripped.Message);
            AssertEmpty(roundTripped.EncryptionInfo);
        }
#endif

        /// <summary>
        /// Verifies that <see cref="Exception.Message"/> and <see cref="SqlException.ToString"/>
        /// do not change when a populated snapshot is attached.
        /// </summary>
        [Fact]
        public void SqlException_MessageAndToString_UnchangedByPopulatedSnapshot()
        {
            Guid connectionId = Guid.NewGuid();
            SqlException withoutInfo = SqlException.CreateException(CreateErrorCollection(), ServerVersion, connectionId, null);
            SqlException withInfo = SqlException.CreateException(CreateErrorCollection(), ServerVersion, connectionId, null);
            withInfo.EncryptionInfo = CreatePopulated();

            Assert.Equal(withoutInfo.Message, withInfo.Message);
            Assert.Equal(withoutInfo.ToString(), withInfo.ToString());
            Assert.DoesNotContain(SampleThumbprint, withInfo.ToString());
            Assert.DoesNotContain(nameof(SqlConnectionEncryptionMode.Strict), withInfo.Message);
        }

        #endregion

        #region Populated snapshots

        /// <summary>
        /// Verifies that values supplied to the internal constructor round-trip unchanged.
        /// </summary>
        [Fact]
        public void Constructor_Populated_RoundTripsValues()
        {
            SqlConnectionEncryptionInfo info = CreatePopulated();

            Assert.Equal(SqlConnectionEncryptionMode.Strict, info.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
            Assert.Equal(SampleThumbprint, info.PresentedServerCertificateThumbprint);
            Assert.Equal(SqlServerCertificateValidationErrors.HostNameMismatch, info.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, info.ChainStatus);
            Assert.False(info.IsEmpty);
        }

        /// <summary>
        /// Verifies that each field independently controls <see cref="SqlConnectionEncryptionInfo.IsEmpty"/>.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void Constructor_SingleFieldSet_IsNotEmpty(int fieldIndex)
        {
            SqlConnectionEncryptionInfo info = new(
                fieldIndex == 0 ? SqlConnectionEncryptionMode.None : null,
                fieldIndex == 1 ? SslProtocols.None : null,
                fieldIndex == 2 ? string.Empty : null,
                fieldIndex == 3 ? SqlServerCertificateValidationErrors.None : null,
                fieldIndex == 4 ? X509ChainStatusFlags.NoError : null);

            Assert.False(info.IsEmpty);
        }

        /// <summary>
        /// Verifies that zero-valued enum members are distinct from null (not available).
        /// </summary>
        [Fact]
        public void Constructor_ZeroValues_DistinctFromNull()
        {
            SqlConnectionEncryptionInfo info = new(
                SqlConnectionEncryptionMode.None,
                SslProtocols.None,
                presentedServerCertificateThumbprint: null,
                SqlServerCertificateValidationErrors.None,
                X509ChainStatusFlags.NoError);

            Assert.True(info.NegotiatedEncryptionMode.HasValue);
            Assert.Equal(SqlConnectionEncryptionMode.None, info.NegotiatedEncryptionMode);
            Assert.True(info.NegotiatedEncryptionProtocol.HasValue);
            Assert.Equal(SslProtocols.None, info.NegotiatedEncryptionProtocol);
            Assert.True(info.ValidationErrors.HasValue);
            Assert.Equal(SqlServerCertificateValidationErrors.None, info.ValidationErrors);
            Assert.True(info.ChainStatus.HasValue);
            Assert.Equal(X509ChainStatusFlags.NoError, info.ChainStatus);
            Assert.Null(info.PresentedServerCertificateThumbprint);
        }

        /// <summary>
        /// Verifies that combined validation-error and chain-status flags round-trip unchanged.
        /// </summary>
        [Fact]
        public void Constructor_FlagCombinations_RoundTrip()
        {
            const SqlServerCertificateValidationErrors errors =
                SqlServerCertificateValidationErrors.HostNameMismatch |
                SqlServerCertificateValidationErrors.ChainError |
                SqlServerCertificateValidationErrors.PinnedCertificateMismatch;
            const X509ChainStatusFlags chain =
                X509ChainStatusFlags.UntrustedRoot |
                X509ChainStatusFlags.NotTimeValid |
                X509ChainStatusFlags.PartialChain;

            SqlConnectionEncryptionInfo info = new(null, null, null, errors, chain);

            Assert.Equal(errors, info.ValidationErrors);
            Assert.True(info.ValidationErrors!.Value.HasFlag(SqlServerCertificateValidationErrors.ChainError));
            Assert.False(info.ValidationErrors!.Value.HasFlag(SqlServerCertificateValidationErrors.ServerCertificateMissing));
            Assert.Equal(chain, info.ChainStatus);
            Assert.True(info.ChainStatus!.Value.HasFlag(X509ChainStatusFlags.PartialChain));
        }

        /// <summary>
        /// Verifies that a protocol value without a named member on the current runtime (for
        /// example TLS 1.3 on .NET Framework) is preserved numerically.
        /// </summary>
        [Fact]
        public void Constructor_NumericProtocolValue_IsPreserved()
        {
            const SslProtocols tls13 = (SslProtocols)0x3000;

            SqlConnectionEncryptionInfo info = new(SqlConnectionEncryptionMode.Strict, tls13, null, null, null);

            Assert.Equal(0x3000, (int)info.NegotiatedEncryptionProtocol!.Value);
        }

        /// <summary>
        /// Verifies that the certificate identifier cannot be mutated through the snapshot.
        /// The identifier is a <see cref="string"/>, which is immutable, so no defensive copy is
        /// required; this test guards against a future change to a mutable representation.
        /// </summary>
        [Fact]
        public void PresentedServerCertificateThumbprint_IsImmutable()
        {
            Assert.Equal(
                typeof(string),
                typeof(SqlConnectionEncryptionInfo)
                    .GetProperty(nameof(SqlConnectionEncryptionInfo.PresentedServerCertificateThumbprint))!
                    .PropertyType);

            SqlConnectionEncryptionInfo info = CreatePopulated();

            Assert.Same(info.PresentedServerCertificateThumbprint, info.PresentedServerCertificateThumbprint);
            Assert.Equal(SampleThumbprint, info.PresentedServerCertificateThumbprint);
        }

        #endregion

        #region Enum contracts

        /// <summary>
        /// Verifies the numeric values of <see cref="SqlConnectionEncryptionMode"/>.
        /// </summary>
        [Fact]
        public void SqlConnectionEncryptionMode_HasExpectedValues()
        {
            Assert.Equal(0, (int)SqlConnectionEncryptionMode.None);
            Assert.Equal(1, (int)SqlConnectionEncryptionMode.LoginOnly);
            Assert.Equal(2, (int)SqlConnectionEncryptionMode.Full);
            Assert.Equal(3, (int)SqlConnectionEncryptionMode.Strict);
            Assert.Equal(4, Enum.GetValues(typeof(SqlConnectionEncryptionMode)).Length);
        }

        /// <summary>
        /// Verifies the numeric values and flags attribute of
        /// <see cref="SqlServerCertificateValidationErrors"/>.
        /// </summary>
        [Fact]
        public void SqlServerCertificateValidationErrors_HasExpectedValues()
        {
            Assert.True(typeof(SqlServerCertificateValidationErrors).IsDefined(typeof(FlagsAttribute), inherit: false));
            Assert.Equal(0, (int)SqlServerCertificateValidationErrors.None);
            Assert.Equal(1, (int)SqlServerCertificateValidationErrors.ServerCertificateMissing);
            Assert.Equal(2, (int)SqlServerCertificateValidationErrors.HostNameMismatch);
            Assert.Equal(4, (int)SqlServerCertificateValidationErrors.ChainError);
            Assert.Equal(8, (int)SqlServerCertificateValidationErrors.PinnedCertificateMismatch);
            Assert.Equal(5, Enum.GetValues(typeof(SqlServerCertificateValidationErrors)).Length);
        }

        #endregion

        #region Helpers

        private static void AssertEmpty(SqlConnectionEncryptionInfo? info)
        {
            Assert.NotNull(info);
            Assert.Null(info.NegotiatedEncryptionMode);
            Assert.Null(info.NegotiatedEncryptionProtocol);
            Assert.Null(info.PresentedServerCertificateThumbprint);
            Assert.Null(info.ValidationErrors);
            Assert.Null(info.ChainStatus);
        }

        private static SqlConnectionEncryptionInfo CreatePopulated() =>
            new(
                SqlConnectionEncryptionMode.Strict,
                SslProtocols.Tls12,
                SampleThumbprint,
                SqlServerCertificateValidationErrors.HostNameMismatch,
                X509ChainStatusFlags.UntrustedRoot);

        private static SqlError CreateError() =>
            new(
                infoNumber: 50000,
                errorState: 1,
                errorClass: 16,
                server: "server",
                errorMessage: "Test error message.",
                procedure: "proc",
                lineNumber: 1,
                exception: null,
                batchIndex: -1);

        private static SqlErrorCollection CreateErrorCollection() => new() { CreateError() };

        #endregion
    }
}
