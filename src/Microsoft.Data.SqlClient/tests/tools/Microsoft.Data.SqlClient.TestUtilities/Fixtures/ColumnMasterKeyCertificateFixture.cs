// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Data.SqlClient.TestUtilities.Fixtures
{
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
}
