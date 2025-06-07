// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures;

public sealed class ColumnEncryptionCertificateFixture : CertificateFixtureBase
{
    public X509Certificate2 PrimaryColumnEncryptionCertificate { get; }

    public X509Certificate2 SecondaryColumnEncryptionCertificate { get; }

    public X509Certificate2 CertificateWithoutPrivateKey { get; }

    private readonly X509Certificate2 _currentUserCertificate;
    private readonly X509Certificate2 _localMachineCertificate;

    public ColumnEncryptionCertificateFixture()
    {
        PrimaryColumnEncryptionCertificate = CreateCertificate(nameof(PrimaryColumnEncryptionCertificate), Array.Empty<string>(), Array.Empty<string>());
        SecondaryColumnEncryptionCertificate = CreateCertificate(nameof(SecondaryColumnEncryptionCertificate), Array.Empty<string>(), Array.Empty<string>());
        _currentUserCertificate = CreateCertificate(nameof(_currentUserCertificate), Array.Empty<string>(), Array.Empty<string>());
        using (X509Certificate2 createdCertificate = CreateCertificate(nameof(CertificateWithoutPrivateKey), Array.Empty<string>(), Array.Empty<string>()))
        {
            // This will strip the private key away from the created certificate
#if NET9_0_OR_GREATER
                CertificateWithoutPrivateKey = X509CertificateLoader.LoadCertificate(createdCertificate.Export(X509ContentType.Cert));
#else
            CertificateWithoutPrivateKey = new X509Certificate2(createdCertificate.Export(X509ContentType.Cert));
#endif
            AddToStore(CertificateWithoutPrivateKey, StoreLocation.CurrentUser, StoreName.My);
        }

        AddToStore(PrimaryColumnEncryptionCertificate, StoreLocation.CurrentUser, StoreName.My);
        AddToStore(SecondaryColumnEncryptionCertificate, StoreLocation.CurrentUser, StoreName.My);
        AddToStore(_currentUserCertificate, StoreLocation.CurrentUser, StoreName.My);

        if (IsAdmin)
        {
            _localMachineCertificate = CreateCertificate(nameof(_localMachineCertificate), Array.Empty<string>(), Array.Empty<string>());

            AddToStore(_localMachineCertificate, StoreLocation.LocalMachine, StoreName.My);
        }
    }

    public X509Certificate2 GetCertificate(StoreLocation storeLocation)
    {
        return storeLocation == StoreLocation.CurrentUser
            ? _currentUserCertificate
            : storeLocation == StoreLocation.LocalMachine && IsAdmin
                ? _localMachineCertificate
                : throw new InvalidOperationException("Attempted to retrieve the certificate added to the local machine store; this requires administrator rights.");
    }

    public static bool IsAdmin
        => Environment.OSVersion.Platform == PlatformID.Win32NT
            && new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
}
