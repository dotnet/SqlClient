// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Principal;
using Interop.Windows.Advapi32;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    internal sealed partial class DbConnectionPoolIdentity : IEquatable<DbConnectionPoolIdentity>
    {
        public static readonly DbConnectionPoolIdentity NoIdentity =
            new DbConnectionPoolIdentity(sidString: string.Empty, isRestricted: false, isNetwork: true, authenticationId: 0);

        private static DbConnectionPoolIdentity s_lastIdentity = null;

        private readonly string _sidString;
        private readonly bool _isRestricted;
        private readonly bool _isNetwork;
        private readonly long _authenticationId;
        private readonly int _hashCode;

        private DbConnectionPoolIdentity(string sidString, bool isRestricted, bool isNetwork, long authenticationId)
        {
            _sidString = sidString;
            _isRestricted = isRestricted;
            _isNetwork = isNetwork;
            _authenticationId = authenticationId;
            _hashCode = (sidString, isRestricted, isNetwork, authenticationId).GetHashCode();
        }

        // @TODO: Make auto-property
        internal bool IsRestricted
        {
            get { return _isRestricted; }
        }

        public override bool Equals(object value) =>
            Equals((DbConnectionPoolIdentity)value);

        public bool Equals(DbConnectionPoolIdentity value)
        {
            bool result = this == value;
            if (!result && value != null)
            {
                result = Equals(value._sidString, value._isRestricted, value._isNetwork, value._authenticationId);
            }

            return result;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        private bool Equals(string sidString, bool isRestricted, bool isNetwork, long authenticationId) =>
            _sidString == sidString &&
            _isRestricted == isRestricted &&
            _isNetwork == isNetwork &&
            _authenticationId == authenticationId;

        internal static DbConnectionPoolIdentity GetCurrent()
        {
            // Windows token identity applies to both managed and native networking.
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
                                               lastIdentity.Equals(sidString, isRestricted: false, isNetwork: false, authenticationId: 0)
                ? lastIdentity
                : new DbConnectionPoolIdentity(sidString, isRestricted: false, isNetwork: false, authenticationId: 0);

            s_lastIdentity = current;
            return current;
        }

        private static DbConnectionPoolIdentity GetCurrentWindows()
        {
            DbConnectionPoolIdentity current;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                SecurityIdentifier user = identity.User;
                // TODO: Remove this SID-derived redundant key field after checking NoIdentity and non-Windows behavior.
                bool isNetwork = user.IsWellKnown(WellKnownSidType.NetworkSid);
                string sidString = user.Value;

                // NEW_CREDENTIALS logons retain the local SID but use different outbound credentials.
                // Key by the logon session, not TokenId, so duplicated tokens still share a pool.
                long authenticationId = Advapi32.GetAuthenticationId(identity.AccessToken);
                bool isRestricted = Advapi32.GetIsTokenRestricted(identity.AccessToken);

                var lastIdentity = s_lastIdentity;
                if (lastIdentity != null &&
                    lastIdentity.Equals(sidString, isRestricted, isNetwork, authenticationId))
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
