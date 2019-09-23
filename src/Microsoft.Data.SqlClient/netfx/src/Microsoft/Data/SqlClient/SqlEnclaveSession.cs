// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{

    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/SqlEnclaveSession/*' />
    public class SqlEnclaveSession
    {

        private static readonly string _sessionKeyName = "SessionKey";
        private static readonly string _className = "EnclaveSession";

        private readonly byte[] _sessionKey;

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/SessionId/*' />
        public long SessionId { get; }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/GetSessionKey/*' />
        public byte[] GetSessionKey()
        {
            return Clone(_sessionKey);
        }

        /// <summary>
        /// Deep copy the array into a new array
        /// </summary>
        /// <param name="arrayToClone"></param>
        /// <returns></returns>
        private byte[] Clone(byte[] arrayToClone)
        {

            byte[] returnValue = new byte[arrayToClone.Length];

            for (int i = 0; i < arrayToClone.Length; i++)
            {
                returnValue[i] = arrayToClone[i];
            }

            return returnValue;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/ctor/*' />
        public SqlEnclaveSession(byte[] sessionKey, long sessionId/*, long counter*/)
        {
            if (null == sessionKey)
            { throw SQL.NullArgumentInConstructorInternal(_sessionKeyName, _className); }
            if (0 == sessionKey.Length)
            { throw SQL.EmptyArgumentInConstructorInternal(_sessionKeyName, _className); }

            _sessionKey = sessionKey;
            SessionId = sessionId;
        }
    }
}
