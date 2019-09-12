// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// AD auth retry states.
    /// </summary>
    internal enum ActiveDirectoryAuthenticationTimeoutRetryState
    {
        NotStarted = 0,
        Retrying,
        HasLoggedIn,
    }

    /// <summary>
    /// AD auth retry helper.
    /// </summary>
    internal class ActiveDirectoryAuthenticationTimeoutRetryHelper
    {
        private ActiveDirectoryAuthenticationTimeoutRetryState _state = ActiveDirectoryAuthenticationTimeoutRetryState.NotStarted;
        private SqlFedAuthToken _token;
        private readonly string _typeName;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ActiveDirectoryAuthenticationTimeoutRetryHelper()
        {
            _typeName = GetType().Name;
        }

        /// <summary>
        /// Retry state.
        /// </summary>
        public ActiveDirectoryAuthenticationTimeoutRetryState State
        {
            get { return _state; }
            set
            {
                switch (_state)
                {
                    case ActiveDirectoryAuthenticationTimeoutRetryState.NotStarted:
                        if (value != ActiveDirectoryAuthenticationTimeoutRetryState.Retrying
                            && value != ActiveDirectoryAuthenticationTimeoutRetryState.HasLoggedIn)
                        {
                            throw new InvalidOperationException($"Cannot transit from {_state} to {value}.");
                        }
                        break;
                    case ActiveDirectoryAuthenticationTimeoutRetryState.Retrying:
                        if (value != ActiveDirectoryAuthenticationTimeoutRetryState.HasLoggedIn)
                        {
                            throw new InvalidOperationException($"Cannot transit from {_state} to {value}.");
                        }
                        break;
                    case ActiveDirectoryAuthenticationTimeoutRetryState.HasLoggedIn:
                        throw new InvalidOperationException($"Cannot transit from {_state} to {value}.");
                    default:
                        throw new InvalidOperationException($"Unsupported state: {value}.");
                }
                _state = value;
            }
        }

        /// <summary>
        /// Cached token.
        /// </summary>
        public SqlFedAuthToken CachedToken
        {
            get
            {
                return _token;
            }
            set
            {
                _token = value;
            }
        }

        /// <summary>
        /// Whether login can be retried after a client/server connection timeout due to a long-time token acquisition.
        /// </summary>
        public bool CanRetryWithSqlException(SqlException sqlex)
        {
            if (_state == ActiveDirectoryAuthenticationTimeoutRetryState.NotStarted
                && CachedToken != null
                && IsConnectTimeoutError(sqlex))
            {
                return true;
            }
            return false;
        }

        private static bool IsConnectTimeoutError(SqlException sqlex)
        {
            var innerException = sqlex.InnerException as Win32Exception;
            if (innerException == null)
                return false;
            return innerException.NativeErrorCode == 10054 // Server timeout
                   || innerException.NativeErrorCode == 258; // Client timeout
        }

        private static string GetTokenHash(SqlFedAuthToken token)
        {
            if (token == null)
                return "null";

            // Here we mimic how ADAL calculates hash for token. They use UTF8 instead of Unicode.
            var originalTokenString = SqlAuthenticationToken.AccessTokenStringFromBytes(token.accessToken);
            var bytesInUtf8 = Encoding.UTF8.GetBytes(originalTokenString);
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytesInUtf8);
                return Convert.ToBase64String(hash);
            }
        }
    }
}
