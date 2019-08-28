// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Microsoft.Data.SqlClient {

    /// <summary>
    /// AD Authentication parameters passed by driver to auth providers.
    /// </summary>
    public class SqlAuthenticationParameters {
        /// <summary>
        /// Authentication method.
        /// </summary>
        public SqlAuthenticationMethod AuthenticationMethod { get; }

        /// <summary>
        /// Resource URI.
        /// </summary>
        public string[] Scopes { get; }

        /// <summary>
        /// Authority URI.
        /// </summary>
        public string Authority { get; }

        /// <summary>
        /// User login name/id.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// User password.
        /// </summary>
        public string Password { get; }

        /// <summary>
        /// Connection Id.
        /// </summary>
        public Guid ConnectionId { get; }

        /// <summary>
        /// Server name.
        /// </summary>
        public string ServerName { get; }

        /// <summary>
        /// Database name.
        /// </summary>
        public string DatabaseName { get; }

        protected SqlAuthenticationParameters(
            SqlAuthenticationMethod authenticationMethod,
            string serverName,
            string databaseName,
            string[] scopes,
            string authority,
            string userId,
            string password,
            Guid connectionId) {
            AuthenticationMethod = authenticationMethod;
            ServerName = serverName;
            DatabaseName = databaseName;
            Scopes = scopes;
            Authority = authority;
            UserId = userId;
            Password = password;
            ConnectionId = connectionId;
        }

        /// <summary>
        /// AD authentication parameter builder.
        /// </summary>
        internal class Builder {
            private readonly SqlAuthenticationMethod _authenticationMethod;
            private readonly string _serverName;
            private readonly string _databaseName;
            private readonly string[] _scopes;
            private readonly string _authority;
            private string _userId;
            private string _password;
            private Guid _connectionId = Guid.NewGuid();

            /// <summary>
            /// Implicitly converts to <see cref="SqlAuthenticationParameters"/>.
            /// </summary>
            public static implicit operator SqlAuthenticationParameters(Builder builder)
            {
                return new SqlAuthenticationParameters(
                    authenticationMethod: builder._authenticationMethod,
                    serverName: builder._serverName,
                    databaseName: builder._databaseName,
                    scopes: builder._scopes,
                    authority: builder._authority,
                    userId: builder._userId,
                    password: builder._password,
                    connectionId: builder._connectionId);
            }

            /// <summary>
            /// Set user id.
            /// </summary>
            public Builder WithUserId(string userId) {
                _userId = userId;
                return this;
            }

            /// <summary>
            /// Set password.
            /// </summary>
            public Builder WithPassword(string password) {
                _password = password;
                return this;
            }

            /// <summary>
            /// Set password.
            /// </summary>
            public Builder WithPassword(SecureString password) {
                IntPtr valuePtr = IntPtr.Zero;
                try {
                    valuePtr = Marshal.SecureStringToGlobalAllocUnicode(password);
                    _password = Marshal.PtrToStringUni(valuePtr);
                } finally {
                    Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
                }
                return this;
            }

            /// <summary>
            /// Set a specific connection id instead of using a random one.
            /// </summary>
            public Builder WithConnectionId(Guid connectionId) {
                _connectionId = connectionId;
                return this;
            }

            internal Builder(SqlAuthenticationMethod authenticationMethod, string resource, string authority, string serverName, string databaseName) {
                _authenticationMethod = authenticationMethod;
                _serverName = serverName;
                _databaseName = databaseName;
                _scopes = new string[] { resource + "/.default" };
                _authority = authority;
            }
        }
    }
}
