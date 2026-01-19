// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#if NETFRAMEWORK
using System.Runtime.Versioning;
using System.Security.Principal;
#endif

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
        private readonly int _hashCode;

        private DbConnectionPoolIdentity(string sidString, bool isRestricted, bool isNetwork)
        {
            _sidString = sidString;
            _isRestricted = isRestricted;
            _isNetwork = isNetwork;
            _hashCode = sidString == null ? 0 : sidString.GetHashCode();
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
                         _isNetwork == that._isNetwork;
            }

            return result;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        #if NETFRAMEWORK
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        [ResourceExposure(ResourceScope.None)] // SxS: this method does not create named objects
        #endif
        internal static DbConnectionPoolIdentity GetCurrent()
        {
            #if NETFRAMEWORK
            return GetCurrentNative();
            #else

            #if _UNIX
            return GetCurrentManaged();
            #else
            return LocalAppContextSwitches.UseManagedNetworking
                ? GetCurrentManaged()
                : GetCurrentNative();
            #endif

            #endif
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

        #if _WINDOWS
        private static DbConnectionPoolIdentity GetCurrentNative()
        {
            DbConnectionPoolIdentity current;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                IntPtr token = identity.AccessToken.DangerousGetHandle();
                SecurityIdentifier user = identity.User;
                bool isNetwork = user.IsWellKnown(WellKnownSidType.NetworkSid);
                string sidString = user.Value;

                // Win32NativeMethods.IsTokenRestricted will raise exception if the native call fails
                SniNativeWrapper.SniIsTokenRestricted(token, out bool isRestricted);

                var lastIdentity = s_lastIdentity;
                if (lastIdentity != null &&
                    lastIdentity._sidString == sidString &&
                    lastIdentity._isRestricted == isRestricted &&
                    lastIdentity._isNetwork == isNetwork)
                {
                    current = lastIdentity;
                }
                else
                {
                    current = new DbConnectionPoolIdentity(sidString, isRestricted, isNetwork);
                }
            }
            s_lastIdentity = current;
            return current;
        }
        #endif
    }
}

