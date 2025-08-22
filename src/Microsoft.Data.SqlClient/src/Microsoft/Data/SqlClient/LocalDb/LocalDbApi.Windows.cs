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

#if NETFRAMEWORK
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Threading;
#endif

namespace Microsoft.Data.SqlClient.LocalDb
{
    internal static class LocalDbApi
    {
        // Buffer size for Local DB error message 1K will be enough for all messages
        private const int ErrorMessageBufferSize = 1024;

        private const string LocalDbPrefix = @"(localdb)\";
        private const string LocalDbPrefixNamedPipe = @"np:\\.\pipe\LOCALDB#";
        
        // Flag for LocalDbFormatMessage that indicates that message can be truncated if it does
        // not fit in the buffer.
        private const uint LocalDbTruncateErrorMessage = 1;

        #if NETFRAMEWORK
        private const string PartialTrustFlagKey = "ALLOW_LOCALDB_IN_PARTIAL_TRUST";
        #endif
        
        private static readonly object s_dllLock = new object();
        
        #if NETFRAMEWORK
        private static readonly object s_configLock = new object();
        #endif

        private static LocalDbFormatMessageDelegate s_localDbFormatMessage;
        // This is copy of handle that SNI maintains, so we are responsible for freeing it -
        // therefore there we are not using SafeHandle
        private static IntPtr s_userInstanceDllHandle = IntPtr.Zero;
        
        #if NETFRAMEWORK
        private static Dictionary<string, InstanceInfo> s_configurableInstances;
        private static PermissionSet s_fullTrust;
        private static LocalDbCreateInstanceDelegate s_localDbCreateInstance;
        private static bool s_partialTrustAllowed;
        private static bool s_partialTrustFlagChecked;
        #endif

        #if NETFRAMEWORK
        [SuppressUnmanagedCodeSecurity]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int LocalDbCreateInstanceDelegate(
            [MarshalAs(UnmanagedType.LPWStr)] string version,
            [MarshalAs(UnmanagedType.LPWStr)] string instance,
            uint flags);
        #endif
        
        [SuppressUnmanagedCodeSecurity]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate int LocalDbFormatMessageDelegate(
            int hrLocalDb,
            uint dwFlags,
            uint dwLanguageId,
            StringBuilder buffer,
            ref uint bufferLength);
        
        #if NETFRAMEWORK
        private static LocalDbCreateInstanceDelegate LocalDbCreateInstance
        {
            get
            {
                if (s_localDbCreateInstance is null)
                {
                    lock (s_dllLock)
                    {
                        if (s_localDbCreateInstance is null)
                        {
                            IntPtr functionAddr = LoadProcAddress("LocalDBCreateInstance");
                            if (functionAddr == IntPtr.Zero)
                            {
                                int hResult = Marshal.GetLastWin32Error();
                                SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.LocalDBCreateInstance> GetProcAddress for LocalDBCreateInstance error 0x{0}", hResult);
                                throw CreateLocalDbException(errorMessage: StringsHelper.GetString("LocalDB_MethodNotFound"));
                            }
                            
                            s_localDbCreateInstance = (LocalDbCreateInstanceDelegate)Marshal.GetDelegateForFunctionPointer(functionAddr, typeof(LocalDbCreateInstanceDelegate));
                        }
                    }
                }

                return s_localDbCreateInstance;
            }
        }
        #endif
        
        private static LocalDbFormatMessageDelegate LocalDbFormatMessage
        {
            get
            {
                if (s_localDbFormatMessage is null)
                {
                    lock (s_dllLock)
                    {
                        if (s_localDbFormatMessage is null)
                        {
                            IntPtr functionAddr = LoadProcAddress("LocalDBFormatMessage");
                            if (functionAddr == IntPtr.Zero)
                            {
                                // SNI checks for LocalDBFormatMessage during DLL loading, so it is practically impossible to get this error.
                                int hResult = Marshal.GetLastWin32Error();
                                SqlClientEventSource.Log.TryTraceEvent("LocalDBAPI.LocalDBFormatMessage> GetProcAddress for LocalDBFormatMessage error 0x{0}", hResult);
                                throw CreateLocalDbException(errorMessage: Strings.LocalDB_MethodNotFound);
                            }
                            
                            s_localDbFormatMessage = Marshal.GetDelegateForFunctionPointer<LocalDbFormatMessageDelegate>(functionAddr);
                        }
                    }
                }

                return s_localDbFormatMessage;
            }
        }
        
