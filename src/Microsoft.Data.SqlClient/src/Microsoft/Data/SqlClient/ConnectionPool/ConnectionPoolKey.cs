// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    // ConnectionPoolKey: Key to connection pool groups for SqlConnection
    //  Connection string, SqlCredential, access token, access token callback, and SSPI context provider are used as a key
    internal class ConnectionPoolKey : ICloneable
    {
        private string _connectionString;
        private int _hashValue;
        private readonly SqlCredential _credential;
        private readonly string _accessToken;
        private Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> _accessTokenCallback;
        private SspiContextProvider _sspiContextProvider;

        internal SqlCredential Credential => _credential;
        internal string AccessToken => _accessToken;
        internal Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> AccessTokenCallback => _accessTokenCallback;
        internal SspiContextProvider SspiContextProvider => _sspiContextProvider;

        internal string ConnectionString
        {
            get => _connectionString;
            set
            {
                _connectionString = value;
                CalculateHashCode();
            }
        }

        internal ConnectionPoolKey(
            string connectionString,
            SqlCredential credential,
            string accessToken,
            Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> accessTokenCallback,
            SspiContextProvider sspiContextProvider)
        {
            Debug.Assert(credential == null || accessToken == null || accessTokenCallback == null, "Credential, AccessToken, and Callback can't have a value at the same time.");
            _connectionString = connectionString;
            _credential = credential;
            _accessToken = accessToken;
            _accessTokenCallback = accessTokenCallback;
            _sspiContextProvider = sspiContextProvider;
            CalculateHashCode();
        }

        private ConnectionPoolKey(ConnectionPoolKey key)
        {
            _connectionString = key._connectionString;
            _credential = key._credential;
            _accessToken = key._accessToken;
            _accessTokenCallback = key._accessTokenCallback;
            _sspiContextProvider = key._sspiContextProvider;
            CalculateHashCode();
        }

        public object Clone()
        {
            return new ConnectionPoolKey(this);
        }

        public override bool Equals(object obj)
        {
            return (obj is ConnectionPoolKey key
                && _credential == key._credential
                && _connectionString == key._connectionString
                && _accessTokenCallback == key._accessTokenCallback
                && string.CompareOrdinal(_accessToken, key._accessToken) == 0
                && _sspiContextProvider == key._sspiContextProvider);
        }

        public override int GetHashCode()
        {
            return _hashValue;
        }

        private void CalculateHashCode()
        {
            _hashValue = _connectionString == null ? 0 : _connectionString.GetHashCode();

            if (_credential != null)
            {
                unchecked
                {
                    _hashValue = _hashValue * 17 + _credential.GetHashCode();
                }
            }
            else if (_accessToken != null)
            {
                unchecked
                {
                    _hashValue = _hashValue * 17 + _accessToken.GetHashCode();
                }
            }
            else if (_accessTokenCallback != null)
            {
                unchecked
                {
                    _hashValue = _hashValue * 17 + _accessTokenCallback.GetHashCode();
                }
            }

            if (_sspiContextProvider != null)
            {
                _hashValue = _hashValue * 17 + _sspiContextProvider.GetHashCode();
            }
        }
    }
}
