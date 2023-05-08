// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    // SqlConnectionPoolKey: Implementation of a key to connection pool groups for specifically to be used for SqlConnection
    //  Connection string and SqlCredential are used as a key
    internal class SqlConnectionPoolKey : DbConnectionPoolKey
    {
        private int _hashValue;
        private readonly SqlCredential _credential;
        private readonly string _accessToken;

        internal SqlCredential Credential => _credential;
        internal string AccessToken => _accessToken;

        internal override string ConnectionString
        {
            get => base.ConnectionString;
            set
            {
                base.ConnectionString = value;
                CalculateHashCode();
            }
        }

#if NETFRAMEWORK
        #region NET Framework
        private readonly ServerCertificateValidationCallback _serverCertificateValidationCallback;
        private readonly ClientCertificateRetrievalCallback _clientCertificateRetrievalCallback;
        private readonly SqlClientOriginalNetworkAddressInfo _originalNetworkAddressInfo;

        internal ServerCertificateValidationCallback ServerCertificateValidationCallback
            => _serverCertificateValidationCallback;

        internal ClientCertificateRetrievalCallback ClientCertificateRetrievalCallback
            => _clientCertificateRetrievalCallback;

        internal SqlClientOriginalNetworkAddressInfo OriginalNetworkAddressInfo
            => _originalNetworkAddressInfo;

        internal SqlConnectionPoolKey(string connectionString,
                            SqlCredential credential,
                            string accessToken,
                            ServerCertificateValidationCallback serverCertificateValidationCallback,
                            ClientCertificateRetrievalCallback clientCertificateRetrievalCallback,
                            SqlClientOriginalNetworkAddressInfo originalNetworkAddressInfo) : base(connectionString)
        {
            Debug.Assert(_credential == null || _accessToken == null, "Credential and AccessToken can't have the value at the same time.");
            _credential = credential;
            _accessToken = accessToken;
            _serverCertificateValidationCallback = serverCertificateValidationCallback;
            _clientCertificateRetrievalCallback = clientCertificateRetrievalCallback;
            _originalNetworkAddressInfo = originalNetworkAddressInfo;
            CalculateHashCode();
        }
        #endregion
#else
        #region NET Core
        internal SqlConnectionPoolKey(string connectionString, SqlCredential credential, string accessToken) : base(connectionString)
        {
            Debug.Assert(_credential == null || _accessToken == null, "Credential and AccessToken can't have the value at the same time.");
            _credential = credential;
            _accessToken = accessToken;
            CalculateHashCode();
        }
        #endregion
#endif

        private SqlConnectionPoolKey(SqlConnectionPoolKey key) : base(key)
        {
            _credential = key.Credential;
            _accessToken = key.AccessToken;
#if NETFRAMEWORK
            _serverCertificateValidationCallback = key._serverCertificateValidationCallback;
            _clientCertificateRetrievalCallback = key._clientCertificateRetrievalCallback;
#endif
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
                && string.CompareOrdinal(_accessToken, key._accessToken) == 0
#if NETFRAMEWORK
                && _serverCertificateValidationCallback == key._serverCertificateValidationCallback
                && _clientCertificateRetrievalCallback == key._clientCertificateRetrievalCallback
                && _originalNetworkAddressInfo == key._originalNetworkAddressInfo
#endif
                );
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

#if NETFRAMEWORK
            if (_originalNetworkAddressInfo != null)
            {
                unchecked
                {
                    _hashValue = _hashValue * 17 + _originalNetworkAddressInfo.GetHashCode();
                }
            }
#endif
        }
    }
}
