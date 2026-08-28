// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Text;

namespace Microsoft.Data.SqlClient.Parser;

internal sealed class SqlFedAuthToken
{
    internal SqlFedAuthToken(byte[] accessToken, long expirationFileTime)
    {
        AccessToken = accessToken;
        DataLen = (uint)AccessToken.Length;
        ExpirationFileTime = expirationFileTime;
    }

    /// <summary>
    /// Convert from a SqlAuthenticationToken.
    /// </summary>
    internal SqlFedAuthToken(SqlAuthenticationToken token)
    {
        AccessToken = Encoding.Unicode.GetBytes(token.AccessToken);
        DataLen = (uint)AccessToken.Length;
        ExpirationFileTime = token.ExpiresOn.ToFileTime();
    }

    internal byte[] AccessToken { get; }

    internal uint DataLen { get; }

    internal long ExpirationFileTime { get; }
}
