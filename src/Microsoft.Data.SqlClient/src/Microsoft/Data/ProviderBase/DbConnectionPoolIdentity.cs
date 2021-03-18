// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.ProviderBase
{
    [Serializable] // Serializable so SqlDependencyProcessDispatcher can marshall cross domain to SqlDependency.
    internal sealed partial class DbConnectionPoolIdentity
    {
        public static readonly DbConnectionPoolIdentity s_noIdentity = new DbConnectionPoolIdentity(string.Empty, false, true);

        private readonly string _sidString;
        private readonly bool _isRestricted;
        private readonly bool _isNetwork;
        private readonly int _hashCode;

        private DbConnectionPoolIdentity(string sidString, bool isRestricted, bool isNetwork)
        {
            _sidString = sidString;
            _isRestricted = isRestricted;
            _isNetwork = isNetwork;
            _hashCode = sidString?.GetHashCode() ?? 0;
        }

        internal bool IsRestricted => _isRestricted;

        public override bool Equals(object value)
        {
            bool result = ReferenceEquals(this, s_noIdentity) || ReferenceEquals(this, value);
            if (!result && (!(value is null)))
            {
                DbConnectionPoolIdentity that = (DbConnectionPoolIdentity)value;
                result = 
                    string.Equals(_sidString,that._sidString,System.StringComparison.Ordinal) && 
                    (_isRestricted == that._isRestricted) && 
                    (_isNetwork == that._isNetwork);
            }
            return result;
        }

        override public int GetHashCode()
        {
            return _hashCode;
        }

        internal static DbConnectionPoolIdentity GetCurrentManaged()
        {
            string domainString = System.Environment.UserDomainName;
            string sidString = (!string.IsNullOrWhiteSpace(domainString) ? domainString + "\\" : "") + System.Environment.UserName;
            bool isNetwork = false;
            bool isRestricted = false;
            return new DbConnectionPoolIdentity(sidString, isRestricted, isNetwork);
        }
    }
}

