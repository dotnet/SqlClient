// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Principal;
using Interop.Windows.Advapi32;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    internal sealed partial class DbConnectionPoolIdentity
    {
        public static readonly DbConnectionPoolIdentity NoIdentity =
            new DbConnectionPoolIdentity(sidString: string.Empty, isRestricted: false, isNetwork: true);

        private static DbConnectionPoolIdentity s_lastIdentity = null;

        private readonly string _sidString;
        private readonly bool _isRestricted;
        private readonly bool _isNetwork;
        private readonly long _authenticationId;
        private readonly int _hashCode;

        private DbConnectionPoolIdentity(string sidString, bool isRestricted, bool isNetwork, long authenticationId = 0)
        {
            _sidString = sidString;
            _isRestricted = isRestricted;
            _isNetwork = isNetwork;
            _authenticationId = authenticationId;
            _hashCode = unchecked(((((sidString == null ? 0 : sidString.GetHashCode()) * 397) ^ authenticationId.GetHashCode()) * 397
                ^ (isRestricted ? 1 : 0)) * 397 ^ (isNetwork ? 1 : 0));
        }

        // @TODO: Make auto-property
        internal bool IsRestricted
        {
            get { return _isRestricted; }
        }

        public override bool Equals(object value)
        {
            bool result = this == NoIdentity || this == value;
            if (!result && value != null)
            {
                DbConnectionPoolIdentity that = (DbConnectionPoolIdentity)value;
                result = _sidString == that._sidString &&
                         _isRestricted == that._isRestricted &&
                         _isNetwork == that._isNetwork &&
                         _authenticationId == that._authenticationId;
            }

            return result;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        internal static DbConnectionPoolIdentity GetCurrent()
        {
            return OsConstants.IsWindows ? GetCurrentWindows() : GetCurrentManaged();
        }

        #if NETFRAMEWORK
        internal static WindowsIdentity GetCurrentWindowsIdentity() =>
            WindowsIdentity.GetCurrent();
        #endif

        private static DbConnectionPoolIdentity GetCurrentManaged()
        {
            string domainString = Environment.UserDomainName;
            string sidString = string.IsNullOrWhiteSpace(domainString)
                ? Environment.UserName
                : $@"{domainString}\{Environment.UserName}";

            var lastIdentity = s_lastIdentity;
            
            DbConnectionPoolIdentity current = lastIdentity != null &&
                                               lastIdentity._sidString == sidString &&
                                               !lastIdentity._isRestricted &&
                                               !lastIdentity._isNetwork
                ? lastIdentity
                : new DbConnectionPoolIdentity(sidString, isRestricted: false, isNetwork: false);

            s_lastIdentity = current;
            return current;
        }

        private static DbConnectionPoolIdentity GetCurrentWindows()
        {
            DbConnectionPoolIdentity current;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                SecurityIdentifier user = identity.User;
                bool isNetwork = user.IsWellKnown(WellKnownSidType.NetworkSid);
                string sidString = user.Value;

                // NEW_CREDENTIALS logons retain the local SID but use different outbound credentials.
                // Key by the logon session, not TokenId, so duplicated tokens still share a pool.
                long authenticationId = Advapi32.GetAuthenticationId(identity.AccessToken);
                bool isRestricted = Advapi32.GetIsTokenRestricted(identity.AccessToken);

                var lastIdentity = s_lastIdentity;
                if (lastIdentity != null &&
                    lastIdentity._sidString == sidString &&
                    lastIdentity._isRestricted == isRestricted &&
                    lastIdentity._isNetwork == isNetwork &&
                    lastIdentity._authenticationId == authenticationId)
                {
                    current = lastIdentity;
                }
                else
                {
                    current = new DbConnectionPoolIdentity(sidString, isRestricted, isNetwork, authenticationId);
                }
            }
            s_lastIdentity = current;
            return current;
        }
    }
}
