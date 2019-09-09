// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// Represents an AD authentication token.
    /// </summary>
    public class SqlAuthenticationToken
    {

        /// <summary>
        /// Gets the token expiration time.
        /// </summary>
        /// <value>The token expiration time.</value>
        /// <remarks>Tobe added.</remarks>
        public DateTimeOffset ExpiresOn { get; }

        /// <summary>
        /// Gets the token string.
        /// </summary>
        /// <remarks>To be added.</remarks>
        public string AccessToken { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Data.SqlClient.SqlAuthenticationToken" /> class.
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="expiresOn"></param>
        /// <remarks>To be added.</remarks>
        /// <exception cref="System.ArgumentNullException"></exception>The <paramref name="accessToken" /> parameter is <see langword="null" /> or empty.
        public SqlAuthenticationToken(string accessToken, DateTimeOffset expiresOn)
        {
            if (string.IsNullOrEmpty(accessToken)) throw SQL.ParameterCannotBeEmpty("AccessToken");

            AccessToken = accessToken;
            ExpiresOn = expiresOn;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="expiresOn"></param>
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
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        internal static string AccessTokenStringFromBytes(byte[] bytes)
        {
            return Encoding.Unicode.GetString(bytes);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        internal static byte[] AccessTokenBytesFromString(string token)
        {
            return Encoding.Unicode.GetBytes(token);
        }
    }
}
