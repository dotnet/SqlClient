// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    sealed internal partial class DbConnectionPoolIdentity
    {
        public static readonly DbConnectionPoolIdentity NoIdentity = new DbConnectionPoolIdentity(string.Empty, false, true);
        private static DbConnectionPoolIdentity s_lastIdentity = null;

        private readonly string _sidString;
        private readonly bool _isRestricted;
        private readonly bool _isNetwork;
        private readonly int _hashCode;

        private DbConnectionPoolIdentity(string sidString, bool isRestricted, bool isNetwork)
        {
            _sidString = sidString;
            _isRestricted = isRestricted;
            _isNetwork = isNetwork;
            _hashCode = sidString == null ? 0 : sidString.GetHashCode();
        }

        internal bool IsRestricted
        {
            get { return _isRestricted; }
        }


        override public bool Equals(object value)
        {
            bool result = ((this == NoIdentity) || (this == value));
            if (!result && value != null)
            {
                DbConnectionPoolIdentity that = ((DbConnectionPoolIdentity)value);
                result = ((_sidString == that._sidString) && (_isRestricted == that._isRestricted) && (_isNetwork == that._isNetwork));
            }
            return result;
        }

        override public int GetHashCode()
        {
            return _hashCode;
        }

        private static DbConnectionPoolIdentity GetCurrentManaged()
        {
            DbConnectionPoolIdentity current;
            string domainString = System.Environment.UserDomainName;
            string sidString = (!string.IsNullOrWhiteSpace(domainString) ? domainString + "\\" : "") + System.Environment.UserName;
            bool isNetwork = false;
            bool isRestricted = false;

            var lastIdentity = s_lastIdentity;

            if ((lastIdentity != null) && (lastIdentity._sidString == sidString) && (lastIdentity._isRestricted == isRestricted) && (lastIdentity._isNetwork == isNetwork))
            {
                current = lastIdentity;
            }
            else
            {
                current = new DbConnectionPoolIdentity(sidString, isRestricted, isNetwork);
            }
            s_lastIdentity = current;
            return current;
        }
    }
}

