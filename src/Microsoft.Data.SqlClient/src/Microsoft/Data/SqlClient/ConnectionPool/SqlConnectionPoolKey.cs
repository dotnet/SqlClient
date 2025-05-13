// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    // SqlConnectionPoolKey: Implementation of a key to connection pool groups for specifically to be used for SqlConnection
    //  Connection string and SqlCredential are used as a key
    internal class SqlConnectionPoolKey : DbConnectionPoolKey
    {
        private int _hashValue;
        private readonly SqlCredential _credential;
        private readonly string _accessToken;
        private Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> _accessTokenCallback;
        private SspiContextProvider _sspiContextProvider;

        internal SqlCredential Credential => _credential;
        internal string AccessToken => _accessToken;
        internal Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> AccessTokenCallback => _accessTokenCallback;
        internal SspiContextProvider SspiContextProvider => _sspiContextProvider;

        internal override string ConnectionString
        {
            get => base.ConnectionString;
            set
            {
                base.ConnectionString = value;
                CalculateHashCode();
            }
        }

        internal SqlConnectionPoolKey(
            string connectionString,
            SqlCredential credential,
            string accessToken,
            Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> accessTokenCallback,
            SspiContextProvider sspiContextProvider) : base(connectionString)
        {
            Debug.Assert(credential == null || accessToken == null || accessTokenCallback == null, "Credential, AccessToken, and Callback can't have a value at the same time.");
            _credential = credential;
            _accessToken = accessToken;
            _accessTokenCallback = accessTokenCallback;
            _sspiContextProvider = sspiContextProvider;
            CalculateHashCode();
        }

        private SqlConnectionPoolKey(SqlConnectionPoolKey key) : base(key)
        {
            _credential = key.Credential;
            _accessToken = key.AccessToken;
            _accessTokenCallback = key._accessTokenCallback;
            _sspiContextProvider = key._sspiContextProvider;

            CalculateHashCode();
        }

        public override object Clone()
        {
            return new SqlConnectionPoolKey(this);
        }

        public override bool Equals(object obj)
        {
            return (obj is SqlConnectionPoolKey key
                && _credential == key._credential
                && ConnectionString == key.ConnectionString
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
            _hashValue = base.GetHashCode();

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
