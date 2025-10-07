// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Class to pass original client information.
    /// </summary>
    public sealed class SqlClientOriginalNetworkAddressInfo
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientOriginalNetworkAddressInfo.xml' path='docs/members[@name="SqlClientOriginalNetworkAddressInfo"]/ctor/*' />
        public SqlClientOriginalNetworkAddressInfo(IPAddress address, bool isFromDataSecurityProxy = false, bool isVnetAddress = false)
        {
            _address = address ?? throw new ArgumentNullException("address");
            _isFromDataSecurityProxy = isFromDataSecurityProxy;
            _isVnetAddress = isVnetAddress;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientOriginalNetworkAddressInfo.xml' path='docs/members[@name="SqlClientOriginalNetworkAddressInfo"]/GetHashCode/*' />
        public override int GetHashCode()
        {
            return _address != null ? _address.GetHashCode() : 0;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientOriginalNetworkAddressInfo.xml' path='docs/members[@name="SqlClientOriginalNetworkAddressInfo"]/Equals/*' />
        public override bool Equals(object other)
        {
            var otherAddress = other as SqlClientOriginalNetworkAddressInfo;
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

            if (_isVnetAddress != otherAddress._isVnetAddress)
            {
                return false;
            }

            return true;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientOriginalNetworkAddressInfo.xml' path='docs/members[@name="SqlClientOriginalNetworkAddressInfo"]/Address/*' />
        public IPAddress Address
        {
            get { return _address; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientOriginalNetworkAddressInfo.xml' path='docs/members[@name="SqlClientOriginalNetworkAddressInfo"]/IsFromDataSecurityProxy/*' />
        public bool IsFromDataSecurityProxy
        {
            get { return _isFromDataSecurityProxy; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientOriginalNetworkAddressInfo.xml' path='docs/members[@name="SqlClientOriginalNetworkAddressInfo"]/IsVnetAddress/*' />
        public bool IsVnetAddress
        {
            get { return _isVnetAddress; }
        }

        private readonly IPAddress _address;

        private readonly bool _isFromDataSecurityProxy;

        private readonly bool _isVnetAddress;
    }
}
