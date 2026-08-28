// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/SqlConnectionAttestationProtocol/*' />
public enum SqlConnectionAttestationProtocol
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/NotSpecified/*' />
    NotSpecified = 0,

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/AAS/*' />
    AAS = 1,

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/None/*' />
    None = 2,

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/HGS/*' />
    HGS = 3
}