        private static IntPtr UserInstanceDllHandle
        {
            get
            {
                if (s_userInstanceDllHandle == IntPtr.Zero)
                {
                    lock (s_dllLock)
                    {
                        if (s_userInstanceDllHandle == IntPtr.Zero)
                        {
                            SniNativeWrapper.SniQueryInfo(QueryType.SNI_QUERY_LOCALDB_HMODULE, ref s_userInstanceDllHandle);
                            if (s_userInstanceDllHandle != IntPtr.Zero)
                            {
                                #if NETFRAMEWORK
                                SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.UserInstanceDLLHandle> LocalDB - handle obtained");
                                #else
                                SqlClientEventSource.Log.TryTraceEvent("LocalDBAPI.UserInstanceDLLHandle | LocalDB - handle obtained");
                                #endif
                            }
                            else
                            {
                                SniNativeWrapper.SniGetLastError(out SniError sniError);
                                throw CreateLocalDbException(
                                    errorMessage: StringsHelper.GetString("LocalDB_FailedGetDLLHandle"),
                                    sniError: sniError.sniError);
                            }
                        }
                    }
                }
                return s_userInstanceDllHandle;
            }
        }

        #if NETFRAMEWORK
        internal static void AssertLocalDbPermissions()
        {
            s_partialTrustAllowed = true;
        }
        #endif
        
        #if NETFRAMEWORK
        internal static void CreateLocalDbInstance(string instance)
        {
            DemandLocalDbPermissions();
            if (s_configurableInstances is null)
            {
                // load list of instances from configuration, mark them as not created
                lock (s_configLock)
                {
                    if (s_configurableInstances is null)
                    {
                        Dictionary<string, InstanceInfo> tempConfigurableInstances =
                            new Dictionary<string, InstanceInfo>(StringComparer.OrdinalIgnoreCase);
                        object section = ConfigurationManager.GetSection("system.data.localdb");
                        if (section is not null)
                        {
                            // Validate section type
                            if (section is not LocalDbConfigurationSection configSection)
                            {
                                throw CreateLocalDbException(Strings.LocalDB_BadConfigSectionType);
                            }

                            foreach (LocalDbInstanceElement confElement in configSection.LocalDbInstances)
                            {
                                Debug.Assert(confElement.Name != null && confElement.Version != null, "Both name and version should not be null");
                                tempConfigurableInstances.Add(
                                    confElement.Name.Trim(),
                                    new InstanceInfo(confElement.Version.Trim()));
                            }
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.CreateLocalDBInstance> No system.data.localdb section found in configuration");
                        }

                        s_configurableInstances = tempConfigurableInstances;
                    }
                }
            }

            if (!s_configurableInstances.TryGetValue(instance, out InstanceInfo instanceInfo))
            {
                // Instance name was not in the config
                return;
            }

            if (instanceInfo.created)
            {
                // Instance has already been created
                return;
            }

            Debug.Assert(!instance.Contains("\0"), "Instance name should contain embedded nulls");

            if (instanceInfo.version.Contains("\0"))
            {
                throw CreateLocalDbException(errorMessage: Strings.LocalDB_InvalidVersion, instance: instance);
            }

            // LocalDBCreateInstance is thread- and cross-process safe method, it is OK to call
            // from two threads simultaneously
            int hr = LocalDbCreateInstance(instanceInfo.version, instance, flags: 0);
            SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.CreateLocalDBInstance> Starting creation of instance {0} version {1}", instance, instanceInfo.version);

            if (hr < 0)
            {
                throw CreateLocalDbException(errorMessage: StringsHelper.GetString("LocalDB_CreateFailed"), instance: instance, localDbError: hr);
            }

            // Mark instance as created
            SqlClientEventSource.Log.TryTraceEvent("<sc.LocalDBAPI.CreateLocalDBInstance> Finished creation of instance {0}", instance);
            instanceInfo.created = true;
        }
        #endif
        
