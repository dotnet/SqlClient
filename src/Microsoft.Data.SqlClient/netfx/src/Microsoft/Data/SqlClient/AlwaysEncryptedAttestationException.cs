// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\AlwaysEncryptedAttestationException.xml' path='docs/members[@name="AlwaysEncryptedAttestationException"]/AlwaysEncryptedAttestationException/*' />
    public class AlwaysEncryptedAttestationException : Exception
    {
        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\AlwaysEncryptedAttestationException.xml' path='docs/members[@name="AlwaysEncryptedAttestationException"]/ctor[@name="messageAndInnerException"]/*' />
        public AlwaysEncryptedAttestationException(string message, Exception innerException) : base(message, innerException) { }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\AlwaysEncryptedAttestationException.xml' path='docs/members[@name="AlwaysEncryptedAttestationException"]/ctor[@name="messageString"]/*' />
        public AlwaysEncryptedAttestationException(string message) : base(message) { }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\AlwaysEncryptedAttestationException.xml' path='docs/members[@name="AlwaysEncryptedAttestationException"]/ctor[@name="default"]/*' />
        public AlwaysEncryptedAttestationException() : base() { }
    }
}
