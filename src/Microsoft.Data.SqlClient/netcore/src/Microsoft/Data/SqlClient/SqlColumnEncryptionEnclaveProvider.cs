﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/SqlColumnEncryptionEnclaveProvider/*'/>
    internal abstract partial class SqlColumnEncryptionEnclaveProvider
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetEnclaveSession/*'/>
        internal abstract void GetEnclaveSession(string serverName, string attestationUrl, string database, bool generateCustomData, out SqlEnclaveSession sqlEnclaveSession, out long counter, out byte[] customData, out int customDataLength);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetAttestationParameters/*'/>
        internal abstract SqlEnclaveAttestationParameters GetAttestationParameters(string attestationUrl, byte[] customData, int customDataLength);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/InvalidateEnclaveSession/*'/>
        internal abstract void InvalidateEnclaveSession(string serverName, string enclaveAttestationUrl, string database, SqlEnclaveSession enclaveSession);
    }
}
