// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using static Microsoft.Data.SqlClient.UnitTests.Utilities.EncryptionInfoTestData;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Unit tests for the <see cref="SqlConnectionEncryptionInfo"/> public API surface.
    /// </summary>
    public class SqlConnectionEncryptionInfoTests
    {
        #region Empty snapshot

        /// <summary>
        /// Verifies that the shared empty snapshot reports every field as unavailable.
        /// </summary>
        [Fact]
        public void Empty_AllPropertiesAreNull()
        {
            // Act
            SqlConnectionEncryptionInfo info = SqlConnectionEncryptionInfo.Empty;

            // Assert
            AssertEmpty(info);
        }

        /// <summary>
        /// Verifies that the empty snapshot is a single shared instance.
        /// </summary>
        [Fact]
        public void Empty_IsSharedInstance()
        {
            // Act & Assert
            Assert.Same(SqlConnectionEncryptionInfo.Empty, SqlConnectionEncryptionInfo.Empty);
        }

        /// <summary>
        /// Verifies that constructing a snapshot with every argument null is equivalent to empty.
        /// </summary>
        [Fact]
        public void Constructor_AllNull_IsEmpty()
        {
            // Act
            SqlConnectionEncryptionInfo info = new(null, null, null, null, null);

            // Assert
            AssertEmpty(info);
        }

        /// <summary>
        /// Verifies the type is sealed and exposes no public constructors.
        /// </summary>
        [Fact]
        public void Type_IsSealedWithNoPublicConstructors()
        {
            // Act
            Type type = typeof(SqlConnectionEncryptionInfo);

            // Assert
            Assert.True(type.IsSealed);
            Assert.Empty(type.GetConstructors());
        }

        /// <summary>
        /// Verifies every public property is get-only so the snapshot is immutable.
        /// </summary>
        [Fact]
        public void Type_PublicPropertiesAreReadOnly()
        {
            // Act
            PropertyInfo[] properties = typeof(SqlConnectionEncryptionInfo).GetProperties();

            // Assert
            foreach (PropertyInfo property in properties)
            {
                Assert.True(property.CanRead, property.Name);
                Assert.Null(property.GetSetMethod(nonPublic: true));
            }
        }

        #endregion

        #region Populated snapshots

        /// <summary>
        /// Verifies that values supplied to the internal constructor round-trip unchanged.
        /// </summary>
        [Fact]
        public void Constructor_Populated_RoundTripsValues()
        {
            // Act
            SqlConnectionEncryptionInfo info = CreatePopulated();
            using X509Certificate2? certificate = info.GetServerCertificate();

            // Assert
            Assert.Equal(SqlConnectionEncryptionMode.Strict, info.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
            Assert.Equal(SampleCipherSuite, info.NegotiatedCipherSuiteId);
#if NET
            Assert.Equal(System.Net.Security.TlsCipherSuite.TLS_AES_256_GCM_SHA384, info.NegotiatedCipherSuite);
#endif
            ReadOnlyMemory<byte> rawData = Assert.NotNull(info.PresentedServerCertificateRawData);
            Assert.Equal(CreateSampleCertificate(), rawData.ToArray());
            Assert.NotNull(certificate);
            Assert.Equal(SampleCertificateThumbprint, certificate.Thumbprint);
            Assert.Equal(SqlServerCertificateValidationErrors.HostNameMismatch, info.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, info.ChainStatus);
            Assert.False(info.IsEmpty);
        }

        /// <summary>
        /// Verifies that each field independently controls <see cref="SqlConnectionEncryptionInfo.IsEmpty"/>,
        /// and that the field that was supplied is the one reported.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void Constructor_SingleFieldSet_IsNotEmpty(int fieldIndex)
        {
            // Act
            SqlConnectionEncryptionInfo info = new(
                fieldIndex == 0 ? SqlConnectionEncryptionMode.None : null,
                fieldIndex == 1 ? SslProtocols.None : null,
                fieldIndex == 2 ? CreateSampleCertificate() : null,
                fieldIndex == 3 ? SqlServerCertificateValidationErrors.None : null,
                fieldIndex == 4 ? X509ChainStatusFlags.NoError : null,
                fieldIndex == 5 ? ToConstructorCipherSuite(0) : null);

            // Assert
            object? supplied = fieldIndex switch
            {
                0 => info.NegotiatedEncryptionMode,
                1 => info.NegotiatedEncryptionProtocol,
                2 => info.PresentedServerCertificateRawData,
                3 => info.ValidationErrors,
                4 => info.ChainStatus,
                _ => info.NegotiatedCipherSuiteId,
            };
            Assert.NotNull(supplied);
            Assert.False(info.IsEmpty);
        }

        /// <summary>
        /// Verifies that zero-valued enum members are distinct from null (not available).
        /// </summary>
        [Fact]
        public void Constructor_ZeroValues_DistinctFromNull()
        {
            // Act
            SqlConnectionEncryptionInfo info = new(
                SqlConnectionEncryptionMode.None,
                SslProtocols.None,
                presentedServerCertificate: null,
                SqlServerCertificateValidationErrors.None,
                X509ChainStatusFlags.NoError);

            // Assert
            Assert.Equal(SqlConnectionEncryptionMode.None, Assert.NotNull(info.NegotiatedEncryptionMode));
            Assert.Equal(SslProtocols.None, Assert.NotNull(info.NegotiatedEncryptionProtocol));
            Assert.Equal(SqlServerCertificateValidationErrors.None, Assert.NotNull(info.ValidationErrors));
            Assert.Equal(X509ChainStatusFlags.NoError, Assert.NotNull(info.ChainStatus));
            Assert.Null(info.PresentedServerCertificateRawData);
            Assert.Null(info.GetServerCertificate());
        }

        /// <summary>
        /// Verifies that combined validation-error and chain-status flags round-trip unchanged.
        /// </summary>
        [Fact]
        public void Constructor_FlagCombinations_RoundTrip()
        {
            // Arrange
            const SqlServerCertificateValidationErrors errors =
                SqlServerCertificateValidationErrors.HostNameMismatch |
                SqlServerCertificateValidationErrors.ChainError |
                SqlServerCertificateValidationErrors.PinnedCertificateMismatch;
            const X509ChainStatusFlags chain =
                X509ChainStatusFlags.UntrustedRoot |
                X509ChainStatusFlags.NotTimeValid |
                X509ChainStatusFlags.PartialChain;

            // Act
            SqlConnectionEncryptionInfo info = new(null, null, null, errors, chain);

            // Assert
            SqlServerCertificateValidationErrors actualErrors = Assert.NotNull(info.ValidationErrors);
            Assert.Equal(errors, actualErrors);
            Assert.True(actualErrors.HasFlag(SqlServerCertificateValidationErrors.ChainError));
            Assert.False(actualErrors.HasFlag(SqlServerCertificateValidationErrors.ServerCertificateMissing));
            X509ChainStatusFlags actualChain = Assert.NotNull(info.ChainStatus);
            Assert.Equal(chain, actualChain);
            Assert.True(actualChain.HasFlag(X509ChainStatusFlags.PartialChain));
        }

        /// <summary>
        /// Verifies that a protocol value without a named member on the current runtime (for
        /// example TLS 1.3 on .NET Framework) is preserved numerically.
        /// </summary>
        [Fact]
        public void Constructor_NumericProtocolValue_IsPreserved()
        {
            // Arrange
            const SslProtocols tls13 = (SslProtocols)0x3000;

            // Act
            SqlConnectionEncryptionInfo info = new(SqlConnectionEncryptionMode.Strict, tls13, null, null, null);

            // Assert
            Assert.Equal(0x3000, (int)Assert.NotNull(info.NegotiatedEncryptionProtocol));
        }

        #endregion

        #region Negotiated cipher suite

        /// <summary>
        /// Verifies that the negotiated cipher suite defaults to null (not available) when it is not
        /// supplied, even if other fields are populated.
        /// </summary>
        [Fact]
        public void NegotiatedCipherSuite_NotSupplied_IsNull()
        {
            // Act
            SqlConnectionEncryptionInfo info = new(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12, null, null, null);

            // Assert
            Assert.Null(info.NegotiatedCipherSuiteId);
#if NET
            Assert.Null(info.NegotiatedCipherSuite);
#endif
            Assert.False(info.IsEmpty);
        }

        /// <summary>
        /// Verifies that IANA cipher suite identifiers, including the boundary values, round-trip unchanged.
        /// A value of zero (TLS_NULL_WITH_NULL_NULL) is distinct from null.
        /// </summary>
        [Theory]
        [InlineData(0x0000)]
        [InlineData(0x1301)]
        [InlineData(0x1302)]
        [InlineData(0xC030)]
        [InlineData(0xFFFF)]
        public void NegotiatedCipherSuite_Value_RoundTrips(int cipherSuite)
        {
            // Act
            SqlConnectionEncryptionInfo info = CreateWithCipherSuite(cipherSuite);

            // Assert
            Assert.Equal(cipherSuite, info.NegotiatedCipherSuiteId);
#if NET
            Assert.Equal((System.Net.Security.TlsCipherSuite)cipherSuite, info.NegotiatedCipherSuite);
#endif
            Assert.False(info.IsEmpty);
        }

#if NETFRAMEWORK
        /// <summary>
        /// Verifies that a value outside the 16-bit IANA identifier range is rejected. On .NET the
        /// constructor takes a <c>TlsCipherSuite</c>, so such a value cannot be supplied.
        /// </summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(0x10000)]
        [InlineData(int.MaxValue)]
        public void Constructor_InvalidCipherSuite_Throws(int cipherSuite)
        {
            // Act
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new SqlConnectionEncryptionInfo(null, null, null, null, null, cipherSuite));

            // Assert
            Assert.Equal("negotiatedCipherSuite", ex.ParamName);
        }
#endif

        /// <summary>
        /// Verifies that a valid 16-bit identifier that is not a registered cipher suite or has no
        /// named <c>TlsCipherSuite</c> member (for example, a GREASE value) is preserved numerically
        /// and not filtered out.
        /// </summary>
        [Theory]
        [InlineData(0x0A0A)]
        [InlineData(0xF00F)]
        [InlineData(0xFAFA)]
        public void NegotiatedCipherSuite_UnknownIdentifier_IsPreserved(int cipherSuite)
        {
            // Act
            SqlConnectionEncryptionInfo info = CreateWithCipherSuite(cipherSuite);

            // Assert
            Assert.Equal(cipherSuite, info.NegotiatedCipherSuiteId);
#if NET
            Assert.False(Enum.IsDefined(typeof(System.Net.Security.TlsCipherSuite), (ushort)cipherSuite));
            Assert.Equal((System.Net.Security.TlsCipherSuite)cipherSuite, info.NegotiatedCipherSuite);
#endif
        }

#if NET
        /// <summary>
        /// Verifies that a known IANA identifier maps to the matching named <c>TlsCipherSuite</c> member.
        /// </summary>
        [Fact]
        public void NegotiatedCipherSuite_KnownIdentifier_MapsToNamedMember()
        {
            // Act
            SqlConnectionEncryptionInfo info = new(
                null, null, null, null, null, System.Net.Security.TlsCipherSuite.TLS_AES_256_GCM_SHA384);

            // Assert
            Assert.Equal(System.Net.Security.TlsCipherSuite.TLS_AES_256_GCM_SHA384, info.NegotiatedCipherSuite);
            Assert.Equal(0x1302, info.NegotiatedCipherSuiteId);
        }
#endif

        /// <summary>
        /// Verifies that <c>NegotiatedCipherSuiteId</c> is a read-only <c>int?</c> on every target
        /// framework and carries no <see cref="CLSCompliantAttribute"/>, so it inherits the assembly's
        /// CLS compliance and has an identical signature everywhere.
        /// </summary>
        [Fact]
        public void NegotiatedCipherSuiteId_PropertyShape_IsSameOnEveryTargetFramework()
        {
            // Act
            PropertyInfo? property =
                typeof(SqlConnectionEncryptionInfo).GetProperty(nameof(SqlConnectionEncryptionInfo.NegotiatedCipherSuiteId));

            // Assert
            Assert.NotNull(property);
            Assert.Equal(typeof(int?), property.PropertyType);
            Assert.True(property.CanRead);
            Assert.False(property.CanWrite);
            MethodInfo? getter = property.GetMethod;
            Assert.NotNull(getter);
            Assert.True(getter.IsPublic);
            Assert.Null(Attribute.GetCustomAttribute(property, typeof(CLSCompliantAttribute)));
            Assert.Null(Attribute.GetCustomAttribute(getter, typeof(CLSCompliantAttribute)));
        }

        /// <summary>
        /// Verifies that the strongly typed <c>NegotiatedCipherSuite</c> property exists only on .NET,
        /// as a read-only <c>TlsCipherSuite?</c> marked not CLS-compliant.
        /// </summary>
        [Fact]
        public void NegotiatedCipherSuite_PropertyShape_MatchesTargetFramework()
        {
            // Act
            PropertyInfo? property =
                typeof(SqlConnectionEncryptionInfo).GetProperty("NegotiatedCipherSuite");

            // Assert
#if NET
            Assert.NotNull(property);
            Assert.Equal(typeof(System.Net.Security.TlsCipherSuite?), property.PropertyType);
            Assert.True(property.CanRead);
            Assert.False(property.CanWrite);
            CLSCompliantAttribute? cls =
                (CLSCompliantAttribute?)Attribute.GetCustomAttribute(property, typeof(CLSCompliantAttribute));
            Assert.NotNull(cls);
            Assert.False(cls.IsCompliant);
#else
            Assert.Null(property);
#endif
        }

        /// <summary>
        /// Verifies that the assembly remains CLS-compliant overall.
        /// </summary>
        [Fact]
        public void Assembly_RemainsClsCompliant()
        {
            // Act
            CLSCompliantAttribute? cls = (CLSCompliantAttribute?)Attribute.GetCustomAttribute(
                typeof(SqlConnectionEncryptionInfo).Assembly, typeof(CLSCompliantAttribute));

            // Assert
            Assert.NotNull(cls);
            Assert.True(cls.IsCompliant);
        }

        #endregion

        #region Server certificate

        /// <summary>
        /// Verifies that the certificate is exposed as a public method returning
        /// <see cref="X509Certificate2"/> and that no hash property remains.
        /// </summary>
        [Fact]
        public void GetServerCertificate_PublicShape()
        {
            // Arrange
            Type type = typeof(SqlConnectionEncryptionInfo);

            // Act
            MethodInfo? method = type.GetMethod(
                nameof(SqlConnectionEncryptionInfo.GetServerCertificate),
                Type.EmptyTypes);

            // Assert
            Assert.NotNull(method);
            Assert.True(method.IsPublic);
            Assert.Equal(typeof(X509Certificate2), method.ReturnType);
            Assert.Null(type.GetProperty("PresentedServerCertificateHash256"));
            Assert.Null(type.GetProperty("ServerCertificate"));
            Assert.Null(type.GetProperty("PresentedServerCertificateRawData"));
        }

        /// <summary>
        /// Verifies that the returned certificate matches the captured DER bytes.
        /// </summary>
        [Fact]
        public void GetServerCertificate_RoundTripsCertificate()
        {
            // Arrange
            byte[] der = CreateSampleCertificate();
            SqlConnectionEncryptionInfo info = new(null, null, der, null, null);

            // Act
            using X509Certificate2? certificate = info.GetServerCertificate();

            // Assert
            Assert.NotNull(certificate);
            Assert.Equal(SampleCertificateThumbprint, certificate.Thumbprint);
            Assert.Equal(SampleCertificateSubject, certificate.Subject);
            Assert.Equal(der, certificate.RawData);
            Assert.False(certificate.HasPrivateKey);
        }

        /// <summary>
        /// Verifies that every call returns a new, independently owned instance, and that
        /// disposing one does not affect later calls.
        /// </summary>
        [Fact]
        public void GetServerCertificate_ReturnsNewInstancePerCall()
        {
            // Arrange
            SqlConnectionEncryptionInfo info = CreatePopulated();

            // Act
            X509Certificate2? first = info.GetServerCertificate();
            using X509Certificate2? second = info.GetServerCertificate();
            Assert.NotNull(first);
            first.Dispose();
            using X509Certificate2? third = info.GetServerCertificate();

            // Assert
            Assert.NotNull(second);
            Assert.NotNull(third);
            Assert.NotSame(first, second);
            Assert.NotSame(second, third);
            Assert.Equal(SampleCertificateThumbprint, second.Thumbprint);
            Assert.Equal(SampleCertificateThumbprint, third.Thumbprint);
        }

        /// <summary>
        /// Verifies that mutating the caller's array after construction does not change the
        /// snapshot.
        /// </summary>
        [Fact]
        public void GetServerCertificate_SourceMutation_DoesNotAffectSnapshot()
        {
            // Arrange
            byte[] source = CreateSampleCertificate();
            SqlConnectionEncryptionInfo info = new(null, null, source, null, null);

            // Act
            Array.Clear(source, 0, source.Length);
            using X509Certificate2? certificate = info.GetServerCertificate();

            // Assert
            ReadOnlyMemory<byte> rawData = Assert.NotNull(info.PresentedServerCertificateRawData);
            Assert.Equal(CreateSampleCertificate(), rawData.ToArray());
            Assert.NotNull(certificate);
            Assert.Equal(SampleCertificateThumbprint, certificate.Thumbprint);
        }

        /// <summary>
        /// Verifies that mutating the raw data of a returned certificate does not change the
        /// snapshot.
        /// </summary>
        [Fact]
        public void GetServerCertificate_ReturnedRawDataMutation_DoesNotAffectSnapshot()
        {
            // Arrange
            SqlConnectionEncryptionInfo info = CreatePopulated();

            // Act
            using (X509Certificate2? certificate = info.GetServerCertificate())
            {
                Assert.NotNull(certificate);
                byte[] raw = certificate.RawData;
                Array.Clear(raw, 0, raw.Length);
            }
            using X509Certificate2? again = info.GetServerCertificate();

            // Assert
            Assert.NotNull(again);
            Assert.Equal(SampleCertificateThumbprint, again.Thumbprint);
        }

        /// <summary>
        /// Verifies that null and empty certificate bytes are both reported as unavailable and
        /// produce an empty snapshot.
        /// </summary>
        [Fact]
        public void GetServerCertificate_NullOrEmpty_ReturnsNull()
        {
            // Act
            SqlConnectionEncryptionInfo fromNull = new(null, null, null, null, null);
            SqlConnectionEncryptionInfo fromEmpty = new(null, null, Array.Empty<byte>(), null, null);

            // Assert
            AssertEmpty(fromNull);
            AssertEmpty(fromEmpty);
        }

        /// <summary>
        /// Verifies that bytes that cannot be decoded as a certificate are reported as unavailable
        /// instead of throwing.
        /// </summary>
        /// <remarks>
        /// The snapshot is still not empty: <see cref="SqlConnectionEncryptionInfo.IsEmpty"/>
        /// reports whether the driver captured anything, and decoding is deferred to
        /// <see cref="SqlConnectionEncryptionInfo.GetServerCertificate"/>. The driver's capture paths
        /// only supply certificates from a completed TLS handshake, so this state is not expected
        /// outside of tests.
        /// </remarks>
        [Theory]
        [InlineData(new byte[] { 0x01 })]
        [InlineData(new byte[] { 0x30, 0x82, 0x01, 0x00, 0x00 })]
        [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
        public void GetServerCertificate_UndecodableBytes_ReturnsNull(byte[] bytes)
        {
            // Arrange
            SqlConnectionEncryptionInfo info = new(null, null, bytes, null, null);

            // Act
            using X509Certificate2? certificate = info.GetServerCertificate();

            // Assert
            Assert.Null(certificate);
            Assert.False(info.IsEmpty);
        }

        /// <summary>
        /// Verifies that a captured certificate alone makes the snapshot non-empty.
        /// </summary>
        [Fact]
        public void GetServerCertificate_CertificateOnly_IsNotEmpty()
        {
            // Arrange
            SqlConnectionEncryptionInfo info = new(null, null, CreateSampleCertificate(), null, null);

            // Act
            using X509Certificate2? certificate = info.GetServerCertificate();

            // Assert
            Assert.NotNull(certificate);
            Assert.False(info.IsEmpty);
            Assert.Null(info.NegotiatedEncryptionMode);
            Assert.Null(info.ValidationErrors);
        }

        #endregion

        #region Enum contracts

        /// <summary>
        /// Verifies the underlying type and numeric values of <see cref="SqlConnectionEncryptionMode"/>.
        /// </summary>
        [Fact]
        public void SqlConnectionEncryptionMode_HasExpectedValues()
        {
            // Act
            Type underlyingType = Enum.GetUnderlyingType(typeof(SqlConnectionEncryptionMode));

            // Assert
            Assert.Equal(typeof(byte), underlyingType);
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
            // Act
            Type type = typeof(SqlServerCertificateValidationErrors);

            // Assert
            Assert.True(type.IsDefined(typeof(FlagsAttribute), inherit: false));
            Assert.Equal(typeof(int), Enum.GetUnderlyingType(type));
            Assert.Equal(0, (int)SqlServerCertificateValidationErrors.None);
            Assert.Equal(1, (int)SqlServerCertificateValidationErrors.ServerCertificateMissing);
            Assert.Equal(2, (int)SqlServerCertificateValidationErrors.HostNameMismatch);
            Assert.Equal(4, (int)SqlServerCertificateValidationErrors.ChainError);
            Assert.Equal(8, (int)SqlServerCertificateValidationErrors.PinnedCertificateMismatch);
            Assert.Equal(5, Enum.GetValues(type).Length);
        }

        #endregion
    }
}
