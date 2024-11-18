// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Interop.Windows.Kernel32;
using Interop.Windows.Sni;
using Microsoft.Data.SqlClient;

#if NETFRAMEWORK
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Data.SqlClient.LocalDb;
#endif

namespace Microsoft.Data
{
    internal static class LocalDBAPI
    {
        private const int const_ErrorMessageBufferSize = 1024;      // Buffer size for Local DB error message 1K will be enough for all messages
        private const uint const_LOCALDB_TRUNCATE_ERR_MESSAGE = 1;// flag for LocalDBFormatMessage that indicates that message can be truncated if it does not fit in the buffer
        private const string LocalDbPrefix = @"(localdb)\";
        private const string LocalDbPrefix_NP = @"np:\\.\pipe\LOCALDB#";
        
        #if NETFRAMEWORK
        private const string Const_partialTrustFlagKey = "ALLOW_LOCALDB_IN_PARTIAL_TRUST";
        #endif
        
        private static readonly object s_dllLock = new object();
        
        #if NETFRAMEWORK
        private static readonly object s_configLock = new object();
        #endif

        private static LocalDBFormatMessageDelegate s_localDBFormatMessage = null;
        //This is copy of handle that SNI maintains, so we are responsible for freeing it - therefore there we are not using SafeHandle
        private static IntPtr s_userInstanceDLLHandle = IntPtr.Zero;
        
        #if NETFRAMEWORK
        private static PermissionSet _fullTrust = null;
        private static bool _partialTrustFlagChecked = false;
        private static bool _partialTrustAllowed = false;
        private static Dictionary<string, InstanceInfo> s_configurableInstances = null;
        private static LocalDBCreateInstanceDelegate s_localDBCreateInstance = null;
        #endif

        #if NETFRAMEWORK
        [SuppressUnmanagedCodeSecurity]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int LocalDBCreateInstanceDelegate([MarshalAs(UnmanagedType.LPWStr)] string version, [MarshalAs(UnmanagedType.LPWStr)] string instance, UInt32 flags);
        #endif
        
        [SuppressUnmanagedCodeSecurity]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate int LocalDBFormatMessageDelegate(int hrLocalDB, uint dwFlags, uint dwLanguageId, StringBuilder buffer, ref uint buflen);
        
        #if NETFRAMEWORK
        private static LocalDBCreateInstanceDelegate LocalDBCreateInstance
        {
            get
            {
                if (s_localDBCreateInstance == null)
                {
                    RuntimeHelpers.PrepareConstrainedRegions();
                    
                    lock (s_dllLock)
                    {
                        if (s_localDBCreateInstance == null)
                        {
                            IntPtr functionAddr = LoadProcAddress("LocalDBCreateInstance");
                            if (functionAddr == IntPtr.Zero)
                            {
                                int hResult = Marshal.GetLastWin32Error();
                                SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.LocalDBCreateInstance> GetProcAddress for LocalDBCreateInstance error 0x{0}", hResult);
                                throw CreateLocalDBException(errorMessage: StringsHelper.GetString("LocalDB_MethodNotFound"));
                            }
                            
                            s_localDBCreateInstance = (LocalDBCreateInstanceDelegate)Marshal.GetDelegateForFunctionPointer(functionAddr, typeof(LocalDBCreateInstanceDelegate));
                        }
                    }
                }
                return s_localDBCreateInstance;
            }
        }
        #endif
        
        private static LocalDBFormatMessageDelegate LocalDBFormatMessage
        {
            get
            {
                if (s_localDBFormatMessage == null)
                {
                    #if NETFRAMEWORK
                    RuntimeHelpers.PrepareConstrainedRegions();
                    #endif
                    
                    lock (s_dllLock)
                    {
                        if (s_localDBFormatMessage == null)
                        {
                            IntPtr functionAddr = LoadProcAddress("LocalDBFormatMessage");
                            if (functionAddr == IntPtr.Zero)
                            {
                                // SNI checks for LocalDBFormatMessage during DLL loading, so it is practically impossible to get this error.
                                int hResult = Marshal.GetLastWin32Error();
                                SqlClientEventSource.Log.TryTraceEvent("LocalDBAPI.LocalDBFormatMessage> GetProcAddress for LocalDBFormatMessage error 0x{0}", hResult);
                                throw CreateLocalDBException(errorMessage: Strings.LocalDB_MethodNotFound);
                            }
                            
                            s_localDBFormatMessage = Marshal.GetDelegateForFunctionPointer<LocalDBFormatMessageDelegate>(functionAddr);
                        }
                    }
                }
                return s_localDBFormatMessage;
            }
        }
        
