// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>
/// Shared data and assertions for tests of <see cref="SqlConnectionEncryptionInfo"/> and the
/// getters that expose it.
/// </summary>
internal static class EncryptionInfoTestData
{
    /// <summary>
    /// Server version reported by exceptions created through the internal factories.
    /// </summary>
    internal const string ServerVersion = "16.00.1000";

    /// <summary>
    /// Base64 DER encoding of a public-only, self-signed ECDSA P-256 test certificate
    /// (CN=encryption-info.test). It has no private key and is used only to verify DER
    /// round-tripping.
    /// </summary>
    internal const string SampleCertificateBase64 =
        "MIIBNDCB2qADAgECAggohG/gEdxs1TAKBggqhkjOPQQDAjAfMR0wGwYDVQQDExRlbmNyeXB0aW9uLWluZm8u" +
        "dGVzdDAgFw0yNjAxMDEwMDAwMDBaGA8yMTI2MDEwMTAwMDAwMFowHzEdMBsGA1UEAxMUZW5jcnlwdGlvbi1p" +
        "bmZvLnRlc3QwWTATBgcqhkjOPQIBBggqhkjOPQMBBwNCAATKtC9pE1F5JC1ciq1W/ANr5OBPgXrlvMpmPXMr" +
        "ectPyl630QJbCUT640lSLXUfdF3/8iTglUTiCyxg7SNUztJKMAoGCCqGSM49BAMCA0kAMEYCIQDoHA2MzlvfTWHm" +
        "n8Iioi8sMoKAnV/DcSrw50GwJf+fGAIhAPCezMdXBEwwpf4MU2fkmifrj36YES+JJM49BDRItkIl";

    /// <summary>
    /// SHA-1 thumbprint of the sample certificate, used to confirm the round-tripped identity.
    /// </summary>
    internal const string SampleCertificateThumbprint = "60820F552FF4F3CAAEC10B18EB468A16F01D7313";

    /// <summary>
    /// Subject distinguished name of the sample certificate.
    /// </summary>
    internal const string SampleCertificateSubject = "CN=encryption-info.test";

    /// <summary>
    /// IANA identifier for TLS_AES_256_GCM_SHA384.
    /// </summary>
    internal const int SampleCipherSuite = 0x1302;

    /// <summary>
    /// Creates a new array holding the DER encoding of the sample certificate.
    /// </summary>
    /// <returns>A new array that the caller may mutate.</returns>
    internal static byte[] CreateSampleCertificate() =>
        Convert.FromBase64String(SampleCertificateBase64);

    /// <summary>
    /// Creates a snapshot with only the negotiated cipher suite set, passing it to the internal
    /// constructor as the type it takes on the current target framework.
    /// </summary>
    /// <param name="cipherSuite">The IANA cipher suite identifier.</param>
    /// <returns>The snapshot.</returns>
    internal static SqlConnectionEncryptionInfo CreateWithCipherSuite(int cipherSuite) =>
        new(null, null, null, null, null, ToConstructorCipherSuite(cipherSuite));

    /// <summary>
    /// Creates a snapshot with every field populated with a distinct non-default value.
    /// </summary>
    /// <returns>The populated snapshot.</returns>
    internal static SqlConnectionEncryptionInfo CreatePopulated() =>
        new(
            SqlConnectionEncryptionMode.Strict,
            SslProtocols.Tls12,
            CreateSampleCertificate(),
            SqlServerCertificateValidationErrors.HostNameMismatch,
            X509ChainStatusFlags.UntrustedRoot,
            ToConstructorCipherSuite(SampleCipherSuite));

#if NET
    /// <summary>
    /// Converts an IANA identifier to the cipher suite type the internal constructor takes on .NET.
    /// </summary>
    /// <param name="cipherSuite">The IANA cipher suite identifier.</param>
    /// <returns>The identifier as a <see cref="System.Net.Security.TlsCipherSuite"/>.</returns>
    internal static System.Net.Security.TlsCipherSuite ToConstructorCipherSuite(int cipherSuite) =>
        (System.Net.Security.TlsCipherSuite)cipherSuite;
#else
    /// <summary>
    /// Returns the IANA identifier unchanged, matching the type the internal constructor takes on
    /// .NET Framework.
    /// </summary>
    /// <param name="cipherSuite">The IANA cipher suite identifier.</param>
    /// <returns>The identifier.</returns>
    internal static int ToConstructorCipherSuite(int cipherSuite) => cipherSuite;
#endif

    /// <summary>
    /// Creates a server error used to build exceptions through the internal factories.
    /// </summary>
    /// <returns>The error.</returns>
    internal static SqlError CreateError() =>
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

    /// <summary>
    /// Creates an error collection holding a single error from <see cref="CreateError"/>.
    /// </summary>
    /// <returns>The error collection.</returns>
    internal static SqlErrorCollection CreateErrorCollection() => new() { CreateError() };

    /// <summary>
    /// Asserts that a snapshot is non-null, reports itself as empty, and has every field
    /// unavailable.
    /// </summary>
    /// <param name="info">The snapshot to check.</param>
    internal static void AssertEmpty([NotNull] SqlConnectionEncryptionInfo? info)
    {
        Assert.NotNull(info);
        Assert.True(info.IsEmpty);
        Assert.Null(info.NegotiatedEncryptionMode);
        Assert.Null(info.NegotiatedEncryptionProtocol);
        Assert.Null(info.NegotiatedCipherSuiteId);
#if NET
        Assert.Null(info.NegotiatedCipherSuite);
#endif
        Assert.Null(info.PresentedServerCertificateRawData);
        Assert.Null(info.GetServerCertificate());
        Assert.Null(info.ValidationErrors);
        Assert.Null(info.ChainStatus);
    }
}
