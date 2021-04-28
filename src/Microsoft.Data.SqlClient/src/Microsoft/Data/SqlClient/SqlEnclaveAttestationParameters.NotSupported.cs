// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/SqlEnclaveAttestationParameters/*' />
    internal partial class SqlEnclaveAttestationParameters
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/Protocol/*' />
        internal int Protocol { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/GetInput/*' />
        internal byte[] GetInput()
        {
            return null;
        }
    }
}