        private static IntPtr UserInstanceDLLHandle
        {
            get
            {
                if (s_userInstanceDLLHandle == IntPtr.Zero)
                {
                    #if NETFRAMEWORK
                    RuntimeHelpers.PrepareConstrainedRegions();
                    #endif
                    
                    lock (s_dllLock)
                    {
                        if (s_userInstanceDLLHandle == IntPtr.Zero)
                        {
                            SniNativeWrapper.SNIQueryInfo(QueryType.SNI_QUERY_LOCALDB_HMODULE, ref s_userInstanceDLLHandle);
                            if (s_userInstanceDLLHandle != IntPtr.Zero)
                            {
                                #if NETFRAMEWORK
                                SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.UserInstanceDLLHandle> LocalDB - handle obtained");
                                #else
                                SqlClientEventSource.Log.TryTraceEvent("LocalDBAPI.UserInstanceDLLHandle | LocalDB - handle obtained");
                                #endif
                            }
                            else
                            {
                                SniNativeWrapper.SNIGetLastError(out SniError sniError);
                                throw CreateLocalDBException(errorMessage: StringsHelper.GetString("LocalDB_FailedGetDLLHandle"), sniError: sniError.sniError);
                            }
                        }
                    }
                }
                return s_userInstanceDLLHandle;
            }
        }

        #if NETFRAMEWORK
        internal static void AssertLocalDBPermissions()
        {
            _partialTrustAllowed = true;
        }
        #endif
        
        #if NETFRAMEWORK
        internal static void CreateLocalDBInstance(string instance)
        {
            DemandLocalDBPermissions();
            if (s_configurableInstances == null)
            {
                // load list of instances from configuration, mark them as not created
                bool lockTaken = false;
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Monitor.Enter(s_configLock, ref lockTaken);
                    if (s_configurableInstances == null)
                    {
                        Dictionary<string, InstanceInfo> tempConfigurableInstances = new Dictionary<string, InstanceInfo>(StringComparer.OrdinalIgnoreCase);
                        object section = ConfigurationManager.GetSection("system.data.localdb");
                        if (section != null) // if no section just skip creation
                        {
                            // validate section type
                            LocalDbConfigurationSection configSection = section as LocalDbConfigurationSection;
                            if (configSection == null)
                            {
                                throw CreateLocalDBException(errorMessage: StringsHelper.GetString("LocalDB_BadConfigSectionType"));
                            }

                            foreach (LocalDbInstanceElement confElement in configSection.LocalDbInstances)
                            {
                                Debug.Assert(confElement.Name != null && confElement.Version != null, "Both name and version should not be null");
                                tempConfigurableInstances.Add(confElement.Name.Trim(), new InstanceInfo(confElement.Version.Trim()));
                            }
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.CreateLocalDBInstance> No system.data.localdb section found in configuration");
                        }
                        s_configurableInstances = tempConfigurableInstances;
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(s_configLock);
                    }
                }
            }

            InstanceInfo instanceInfo = null;

            if (!s_configurableInstances.TryGetValue(instance, out instanceInfo))
            {
                // instance name was not in the config
                return;
            }

            if (instanceInfo.created)
            {
                // instance has already been created
                return;
            }

            Debug.Assert(!instance.Contains("\0"), "Instance name should contain embedded nulls");

            if (instanceInfo.version.Contains("\0"))
            {
                throw CreateLocalDBException(errorMessage: StringsHelper.GetString("LocalDB_InvalidVersion"), instance: instance);
            }

            // LocalDBCreateInstance is thread- and cross-process safe method, it is OK to call from two threads simultaneously
            int hr = LocalDBCreateInstance(instanceInfo.version, instance, flags: 0);
            SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.CreateLocalDBInstance> Starting creation of instance {0} version {1}", instance, instanceInfo.version);

