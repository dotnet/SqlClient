// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.ProviderBase;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Data.SqlClient
{
    internal sealed class LocalDB
    {
        private static readonly LocalDB Instance = new LocalDB();
        private const string ClassName = nameof(LocalDB);
        //HKEY_LOCAL_MACHINE
        private const string LocalDBInstalledVersionRegistryKey = "SOFTWARE\\Microsoft\\Microsoft SQL Server Local DB\\Installed Versions\\";

        // Set a non-infinite timeout on the regex to ensure we never hang. 10 seconds should
        // be more than enough as a fail-safe for the small amount of text that is going to be processed. Since
        // we can't set the timeout on each Match() call, this prioritizes perf (RegexOptions.Compiled) over
        // the small possiblity that we might go a little over a connect timeout during extremely high load.
        private static readonly Regex regex = new Regex("(np:.+)\r", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(10));

        private const string InstanceAPIPathValueName = "InstanceAPIPath";
        private const string ProcLocalDBStartInstance = "LocalDBStartInstance";
        private const int MAX_LOCAL_DB_CONNECTION_STRING_SIZE = 260;
        private IntPtr _startInstanceHandle = IntPtr.Zero;
        private static Lazy<string> s_sqlLocalDBExe = new Lazy<string>(() => GetPathToSqlLocalDB());
       
        // Local Db api doc https://msdn.microsoft.com/en-us/library/hh217143.aspx
        // HRESULT LocalDBStartInstance( [Input ] PCWSTR pInstanceName, [Input ] DWORD dwFlags,[Output] LPWSTR wszSqlConnection,[Input/Output] LPDWORD lpcchSqlConnection);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int LocalDBStartInstance(
                [In] [MarshalAs(UnmanagedType.LPWStr)] string localDBInstanceName,
                [In]  int flags,
                [Out] [MarshalAs(UnmanagedType.LPWStr)] StringBuilder sqlConnectionDataSource,
                [In, Out]ref int bufferLength);

        private LocalDBStartInstance localDBStartInstanceFunc = null;

        private volatile SafeLibraryHandle _sqlUserInstanceLibraryHandle;

        private LocalDB() { }

        internal static string GetLocalDBConnectionString(string localDbInstance, TimeoutTimer timeout)
        {
            try
            {
               return Instance.LoadUserInstanceDll() ? Instance.GetConnectionString(localDbInstance) : null;
            }
            catch(Exception ex)
            {
                SqlClientEventSource.Log.TryTraceEvent(ClassName, EventType.ERR,ex?.Message);
                SqlClientEventSource.Log.TryTraceEvent(ClassName, EventType.INFO, "Falling back to use SqlLocalDB.exe.");

                // The old logic to load the SqlUserInstance.dll did not quite work possibly because
                // of an archiecture mismatch (e.g. we are running in an ARM64 process and SqlLocalDB.exe
                // installed on the machine is an AMD64 process).
                // We try to figure out what we need by asking SqlLocalDB.exe (out of proc).

                if (!TryGetLocalDBConnectionStringUsingSqlLocalDBExe(localDbInstance, timeout, out string connString))
                {
                    SqlClientEventSource.Log.TryTraceEvent(ClassName, EventType.ERR, "Unable to to use SqlLocalDB.exe to get the ConnectionString.");
                    throw;
                }

                return connString;
            }
        }
    
        private static string GetPathToSqlLocalDB()
        {
            RegistryKey mssqlRegKey = null;

            try
            {
                mssqlRegKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server");
                if (mssqlRegKey != null)
                {
                    string[] alphaNumnericKeys = mssqlRegKey.GetSubKeyNames();
                    List<int> intKeys =new List<int>();
                    for(int i =0;i<alphaNumnericKeys.Length;i++)
                    {
                       if(int.TryParse(alphaNumnericKeys[i], out int intKey))
                        {
                            intKeys.Add(intKey);
                        }
                    }

                    for(int item =intKeys.Count-1;item>=0;item--)
                    {
                        using (RegistryKey sk = mssqlRegKey.OpenSubKey($@"{intKeys[item]}\Tools\\ClientSetup", writable: false))
                        {
                            if (sk.GetValue("Path") is string value)
                            {
                                string path = Path.Combine(value, "SqlLocalDB.exe");
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
                SqlClientEventSource.Log.TryTraceEvent(ClassName, EventType.ERR, "No File path exist for SqlLocalDB.exe under Registry key.");               
            }
            finally
            {
                mssqlRegKey?.Close();
            }

            return null;
        }

        private string GetConnectionString(string localDbInstance)
        {
            StringBuilder localDBConnectionString = new StringBuilder(MAX_LOCAL_DB_CONNECTION_STRING_SIZE + 1);
            int sizeOfbuffer = localDBConnectionString.Capacity;
            localDBStartInstanceFunc(localDbInstance, 0, localDBConnectionString, ref sizeOfbuffer);
            return localDBConnectionString.ToString();
        }
        private static bool TryGetLocalDBConnectionStringUsingSqlLocalDBExe(string localDbInstance, TimeoutTimer timeout, out string connString)
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

                if (proc.WaitForExit(milliseconds: timeout.MillisecondsRemainingInt))
                {
                    psi = new ProcessStartInfo(s_sqlLocalDBExe.Value, $"i \"{localDbInstance}\"")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    proc = Process.Start(psi);

                    if (proc.WaitForExit(milliseconds: timeout.MillisecondsRemainingInt))
                    {
                        var alllines = proc.StandardOutput.ReadToEnd();

                        SqlClientEventSource.Log.TryTraceEvent(ClassName, EventType.INFO, $"Called: {s_sqlLocalDBExe.Value} \"{localDbInstance}\"");

                        Match match = regex.Match(alllines);
                        if (match.Success)
                        {
                            connString = match.Value.Trim();
                            return true;
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.INFO, "No match found for named pipe SqlLocalDB.exe process stdout.");
                        }
                    }
                }
            }
            catch(RegexMatchTimeoutException ex)
            {
                SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "Unable to retrieve named pipe SqlLocalDB.exe process stdout, it took longer than the maximum time allowed."+ex?.Message);
            }
            catch
            {
                SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "Unable to start LocalDB.exe process.");
            }

            return false;
        }

        internal enum LocalDBErrorState
        {
            NO_INSTALLATION, INVALID_CONFIG, NO_SQLUSERINSTANCEDLL_PATH, INVALID_SQLUSERINSTANCEDLL_PATH, NONE
        }

        /// <summary>
        /// Loads the User Instance dll.
        /// </summary>
        private bool LoadUserInstanceDll()
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
                    SqlClientEventSource.Log.TraceEvent(nameof(LocalDB), EventType.ERR, "User instance DLL path is null.");
                    throw new Exception(MapLocalDBErrorStateToErrorMessage(registryQueryErrorState));
                }

                // In case the registry had an empty path for dll
                if (string.IsNullOrWhiteSpace(dllPath))
                {
                    SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "User instance DLL path is invalid. DLL path = {0}", dllPath);
                    throw new Exception(Strings.SNI_ERROR_55);
                }

                // Load the dll
                SafeLibraryHandle libraryHandle = Interop.Kernel32.LoadLibraryExW(dllPath.Trim(), IntPtr.Zero, 0);

                if (libraryHandle.IsInvalid)
                {
                    SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "Library Handle is invalid. Could not load the dll.");
                    libraryHandle.Dispose();
                    throw new Exception(Strings.SNI_ERROR_56);
                }

                // Load the procs from the DLLs
                _startInstanceHandle = Interop.Kernel32.GetProcAddress(libraryHandle, ProcLocalDBStartInstance);

                if (_startInstanceHandle == IntPtr.Zero)
                {
                    SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "Was not able to load the PROC from DLL. Bad Runtime.");
                    libraryHandle.Dispose();
                    throw new Exception(Strings.SNI_ERROR_57);
                }

                // Set the delegate the invoke.
                localDBStartInstanceFunc = (LocalDBStartInstance)Marshal.GetDelegateForFunctionPointer(_startInstanceHandle, typeof(LocalDBStartInstance));

                if (localDBStartInstanceFunc == null)
                {
                    libraryHandle.Dispose();
                    _startInstanceHandle = IntPtr.Zero;
                    throw new Exception(Strings.SNI_ERROR_57);
                }

                _sqlUserInstanceLibraryHandle = libraryHandle;
                SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.INFO, "User Instance DLL was loaded successfully.");
                return true;
            }
        }

        /// <summary>
        /// Gets the Local db Named pipe data source if the input is a localDB server.
        /// </summary>
        /// <param name="fullServerName">The data source</param>
        /// <param name="timeout for getting LocalDB instance name from SqlLocalDB.exe Proc"></param>
        /// <returns></returns>
        internal static string GetLocalDBDataSource(string fullServerName, TimeoutTimer timeout)
        {
            string localDBConnectionString = null;
            string localDBInstance = DataSource.GetLocalDBInstance(fullServerName, out bool isBadLocalDBDataSource);

            if (isBadLocalDBDataSource)
            {
                return null;
            }

            else if (!string.IsNullOrEmpty(localDBInstance))
            {
                // We have successfully received a localDBInstance which is valid.
                Debug.Assert(!string.IsNullOrWhiteSpace(localDBInstance), "Local DB Instance name cannot be empty.");
                localDBConnectionString = LocalDB.GetLocalDBConnectionString(localDBInstance, timeout);

                if (fullServerName == null)
                {
                    // The Last error is set in LocalDB.GetLocalDBConnectionString. We don't need to set Last here.
                   return null;
                }
            }
           return localDBConnectionString;
        }

        /// <summary>
        /// Retrieves the part of the sqlUserInstance.dll from the registry
        /// </summary>
        /// <param name="errorState">In case the dll path is not found, the error is set here.</param>
        /// <returns></returns>
        private string GetUserInstanceDllPath(out LocalDBErrorState errorState)
        {
            using (TrySNIEventScope.Create(nameof(LocalDB)))
            {
                string dllPath = null;
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(LocalDBInstalledVersionRegistryKey))
                {
                    if (key == null)
                    {
                        errorState = LocalDBErrorState.NO_INSTALLATION;
                        SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "No installation found.");
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
                            SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "Invalid Configuration.");
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
                        SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "Invalid Configuration.");
                        return null;
                    }

                    // Use the latest version to get the DLL path
                    using (RegistryKey latestVersionKey = key.OpenSubKey(latestVersion.ToString()))
                    {

                        object instanceAPIPathRegistryObject = latestVersionKey.GetValue(InstanceAPIPathValueName);

                        if (instanceAPIPathRegistryObject == null)
                        {
                            errorState = LocalDBErrorState.NO_SQLUSERINSTANCEDLL_PATH;
                            SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "No SQL user instance DLL. Instance API Path Registry Object Error.");
                            return null;
                        }

                        RegistryValueKind valueKind = latestVersionKey.GetValueKind(InstanceAPIPathValueName);

                        if (valueKind != RegistryValueKind.String)
                        {
                            errorState = LocalDBErrorState.INVALID_SQLUSERINSTANCEDLL_PATH;
                            SqlClientEventSource.Log.TryTraceEvent(nameof(LocalDB), EventType.ERR, "Invalid SQL user instance DLL path. Registry value kind mismatch.");
                            return null;
                        }

                        dllPath = (string)instanceAPIPathRegistryObject;

                        errorState = LocalDBErrorState.NONE;
                        return dllPath;
                    }
                }
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
    }
}
