// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>
/// Creates TLS certificates for simulated servers without touching a machine or user trust store.
/// </summary>
internal static class TestServerCertificate
{
    /// <summary>
    /// Creates a short-lived self-signed certificate for <c>CN=localhost</c> with its private key.
    /// </summary>
    /// <returns>The certificate, which the caller disposes.</returns>
    public static X509Certificate2 Create()
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Schannel requires an imported key rather than the ephemeral key returned above.
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
    }
}

#endif
