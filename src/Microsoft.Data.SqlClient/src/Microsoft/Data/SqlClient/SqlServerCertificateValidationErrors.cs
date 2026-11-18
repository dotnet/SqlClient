// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlServerCertificateValidationErrors.xml' path='docs/members[@name="SqlServerCertificateValidationErrors"]/SqlServerCertificateValidationErrors/*' />
    [Flags]
    public enum SqlServerCertificateValidationErrors
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlServerCertificateValidationErrors.xml' path='docs/members[@name="SqlServerCertificateValidationErrors"]/None/*' />
        None = 0,

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlServerCertificateValidationErrors.xml' path='docs/members[@name="SqlServerCertificateValidationErrors"]/ServerCertificateMissing/*' />
        ServerCertificateMissing = 1,

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlServerCertificateValidationErrors.xml' path='docs/members[@name="SqlServerCertificateValidationErrors"]/HostNameMismatch/*' />
        HostNameMismatch = 2,

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlServerCertificateValidationErrors.xml' path='docs/members[@name="SqlServerCertificateValidationErrors"]/ChainError/*' />
        ChainError = 4,

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlServerCertificateValidationErrors.xml' path='docs/members[@name="SqlServerCertificateValidationErrors"]/PinnedCertificateMismatch/*' />
        PinnedCertificateMismatch = 8,
    }
}
