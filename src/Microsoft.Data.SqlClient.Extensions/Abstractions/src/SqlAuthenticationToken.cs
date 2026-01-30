// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/SqlAuthenticationToken/*'/>
public sealed class SqlAuthenticationToken
{
    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ctor/*'/>
    public SqlAuthenticationToken(
        string accessToken,
        DateTimeOffset expiresOn)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new TokenException("AccessToken must not be null or empty.");
        }

        AccessToken = accessToken;
        ExpiresOn = expiresOn;
    }

    /// <summary>
    /// The exception thrown by the SqlAuthenticationToken constructor.
    /// </summary>
    internal sealed class TokenException : SqlAuthenticationProviderException
    {
        /// <summary>
        /// Construct with the exception message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        internal TokenException(string message)
        : base(message)
        {
        }
    }

    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/AccessToken/*'/>
    public string AccessToken { get; }

    /// <include file='../doc/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ExpiresOn/*'/>
    public DateTimeOffset ExpiresOn { get; }
}
