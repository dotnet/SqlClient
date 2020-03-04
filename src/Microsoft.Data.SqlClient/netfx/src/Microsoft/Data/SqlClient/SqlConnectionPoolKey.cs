// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    // SqlConnectionPoolKey: Implementation of a key to connection pool groups for specifically to be used for SqlConnection
    //  Connection string and SqlCredential are used as a key
    internal class SqlConnectionPoolKey : DbConnectionPoolKey, ICloneable
    {
        private SqlCredential _credential;
        private int _hashValue;
        private readonly string _accessToken;
        private ServerCertificateValidationCallback _serverCertificateValidationCallback;
        private ClientCertificateRetrievalCallback _clientCertificateRetrievalCallback;
        private SqlClientOriginalNetworkAddressInfo _originalNetworkAddressInfo;

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

        private SqlConnectionPoolKey(SqlConnectionPoolKey key) : base(key)
        {
            _credential = key.Credential;
            _accessToken = key.AccessToken;
            _serverCertificateValidationCallback = key._serverCertificateValidationCallback;
            _clientCertificateRetrievalCallback = key._clientCertificateRetrievalCallback;
            CalculateHashCode();
        }

        object ICloneable.Clone()
        {
            return new SqlConnectionPoolKey(this);
        }

        internal override string ConnectionString
        {
            get
            {
                return base.ConnectionString;
            }

            set
            {
                base.ConnectionString = value;
                CalculateHashCode();
            }
        }

        internal SqlCredential Credential
        {
            get
            {
                return _credential;
            }
        }

        internal string AccessToken
        {
            get
            {
                return _accessToken;
            }
        }

        internal ServerCertificateValidationCallback ServerCertificateValidationCallback
        {
            get
            {
                return _serverCertificateValidationCallback;
            }
        }

        internal ClientCertificateRetrievalCallback ClientCertificateRetrievalCallback
        {
            get
            {
                return _clientCertificateRetrievalCallback;
            }
        }

        internal SqlClientOriginalNetworkAddressInfo OriginalNetworkAddressInfo
        {
            get
            {
                return _originalNetworkAddressInfo;
            }
        }

        public override bool Equals(object obj)
        {
            SqlConnectionPoolKey key = obj as SqlConnectionPoolKey;

            return (key != null &&
                    _credential == key._credential &&
                    ConnectionString == key.ConnectionString &&
                    string.CompareOrdinal(_accessToken, key._accessToken) == 0 &&
                    _serverCertificateValidationCallback == key._serverCertificateValidationCallback &&
                    _clientCertificateRetrievalCallback == key._clientCertificateRetrievalCallback &&
                    _originalNetworkAddressInfo == key._originalNetworkAddressInfo);
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

            if (_originalNetworkAddressInfo != null)
            {
                unchecked
                {
                    _hashValue = _hashValue * 17 + _originalNetworkAddressInfo.GetHashCode();
                }
            }
        }
    }
}