            if (hr < 0)
            {
                throw CreateLocalDBException(errorMessage: StringsHelper.GetString("LocalDB_CreateFailed"), instance: instance, localDbError: hr);
            }

            SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.CreateLocalDBInstance> Finished creation of instance {0}", instance);
            instanceInfo.created = true; // mark instance as created
        }
        #endif
        
        #if NETFRAMEWORK
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
        #endif
        
        // check if name is in format (localdb)\<InstanceName - not empty> and return instance name if it is
        // localDB can also have a format of np:\\.\pipe\LOCALDB#<some number>\tsql\query
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
        
        internal static string GetLocalDBMessage(int hrCode)
        {
            Debug.Assert(hrCode < 0, "HRCode does not indicate error");
            try
            {
                StringBuilder buffer = new StringBuilder((int)const_ErrorMessageBufferSize);
                uint len = (uint)buffer.Capacity;

                // First try for current culture
                int hResult = LocalDBFormatMessage(hrLocalDB: hrCode, dwFlags: const_LOCALDB_TRUNCATE_ERR_MESSAGE, dwLanguageId: (uint)CultureInfo.CurrentCulture.LCID,
                    buffer: buffer, buflen: ref len);
                if (hResult >= 0)
                {
                    return buffer.ToString();
                }
                else
                {
                    // Message is not available for current culture, try default
                    buffer = new StringBuilder((int)const_ErrorMessageBufferSize);
                    len = (uint)buffer.Capacity;
                    hResult = LocalDBFormatMessage(hrLocalDB: hrCode, dwFlags: const_LOCALDB_TRUNCATE_ERR_MESSAGE, dwLanguageId: 0 /* thread locale with fallback to English */,
                        buffer: buffer, buflen: ref len);
                    if (hResult >= 0)
                    {
                        return buffer.ToString();
                    }
                    else
                    {
                        return string.Format(CultureInfo.CurrentCulture, "{0} (0x{1:X}).", Strings.LocalDB_UnobtainableMessage, hResult);
                    }
                }
            }
            catch (SqlException exc)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0} ({1}).", Strings.LocalDB_UnobtainableMessage, exc.Message);
            }
        }
        
        internal static void ReleaseDLLHandles()
        {
            s_userInstanceDLLHandle = IntPtr.Zero;
            s_localDBFormatMessage = null;
            
            #if NETFRAMEWORK
            s_localDBCreateInstance = null;
            #endif
        }
        
        private static SqlException CreateLocalDBException(string errorMessage, string instance = null, int localDbError = 0, uint sniError = 0)
        {
            Debug.Assert((localDbError == 0) || (sniError == 0), "LocalDB error and SNI error cannot be specified simultaneously");
            Debug.Assert(!string.IsNullOrEmpty(errorMessage), "Error message should not be null or empty");
            SqlErrorCollection collection = new SqlErrorCollection();

            int errorCode = (localDbError == 0) ? (int)sniError : localDbError;

            if (sniError != 0)
            {
                string sniErrorMessage = SQL.GetSNIErrorMessage(sniError);
                errorMessage = string.Format("{0} (error: {1} - {2})", errorMessage, sniError, sniErrorMessage);
            }

            collection.Add(new SqlError(errorCode, 0, TdsEnums.FATAL_ERROR_CLASS, instance, errorMessage, null, 0));

            if (localDbError != 0)
            {
                collection.Add(new SqlError(errorCode, 0, TdsEnums.FATAL_ERROR_CLASS, instance, GetLocalDBMessage(localDbError), null, 0));
            }

            SqlException exc = SqlException.CreateException(collection, null);

            exc._doNotReconnect = true;

            return exc;
        }
        
        private static IntPtr LoadProcAddress(string funcName) =>
            #if NETFRAMEWORK
            Kernel32Safe.GetProcAddress(UserInstanceDLLHandle, funcName);
            #else
            Kernel32.GetProcAddress(UserInstanceDLLHandle, funcName);
            #endif

        #if NETFRAMEWORK
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
        #endif
    }
}
