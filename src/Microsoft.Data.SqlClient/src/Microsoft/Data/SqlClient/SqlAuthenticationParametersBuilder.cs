// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlAuthenticationParametersBuilder
    {
        private readonly SqlAuthenticationMethod _authenticationMethod;
        private readonly string _serverName;
        private readonly string _databaseName;
        private readonly string _resource;
        private readonly string _authority;
        private string _userId;
        private string _password;
        private Guid _connectionId = Guid.NewGuid();
        private int _authenticationTimeout = ADP.DefaultConnectionTimeout;

        /// <summary>
        /// Implicitly converts to <see cref="SqlAuthenticationParameters"/>.
        /// </summary>
        public static implicit operator SqlAuthenticationParameters(SqlAuthenticationParametersBuilder builder)
        {
            return new SqlAuthenticationParameters(
                authenticationMethod: builder._authenticationMethod,
                serverName: builder._serverName,
                databaseName: builder._databaseName,
                resource: builder._resource,
                authority: builder._authority,
                userId: builder._userId,
                password: builder._password,
                connectionId: builder._connectionId,
                authenticationTimeout: builder._authenticationTimeout);
        }

        /// <summary>
        /// Set user id.
        /// </summary>
        public SqlAuthenticationParametersBuilder WithUserId(string userId)
        {
            _userId = userId;
            return this;
        }

        /// <summary>
        /// Set password.
        /// </summary>
        public SqlAuthenticationParametersBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        /// <summary>
        /// Set password.
        /// </summary>
        public SqlAuthenticationParametersBuilder WithPassword(SecureString password)
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
        public SqlAuthenticationParametersBuilder WithConnectionId(Guid connectionId)
        {
            _connectionId = connectionId;
            return this;
        }

        /// <summary>
        /// Set authentication timeout.
        /// </summary>
        public SqlAuthenticationParametersBuilder WithAuthenticationTimeout(int timeout)
        {
            _authenticationTimeout = timeout;
            return this;
        }

        internal SqlAuthenticationParametersBuilder(SqlAuthenticationMethod authenticationMethod, string resource, string authority, string serverName, string databaseName)
        {
            _authenticationMethod = authenticationMethod;
            _serverName = serverName;
            _databaseName = databaseName;
            _resource = resource;
            _authority = authority;
        }
    }
}
