// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient
{

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/SqlColumnEncryptionEnclaveProvider/*'/>
    public abstract class SqlColumnEncryptionEnclaveProvider
    {

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetEnclaveSession/*'/>
        public abstract void GetEnclaveSession(string serverName, string attestationUrl, out SqlEnclaveSession sqlEnclaveSession, out long counter);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetAttestationParameters/*'/>
        public abstract SqlEnclaveAttestationParameters GetAttestationParameters();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/CreateEnclaveSession/*'/>
        public abstract void CreateEnclaveSession(byte[] enclaveAttestationInfo, ECDiffieHellmanCng clientDiffieHellmanKey, string attestationUrl, string servername, out SqlEnclaveSession sqlEnclaveSession, out long counter);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/InvalidateEnclaveSession/*'/>
        public abstract void InvalidateEnclaveSession(string serverName, string enclaveAttestationUrl, SqlEnclaveSession enclaveSession);
    }
}
