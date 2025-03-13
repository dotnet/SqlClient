// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Versioning;
using System.Security.Permissions;
using System.Security.Principal;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    partial class DbConnectionPoolIdentity
    {
#if NETFRAMEWORK
        [ResourceExposure(ResourceScope.None)] // SxS: this method does not create named objects
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        internal static DbConnectionPoolIdentity GetCurrent()
        {
            return GetCurrentNative();
        }

        [SecurityPermission(SecurityAction.Assert, Flags = SecurityPermissionFlag.ControlPrincipal)]
        internal static WindowsIdentity GetCurrentWindowsIdentity()
        {
            return WindowsIdentity.GetCurrent();
        }
#else
        internal static DbConnectionPoolIdentity GetCurrent()
        {
            return TdsParserStateObjectFactory.UseManagedSNI ? GetCurrentManaged() : GetCurrentNative();
        }
#endif

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
    }
}

