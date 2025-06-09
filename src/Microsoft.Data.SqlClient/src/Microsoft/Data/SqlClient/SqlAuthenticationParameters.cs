// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/SqlAuthenticationParameters/*'/>
    public class SqlAuthenticationParameters : SqlAuthenticationParametersBase
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ctor/*'/>
        protected SqlAuthenticationParameters(
            SqlAuthenticationMethod authenticationMethod,
            string serverName,
            string databaseName,
            string resource,
            string authority,
            string userId,
            string password,
            Guid connectionId,
            int connectionTimeout)
            : base(
                authenticationMethod,
                serverName,
                databaseName,
                resource,
                authority,
                userId,
                password,
                connectionId,
                connectionTimeout)
        {
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
            private int _connectionTimeout = ADP.DefaultConnectionTimeout;

            /// <summary>
            /// Build and return a <see cref="SqlAuthenticationParameters"/> instance.
            /// </summary>
            public SqlAuthenticationParameters Build()
            {
                return new SqlAuthenticationParameters(
                    authenticationMethod: _authenticationMethod,
                    serverName: _serverName,
                    databaseName: _databaseName,
                    resource: _resource,
                    authority: _authority,
                    userId: _userId,
                    password: _password,
                    connectionId: _connectionId,
                    connectionTimeout: _connectionTimeout);
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

            /// <summary>
            /// Set connection timeout.
            /// </summary>
            public Builder WithConnectionTimeout(int timeout)
            {
                _connectionTimeout = timeout;
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
