// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures;

/// <summary>
/// Provides a fixture for working with certificates backed by a Cryptographic Service Provider (CSP).
/// </summary>
/// <remarks>
/// This class creates and manages a certificate stored in the current user's certificate store,  along
/// with its associated CSP key path. It is intended to facilitate testing or scenarios  requiring temporary
/// certificates.
/// </remarks>
public class CspCertificateFixture : CertificateFixtureBase
{
    public CspCertificateFixture()
    {
        CspCertificate = CreateCertificate(nameof(CspCertificate), Array.Empty<string>(), Array.Empty<string>(), true);

        AddToStore(CspCertificate, StoreLocation.CurrentUser, StoreName.My);

        CspCertificatePath = $"{StoreLocation.CurrentUser}/{StoreName.My}/{CspCertificate.Thumbprint}";
        CspKeyPath = GetCspPathFromCertificate();
    }

    public X509Certificate2 CspCertificate { get; }

    public string CspCertificatePath { get; }

    public string CspKeyPath { get; }

    private string GetCspPathFromCertificate()
    {
        RSA privateKey = CspCertificate.GetRSAPrivateKey();

        if (privateKey is RSACryptoServiceProvider csp)
        {
            return string.Concat(csp.CspKeyContainerInfo.ProviderName, @"/", csp.CspKeyContainerInfo.KeyContainerName);
        }
        else if (privateKey is RSACng cng)
        {
            return string.Concat(cng.Key.Provider.Provider, @"/", cng.Key.KeyName);
        }
        else
        {
            return null;
        }
    }
}
