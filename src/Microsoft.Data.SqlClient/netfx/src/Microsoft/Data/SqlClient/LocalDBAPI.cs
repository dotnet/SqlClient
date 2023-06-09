// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Microsoft.Data
{
    internal static class LocalDBAPI
    {
        private const string LocalDbPrefix = @"(localdb)\";
        private const string LocalDbPrefix_NP = @"np:\\.\pipe\LOCALDB#";
        const string Const_partialTrustFlagKey = "ALLOW_LOCALDB_IN_PARTIAL_TRUST";

        static PermissionSet _fullTrust = null;
        static bool _partialTrustFlagChecked = false;
        static bool _partialTrustAllowed = false;


        // check if name is in format (localdb)\<InstanceName - not empty> and return instance name if it is
        internal static string GetLocalDbInstanceNameFromServerName(string serverName)
        {
            if (serverName is not null)
            {
                // it can start with spaces if specified in quotes
                // Memory allocation is reduced by using ReadOnlySpan
                ReadOnlySpan<char> input = serverName.AsSpan().Trim();
                if (input.StartsWith(LocalDbPrefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    input = input.Slice(LocalDbPrefix.Length);
                    if (!input.IsEmpty)
                    {
                        return input.ToString();
                    }
                }
                else if (input.StartsWith(LocalDbPrefix_NP.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return input.ToString();
                }

            }
            return null;
        }

        internal static void ReleaseDLLHandles()
        {
            s_userInstanceDLLHandle = IntPtr.Zero;
        }



        //This is copy of handle that SNI maintains, so we are responsible for freeing it - therefore there we are not using SafeHandle
        static IntPtr s_userInstanceDLLHandle = IntPtr.Zero;

        static object s_dllLock = new object();

    
        [SuppressUnmanagedCodeSecurity]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int LocalDBCreateInstanceDelegate([MarshalAs(UnmanagedType.LPWStr)] string version, [MarshalAs(UnmanagedType.LPWStr)] string instance, UInt32 flags);
 

        [SuppressUnmanagedCodeSecurity]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate int LocalDBFormatMessageDelegate(int hrLocalDB, UInt32 dwFlags, UInt32 dwLanguageId, StringBuilder buffer, ref UInt32 buflen);

      

        const UInt32 const_LOCALDB_TRUNCATE_ERR_MESSAGE = 1;// flag for LocalDBFormatMessage that indicates that message can be truncated if it does not fit in the buffer
        const int const_ErrorMessageBufferSize = 1024;      // Buffer size for Local DB error message, according to Serverless team, 1K will be enough for all messages


        private class InstanceInfo
        {
            internal InstanceInfo(string version)
            {
                this.version = version;
                this.created = false;
            }

            internal readonly string version;
            internal bool created;
        }

        static object s_configLock = new object();
    
        internal static void DemandLocalDBPermissions()
        {
            if (!_partialTrustAllowed)
            {
                if (!_partialTrustFlagChecked)
                {
                    object partialTrustFlagValue = AppDomain.CurrentDomain.GetData(Const_partialTrustFlagKey);
                    if (partialTrustFlagValue != null && partialTrustFlagValue is bool)
                    {
                        _partialTrustAllowed = (bool)partialTrustFlagValue;
                    }
                    _partialTrustFlagChecked = true;
                    if (_partialTrustAllowed)
                    {
                        return;
                    }
                }
                if (_fullTrust == null)
                {
                    _fullTrust = new NamedPermissionSet("FullTrust");
                }
                _fullTrust.Demand();
            }
        }

        internal static void AssertLocalDBPermissions()
        {
            _partialTrustAllowed = true;
        }


    }
}
