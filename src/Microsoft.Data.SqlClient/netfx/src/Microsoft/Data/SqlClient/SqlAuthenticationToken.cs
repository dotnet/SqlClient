// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// AD authentication token.
    /// </summary>
    public class SqlAuthenticationToken {
        /// <summary>
        /// Token expiration time.
        /// </summary>
        public DateTimeOffset ExpiresOn { get; }

        /// <summary>
        /// Token string.
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public SqlAuthenticationToken(string accessToken, DateTimeOffset expiresOn) {
            if (string.IsNullOrEmpty(accessToken)) throw SQL.ParameterCannotBeEmpty("AccessToken");

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
        internal SqlFedAuthToken ToSqlFedAuthToken() {
            var tokenBytes = AccessTokenBytesFromString(AccessToken);
            return new SqlFedAuthToken {
                accessToken = tokenBytes,
                dataLen = (uint)tokenBytes.Length,
                expirationFileTime = ExpiresOn.ToFileTime()
            };
        }

        /// <summary>
        /// Convert token bytes to string.
        /// </summary>
        internal static string AccessTokenStringFromBytes(byte[] bytes) {
            return Encoding.Unicode.GetString(bytes);
        }

        /// <summary>
        /// Convert token string to bytes.
        /// </summary>
        internal static byte[] AccessTokenBytesFromString(string token) {
            return Encoding.Unicode.GetBytes(token);
        }
    }
}
