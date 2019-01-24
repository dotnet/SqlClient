//------------------------------------------------------------------------------
// <copyright file="SqlClientOriginalAddressInfo.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">tomtal</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient {

    using System;
    using System.Net;
    
    /// <summary>
    /// Class to pass original client information.
    /// </summary>
    /// <param name="certificate"></param>
    /// <returns></returns>
#if ADONET_ORIGINAL_CLIENT_ADDRESS
    public 
#else
    internal
#endif
    sealed class SqlClientOriginalNetworkAddressInfo
    {
        public SqlClientOriginalNetworkAddressInfo(IPAddress address, bool isFromDataSecurityProxy = false)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }
        
            _address = address;
            _isFromDataSecurityProxy = isFromDataSecurityProxy;
        }

        public override int GetHashCode()  
        {
            return _address != null ? _address.GetHashCode() : 0;
        }

        public override bool Equals(object other) 
        {
            SqlClientOriginalNetworkAddressInfo otherAddress = other as SqlClientOriginalNetworkAddressInfo;

            if (otherAddress == null)
            {
                return false;
            }

            if (otherAddress._address != _address)
            {
                return false;
            }

            if (_isFromDataSecurityProxy != otherAddress._isFromDataSecurityProxy)
            {
                return false;
            }

            return true;
        }

        public IPAddress Address
        {
            get { return _address; }
        }

        public bool IsFromDataSecurityProxy 
        {
            get { return _isFromDataSecurityProxy; }
        }

        private IPAddress _address;

        private bool _isFromDataSecurityProxy;
    }
}