        #if NETFRAMEWORK
        internal static void DemandLocalDbPermissions()
        {
            if (!s_partialTrustAllowed)
            {
                if (!s_partialTrustFlagChecked)
                {
                    object partialTrustFlagValue = AppDomain.CurrentDomain.GetData(PartialTrustFlagKey);
                    if (partialTrustFlagValue is bool partialTrustFlagValueBool)
                    {
                        s_partialTrustAllowed = partialTrustFlagValueBool;
                    }

                    s_partialTrustFlagChecked = true;
                    if (s_partialTrustAllowed)
                    {
                        return;
                    }
                }

                s_fullTrust ??= new NamedPermissionSet("FullTrust");
                s_fullTrust.Demand();
            }
        }
        #endif
        
        // Check if name is in format (localdb)\<InstanceName - not empty> and return instance name if it is
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
                else if (input.StartsWith(LocalDbPrefixNamedPipe.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return input.ToString();
                }

            }
            return null;
        }
        
        internal static string GetLocalDbMessage(int hrCode)
        {
            Debug.Assert(hrCode < 0, "HRCode does not indicate error");
            try
            {
                StringBuilder buffer = new StringBuilder(ErrorMessageBufferSize);
                uint len = (uint)buffer.Capacity;

                // First try for current culture
                int hResult = LocalDbFormatMessage(
                    hrLocalDb: hrCode,
                    dwFlags: LocalDbTruncateErrorMessage,
                    dwLanguageId: (uint)CultureInfo.CurrentCulture.LCID,
                    buffer: buffer,
                    bufferLength: ref len);

                if (hResult >= 0)
                {
                    return buffer.ToString();
                }

                // Message is not available for current culture, try default
                buffer = new StringBuilder(ErrorMessageBufferSize);
                len = (uint)buffer.Capacity;
                hResult = LocalDbFormatMessage(
                    hrLocalDb: hrCode,
                    dwFlags: LocalDbTruncateErrorMessage,
                    dwLanguageId: 0, // Thread locale with fallback to English
                    buffer: buffer,
                    bufferLength: ref len);

                if (hResult >= 0)
                {
                    return buffer.ToString();
                }

                return string.Format(CultureInfo.CurrentCulture, "{0} (0x{1:X}).", Strings.LocalDB_UnobtainableMessage, hResult);
            }
            catch (SqlException exc)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0} ({1}).", Strings.LocalDB_UnobtainableMessage, exc.Message);
            }
        }
        
        internal static void ReleaseDllHandles()
        {
            s_userInstanceDllHandle = IntPtr.Zero;
            s_localDbFormatMessage = null;
            
            #if NETFRAMEWORK
            s_localDbCreateInstance = null;
            #endif
        }
        
        private static SqlException CreateLocalDbException(
            string errorMessage,
            string instance = null,
            int localDbError = 0,
            uint sniError = 0)
        {
            Debug.Assert(localDbError == 0 || sniError == 0, "LocalDB error and SNI error cannot be specified simultaneously");
            Debug.Assert(!string.IsNullOrEmpty(errorMessage), "Error message should not be null or empty");

            int errorCode = localDbError == 0 ? (int)sniError : localDbError;

            if (sniError != 0)
            {
                string sniErrorMessage = SQL.GetSNIErrorMessage(sniError);
                errorMessage = $"{errorMessage} (error: {sniError} - {sniErrorMessage})";
            }

            SqlErrorCollection collection = new SqlErrorCollection()
            {
                new SqlError(
                    infoNumber: errorCode,
                    errorState: 0,
                    errorClass: TdsEnums.FATAL_ERROR_CLASS,
                    server: instance,
                    errorMessage: errorMessage,
                    procedure: null,
                    lineNumber: 0)
            };

            if (localDbError != 0)
            {
                collection.Add(new SqlError(
                    infoNumber: errorCode,
                    errorState: 0,
                    errorClass: TdsEnums.FATAL_ERROR_CLASS,
                    server: instance,
                    errorMessage: GetLocalDbMessage(localDbError),
                    procedure: null,
                    lineNumber: 0));
            }


            SqlException exc = SqlException.CreateException(collection, null);
            exc._doNotReconnect = true;

            return exc;
        }
        
        private static IntPtr LoadProcAddress(string funcName) =>
            #if NETFRAMEWORK
            Kernel32Safe.GetProcAddress(UserInstanceDllHandle, funcName);
            #else
            Kernel32.GetProcAddress(UserInstanceDllHandle, funcName);
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
