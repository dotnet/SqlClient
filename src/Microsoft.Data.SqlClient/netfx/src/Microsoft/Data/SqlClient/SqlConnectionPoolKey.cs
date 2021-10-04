// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal partial class SqlConnectionPoolKey : DbConnectionPoolKey
    {
        private readonly ServerCertificateValidationCallback _serverCertificateValidationCallback;
        private readonly ClientCertificateRetrievalCallback _clientCertificateRetrievalCallback;
        private readonly SqlClientOriginalNetworkAddressInfo _originalNetworkAddressInfo;

        private SqlConnectionPoolKey(SqlConnectionPoolKey key) : base(key)
        {
            _credential = key.Credential;
            _accessToken = key.AccessToken;
            _serverCertificateValidationCallback = key._serverCertificateValidationCallback;
            _clientCertificateRetrievalCallback = key._clientCertificateRetrievalCallback;
            CalculateHashCode();
        }

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

        public override bool Equals(object obj)
        {
            return (obj is SqlConnectionPoolKey key
                && _credential == key._credential
                && ConnectionString == key.ConnectionString
                && string.CompareOrdinal(_accessToken, key._accessToken) == 0
                && _serverCertificateValidationCallback == key._serverCertificateValidationCallback
                && _clientCertificateRetrievalCallback == key._clientCertificateRetrievalCallback
                && _originalNetworkAddressInfo == key._originalNetworkAddressInfo);
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

        partial void CalculateHashCode()
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
