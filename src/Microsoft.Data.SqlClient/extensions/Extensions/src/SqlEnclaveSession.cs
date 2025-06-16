// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions;

/// <include file='../doc/SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/SqlEnclaveSession/*' />
internal class SqlEnclaveSession
{

    private static readonly string _sessionKeyName = "SessionKey";
    private static readonly string _className = "EnclaveSession";

    private readonly byte[] _sessionKey;

    /// <include file='../doc/SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/SessionId/*' />
    internal long SessionId { get; }

    /// <include file='../doc/SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/GetSessionKey/*' />
    internal byte[] GetSessionKey()
    {
        return (byte[])_sessionKey.Clone();
    }

    /// <include file='../doc/SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/ctor/*' />
    internal SqlEnclaveSession(byte[] sessionKey, long sessionId)
    {
        _sessionKey = sessionKey;
        SessionId = sessionId;
    }
}
