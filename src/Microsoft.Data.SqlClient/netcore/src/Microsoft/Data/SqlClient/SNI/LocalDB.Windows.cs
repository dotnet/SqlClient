// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Data.SqlClient.SNI
{
    internal sealed class LocalDB
    {
        private static readonly LocalDB Instance = new LocalDB();
        private const string s_className = nameof(LocalDB);

        //HKEY_LOCAL_MACHINE
        private const string LocalDBInstalledVersionRegistryKey = "SOFTWARE\\Microsoft\\Microsoft SQL Server Local DB\\Installed Versions\\";

        private const string InstanceAPIPathValueName = "InstanceAPIPath";

        private const string ProcLocalDBStartInstance = "LocalDBStartInstance";

        private const int MAX_LOCAL_DB_CONNECTION_STRING_SIZE = 260;

        private IntPtr _startInstanceHandle = IntPtr.Zero;

        // Local Db api doc https://msdn.microsoft.com/en-us/library/hh217143.aspx
        // HRESULT LocalDBStartInstance( [Input ] PCWSTR pInstanceName, [Input ] DWORD dwFlags,[Output] LPWSTR wszSqlConnection,[Input/Output] LPDWORD lpcchSqlConnection);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int LocalDBStartInstance(
                [In][MarshalAs(UnmanagedType.LPWStr)] string localDBInstanceName,
                [In] int flags,
                [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder sqlConnectionDataSource,
                [In, Out] ref int bufferLength);

        private LocalDBStartInstance localDBStartInstanceFunc = null;

        private volatile SafeLibraryHandle _sqlUserInstanceLibraryHandle;

        private LocalDB() { }

        /// <summary>
        /// 
        /// </summary>
        public static Lazy<string> s_sqlLocalDBExe = new Lazy<string>(() => GetPathToSqlLocalDB());

        private static string GetPathToSqlLocalDB()
        {
            RegistryKey mssqlRegKey = null;

            try
            {
                mssqlRegKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server");
                if (mssqlRegKey != null)
                {
                    foreach (var item in mssqlRegKey.GetSubKeyNames().Where(x => int.TryParse(x, out var _)).OrderByDescending(_ => _))
                    {
                        using (var sk = mssqlRegKey.OpenSubKey($@"{item}\Tools\\ClientSetup", writable: false))
                        {
                            if (sk.GetValue("Path") is string value)
                            {
                                var path = Path.Combine(value, "SqlLocalDB.exe");
                                if (File.Exists(path))
                                {
                                    return path;
                                }
                            }
                        }
                    }

                }
            }
            catch
            {
                // Too bad...
            }
            finally
            {
                mssqlRegKey?.Close();
            }

            return null;
        }

        private static bool TryGetLocalDBConnectionStringUsingSqlLocalDBExe(string localDbInstance, out string connString)
        {
            connString = null;

            try
            {
                // Make sure the instance is running first. If it is, this call won't do any harm, aside from
                // wasting some time.
                var psi = new ProcessStartInfo(s_sqlLocalDBExe.Value, $"s \"{localDbInstance}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var proc = Process.Start(psi);

                proc.WaitForExit(milliseconds: 5000);

                psi = new ProcessStartInfo(s_sqlLocalDBExe.Value, $"i \"{localDbInstance}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                proc = Process.Start(psi);

                proc.WaitForExit(milliseconds: 5000);

                var alllines = proc.StandardOutput.ReadToEnd();

                SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, $"Called: {s_sqlLocalDBExe.Value} \"{localDbInstance}\"");

                var lines = alllines.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                // TODO: Are named pipes the only option for SqlLocaLDB?
                // TODO: Probably sensitive to move out of this method to avoid compiling it all the time
                System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("(np:.+)\r", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

                // Iterate over all the lines in the stdout looking for something that is a named pipe: that would be
                // the connection string we are looking for!
                foreach (var line in lines)
                {
                    var foo = regex.Match(line);

                    if (foo.Success)
                    {
                        connString = foo.Captures[0].Value;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        internal static string GetLocalDBConnectionString(string localDbInstance)
        {
            try
            {
                return Instance.LoadUserInstanceDll() ? Instance.GetConnectionString(localDbInstance) : null;
            }
            catch
            {
                SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "Falling back to use SqlLocalDB.exe.");

                // The old logic to load the SqlUserInstance.dll did not quite work possibly because
                // of an archiecture mismatch (e.g. we are running in an ARM64 process and SqlLocalDB.exe
                // installed on the machine is an AMD64 process).
                // We try to figure out what we need by asking SqlLocalDB.exe (out of proc).

                if (!TryGetLocalDBConnectionStringUsingSqlLocalDBExe(localDbInstance, out string connString))
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "Unable to to use SqlLocalDB.exe to get the ConnectionString.");
                    throw;
                }

                return connString;
            }
        }

        internal static IntPtr GetProcAddress(string functionName) =>
            Instance.LoadUserInstanceDll() ? Interop.Kernel32.GetProcAddress(LocalDB.Instance._sqlUserInstanceLibraryHandle, functionName) : IntPtr.Zero;

        private string GetConnectionString(string localDbInstance)
        {
            StringBuilder localDBConnectionString = new StringBuilder(MAX_LOCAL_DB_CONNECTION_STRING_SIZE + 1);
            int sizeOfbuffer = localDBConnectionString.Capacity;
            localDBStartInstanceFunc(localDbInstance, 0, localDBConnectionString, ref sizeOfbuffer);
            return localDBConnectionString.ToString();
        }

        internal enum LocalDBErrorState
        {
            NO_INSTALLATION, INVALID_CONFIG, NO_SQLUSERINSTANCEDLL_PATH, INVALID_SQLUSERINSTANCEDLL_PATH, NONE
        }

        internal static uint MapLocalDBErrorStateToCode(LocalDBErrorState errorState)
        {
            switch (errorState)
            {
                case LocalDBErrorState.NO_INSTALLATION:
                    return SNICommon.LocalDBNoInstallation;
                case LocalDBErrorState.INVALID_CONFIG:
                    return SNICommon.LocalDBInvalidConfig;
                case LocalDBErrorState.NO_SQLUSERINSTANCEDLL_PATH:
                    return SNICommon.LocalDBNoSqlUserInstanceDllPath;
                case LocalDBErrorState.INVALID_SQLUSERINSTANCEDLL_PATH:
                    return SNICommon.LocalDBInvalidSqlUserInstanceDllPath;
                case LocalDBErrorState.NONE:
                    return 0;
                default:
                    return SNICommon.LocalDBInvalidConfig;
            }
        }

        internal static string MapLocalDBErrorStateToErrorMessage(LocalDBErrorState errorState)
        {
            switch (errorState)
            {
                case LocalDBErrorState.NO_INSTALLATION:
                    return Strings.SNI_ERROR_52;
                case LocalDBErrorState.INVALID_CONFIG:
                    return Strings.SNI_ERROR_53;
                case LocalDBErrorState.NO_SQLUSERINSTANCEDLL_PATH:
                    return Strings.SNI_ERROR_54;
                case LocalDBErrorState.INVALID_SQLUSERINSTANCEDLL_PATH:
                    return Strings.SNI_ERROR_55;
                case LocalDBErrorState.NONE:
                    return Strings.SNI_ERROR_50;
                default:
                    return Strings.SNI_ERROR_53;
            }
        }

        /// <summary>
        /// Loads the User Instance dll.
        /// </summary>
        private bool LoadUserInstanceDll()
        {
            _sqlUserInstanceLibraryHandle = null;
            throw new Exception();
#if NONO
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                // Check in a non thread-safe way if the handle is already set for performance.
                if (_sqlUserInstanceLibraryHandle != null)
                {
                    return true;
                }

                lock (this)
                {
                    if (_sqlUserInstanceLibraryHandle != null)
                    {
                        return true;
                    }
                    //Get UserInstance Dll path
                    LocalDBErrorState registryQueryErrorState;

                    // Get the LocalDB instance dll path from the registry
                    string dllPath = GetUserInstanceDllPath(out registryQueryErrorState);

                    // If there was no DLL path found, then there is an error.
                    if (dllPath == null)
                    {
                        SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.INVALID_PROV, 0, MapLocalDBErrorStateToCode(registryQueryErrorState), MapLocalDBErrorStateToErrorMessage(registryQueryErrorState));
                        SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "User instance DLL path is null.");
                        return false;
                    }

                    // In case the registry had an empty path for dll
                    if (string.IsNullOrWhiteSpace(dllPath))
                    {
                        SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.INVALID_PROV, 0, SNICommon.LocalDBInvalidSqlUserInstanceDllPath, Strings.SNI_ERROR_55);
                        SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "User instance DLL path is invalid. DLL path = {0}", dllPath);
                        return false;
                    }

                    // Load the dll
                    SafeLibraryHandle libraryHandle = Interop.Kernel32.LoadLibraryExW(dllPath.Trim(), IntPtr.Zero, 0);

                    if (libraryHandle.IsInvalid)
                    {
                        SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.INVALID_PROV, 0, SNICommon.LocalDBFailedToLoadDll, Strings.SNI_ERROR_56);
                        SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "Library Handle is invalid. Could not load the dll.");
                        libraryHandle.Dispose();
                        return false;
                    }

                    // Load the procs from the DLLs
                    _startInstanceHandle = Interop.Kernel32.GetProcAddress(libraryHandle, ProcLocalDBStartInstance);

                    if (_startInstanceHandle == IntPtr.Zero)
                    {
                        SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.INVALID_PROV, 0, SNICommon.LocalDBBadRuntime, Strings.SNI_ERROR_57);
                        SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "Was not able to load the PROC from DLL. Bad Runtime.");
                        libraryHandle.Dispose();
                        return false;
                    }

                    // Set the delegate the invoke.
                    localDBStartInstanceFunc = (LocalDBStartInstance)Marshal.GetDelegateForFunctionPointer(_startInstanceHandle, typeof(LocalDBStartInstance));

                    if (localDBStartInstanceFunc == null)
                    {
                        SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.INVALID_PROV, 0, SNICommon.LocalDBBadRuntime, Strings.SNI_ERROR_57);
                        libraryHandle.Dispose();
                        _startInstanceHandle = IntPtr.Zero;
                        return false;
                    }

                    _sqlUserInstanceLibraryHandle = libraryHandle;
                    SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.INFO, "User Instance DLL was loaded successfully.");
                    return true;
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
#endif
        }

        /// <summary>
        /// Retrieves the part of the sqlUserInstance.dll from the registry
        /// </summary>
        /// <param name="errorState">In case the dll path is not found, the error is set here.</param>
        /// <returns></returns>
        private string GetUserInstanceDllPath(out LocalDBErrorState errorState)
        {
            long scopeID = SqlClientEventSource.Log.TrySNIScopeEnterEvent(s_className);
            try
            {
                string dllPath = null;
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(LocalDBInstalledVersionRegistryKey))
                {
                    if (key == null)
                    {
                        errorState = LocalDBErrorState.NO_INSTALLATION;
                        SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "No installation found.");
                        return null;
                    }

                    Version zeroVersion = new Version();

                    Version latestVersion = zeroVersion;

                    foreach (string subKey in key.GetSubKeyNames())
                    {
                        Version currentKeyVersion;

                        if (!Version.TryParse(subKey, out currentKeyVersion))
                        {
                            errorState = LocalDBErrorState.INVALID_CONFIG;
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "Invalid Configuration.");
                            return null;
                        }

                        if (latestVersion.CompareTo(currentKeyVersion) < 0)
                        {
                            latestVersion = currentKeyVersion;
                        }
                    }

                    // If no valid versions are found, then error out
                    if (latestVersion.Equals(zeroVersion))
                    {
                        errorState = LocalDBErrorState.INVALID_CONFIG;
                        SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "Invalid Configuration.");
                        return null;
                    }

                    // Use the latest version to get the DLL path
                    using (RegistryKey latestVersionKey = key.OpenSubKey(latestVersion.ToString()))
                    {

                        object instanceAPIPathRegistryObject = latestVersionKey.GetValue(InstanceAPIPathValueName);

                        if (instanceAPIPathRegistryObject == null)
                        {
                            errorState = LocalDBErrorState.NO_SQLUSERINSTANCEDLL_PATH;
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "No SQL user instance DLL. Instance API Path Registry Object Error.");
                            return null;
                        }

                        RegistryValueKind valueKind = latestVersionKey.GetValueKind(InstanceAPIPathValueName);

                        if (valueKind != RegistryValueKind.String)
                        {
                            errorState = LocalDBErrorState.INVALID_SQLUSERINSTANCEDLL_PATH;
                            SqlClientEventSource.Log.TrySNITraceEvent(s_className, EventType.ERR, "Invalid SQL user instance DLL path. Registry value kind mismatch.");
                            return null;
                        }

                        dllPath = (string)instanceAPIPathRegistryObject;

                        errorState = LocalDBErrorState.NONE;
                        return dllPath;
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TrySNIScopeLeaveEvent(scopeID);
            }
        }
    }
}
