// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Microsoft.Data.SqlClient
{

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/SqlAuthenticationParameters/*'/>
    public class SqlAuthenticationParameters
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/AuthenticationMethod/*'/>
        public SqlAuthenticationMethod AuthenticationMethod { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/Resource/*'/>
        public string Resource { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/Authority/*'/>
        public string Authority { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/UserId/*'/>
        public string UserId { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/Password/*'/>
        public string Password { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ConnectionId/*'/>
        public Guid ConnectionId { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ServerName/*'/>
        public string ServerName { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/DatabaseName/*'/>
        public string DatabaseName { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ctor/*'/>
        protected SqlAuthenticationParameters(
            SqlAuthenticationMethod authenticationMethod,
            string serverName,
            string databaseName,
            string resource,
            string authority,
            string userId,
            string password,
            Guid connectionId)
        {
            AuthenticationMethod = authenticationMethod;
            ServerName = serverName;
            DatabaseName = databaseName;
            Resource = resource;
            Authority = authority;
            UserId = userId;
            Password = password;
            ConnectionId = connectionId;
        }

        /// <summary>
        /// AD authentication parameter builder.
        /// </summary>
        internal class Builder
        {
            private readonly SqlAuthenticationMethod _authenticationMethod;
            private readonly string _serverName;
            private readonly string _databaseName;
            private readonly string _resource;
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
                    resource: builder._resource,
                    authority: builder._authority,
                    userId: builder._userId,
                    password: builder._password,
                    connectionId: builder._connectionId);
            }

            /// <summary>
            /// Set user id.
            /// </summary>
            public Builder WithUserId(string userId)
            {
                _userId = userId;
                return this;
            }

            /// <summary>
            /// Set password.
            /// </summary>
            public Builder WithPassword(string password)
            {
                _password = password;
                return this;
            }

            /// <summary>
            /// Set password.
            /// </summary>
            public Builder WithPassword(SecureString password)
            {
                IntPtr valuePtr = IntPtr.Zero;
                try
                {
                    valuePtr = Marshal.SecureStringToGlobalAllocUnicode(password);
                    _password = Marshal.PtrToStringUni(valuePtr);
                }
                finally
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
                }
                return this;
            }

            /// <summary>
            /// Set a specific connection id instead of using a random one.
            /// </summary>
            public Builder WithConnectionId(Guid connectionId)
            {
                _connectionId = connectionId;
                return this;
            }

            internal Builder(SqlAuthenticationMethod authenticationMethod, string resource, string authority, string serverName, string databaseName)
            {
                _authenticationMethod = authenticationMethod;
                _serverName = serverName;
                _databaseName = databaseName;
                _resource = resource;
                _authority = authority;
            }
        }
    }
}
