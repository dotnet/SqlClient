// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures;

/// <summary>
/// Provides a test fixture for managing a column master key certificate.
/// </summary>
/// <remarks>
/// This class is intended to simplify the setup and teardown of a column master key certificate for
/// testing purposes. It creates and optionally adds the certificate to the specified certificate store.
/// </remarks>
public class ColumnMasterKeyCertificateFixture : CertificateFixtureBase
{
    public ColumnMasterKeyCertificateFixture()
        : this(true)
    {
    }

    public X509Certificate2 ColumnMasterKeyCertificate { get; }

    protected ColumnMasterKeyCertificateFixture(bool createCertificate)
    {
        if (createCertificate)
        {
            ColumnMasterKeyCertificate = CreateCertificate(nameof(ColumnMasterKeyCertificate), Array.Empty<string>(), Array.Empty<string>());

            AddToStore(ColumnMasterKeyCertificate, StoreLocation.CurrentUser, StoreName.My);
        }
    }
}
