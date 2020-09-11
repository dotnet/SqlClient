// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/SqlAuthenticationToken/*'/>
    public class SqlAuthenticationToken
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ExpiresOn/*'/>
        public DateTimeOffset ExpiresOn { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/AccessToken/*'/>
        public string AccessToken { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ctor/*'/>
        public SqlAuthenticationToken(string accessToken, DateTimeOffset expiresOn)
        {
            if (string.IsNullOrEmpty(accessToken))
                throw SQL.ParameterCannotBeEmpty("AccessToken");

            AccessToken = accessToken;
            ExpiresOn = expiresOn;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        internal SqlAuthenticationToken(byte[] accessToken, DateTimeOffset expiresOn)
            : this(AccessTokenStringFromBytes(accessToken), expiresOn) { }

        /// <summary>
        /// Convert to driver's internal token class.
        /// </summary>
        internal SqlFedAuthToken ToSqlFedAuthToken()
        {
            var tokenBytes = AccessTokenBytesFromString(AccessToken);
            return new SqlFedAuthToken
            {
                accessToken = tokenBytes,
                dataLen = (uint)tokenBytes.Length,
                expirationFileTime = ExpiresOn.ToFileTime()
            };
        }

        /// <summary>
        /// Convert token bytes to string.
        /// </summary>
        internal static string AccessTokenStringFromBytes(byte[] bytes)
        {
            return Encoding.Unicode.GetString(bytes);
        }

        /// <summary>
        /// Convert token string to bytes.
        /// </summary>
        internal static byte[] AccessTokenBytesFromString(string token)
        {
            return Encoding.Unicode.GetBytes(token);
        }
    }
}
