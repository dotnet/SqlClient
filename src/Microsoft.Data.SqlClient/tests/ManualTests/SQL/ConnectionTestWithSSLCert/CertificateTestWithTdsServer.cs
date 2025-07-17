// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using Microsoft.Data.SqlClient.ManualTesting.Tests.DataCommon;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.Win32;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class CertificateTestWithTdsServer : IDisposable
    {
        private static readonly string s_fullPathToPowershellScript = Path.Combine(Directory.GetCurrentDirectory(), "makepfxcert.ps1");
        private static readonly string s_fullPathToCleanupPowershellScript = Path.Combine(Directory.GetCurrentDirectory(), "removecert.ps1");
        private static readonly string s_fullPathToPfx = Path.Combine(Directory.GetCurrentDirectory(), "localhostcert.pfx");
        private static readonly string s_fullPathTothumbprint = Path.Combine(Directory.GetCurrentDirectory(), "thumbprint.txt");
        private static readonly string s_fullPathToClientCert = Path.Combine(Directory.GetCurrentDirectory(), "clientcert");
        private static bool s_windowsAdmin = true;
        private static string s_instanceName = "MSSQLSERVER";
        // s_instanceNamePrefix will get replaced with MSSQL$ is there is an instance name in the connection string
        private static string s_instanceNamePrefix = "";
        private const string LocalHost = "localhost";

        public CertificateTestWithTdsServer()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out _, out string instanceName));

            if (!string.IsNullOrEmpty(instanceName))
            {
                s_instanceName = instanceName;
                s_instanceNamePrefix = "MSSQL$";
            }

            // Confirm that user has elevated access on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    s_windowsAdmin = true;
                else
                    s_windowsAdmin = false;
            }

            if (!Directory.Exists(s_fullPathToClientCert))
            {
                Directory.CreateDirectory(s_fullPathToClientCert);
            }

            RunPowershellScript(s_fullPathToPowershellScript);
        }

        private static bool IsLocalHost()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out _, out _));
            return LocalHost.Equals(hostname, StringComparison.OrdinalIgnoreCase);
        }

        private static bool AreConnStringsSetup() => DataTestUtility.AreConnStringsSetup();
        private static bool IsNotAzureServer() => DataTestUtility.IsNotAzureServer();
        private static bool UseManagedSNIOnWindows() => DataTestUtility.UseManagedSNIOnWindows;

        private static string ForceEncryptionRegistryPath
        {
            get
            {
                if (DataTestUtility.IsSQL2022())
                {
                    return $@"SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.{s_instanceName}\MSSQLSERVER\SuperSocketNetLib";
                }
                if (DataTestUtility.IsSQL2019())
                {
                    return $@"SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL15.{s_instanceName}\MSSQLSERVER\SuperSocketNetLib";
                }
                if (DataTestUtility.IsSQL2016())
                {
                    return $@"SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL14.{s_instanceName}\MSSQLSERVER\SuperSocketNetLib";
                }
                return string.Empty;
            }
        }

        [ConditionalTheory(nameof(AreConnStringsSetup), nameof(IsNotAzureServer), nameof(IsLocalHost))]
        [MemberData(
            nameof(ConnectionTestParametersData.GetConnectionTestParameters),
            MemberType = typeof(ConnectionTestParametersData),
            // xUnit can't consistently serialize the data for this test, so we
            // disable enumeration of the test data to avoid warnings on the
            // console.
            DisableDiscoveryEnumeration = true)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void BeginWindowsConnectionTest(ConnectionTestParameters connectionTestParameters)
        {
            if (!s_windowsAdmin)
            {
                Assert.Fail("User needs to have elevated access for these set of tests");
            }

            ConnectionTest(connectionTestParameters);
        }

        [ConditionalTheory(nameof(AreConnStringsSetup), nameof(IsNotAzureServer), nameof(IsLocalHost))]
        [MemberData(
            nameof(ConnectionTestParametersData.GetConnectionTestParameters),
            MemberType = typeof(ConnectionTestParametersData),
            DisableDiscoveryEnumeration = true)]
        [PlatformSpecific(TestPlatforms.Linux)]
        public void BeginLinuxConnectionTest(ConnectionTestParameters connectionTestParameters)
        {
            ConnectionTest(connectionTestParameters);
        }

        private void ConnectionTest(ConnectionTestParameters connectionTestParameters)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);

            // The TestTdsServer does not validate the user name and password, so we can use any value if they are not defined.
            string userId = string.IsNullOrWhiteSpace(builder.UserID) ? "user" : builder.UserID;
            string password = string.IsNullOrWhiteSpace(builder.Password) ? "password" : builder.Password;

            using GenericTDSServer server = new GenericTDSServer(new TDSServerArguments
            {
                #if NET9_0_OR_GREATER
                EncryptionCertificate = X509CertificateLoader.LoadPkcs12FromFile(s_fullPathToPfx, "nopassword", X509KeyStorageFlags.UserKeySet),
                #else
                EncryptionCertificate = new X509Certificate2(s_fullPathToPfx, "nopassword", X509KeyStorageFlags.UserKeySet),
                #endif
                EncryptionProtocols = connectionTestParameters.EncryptionProtocols,
                Encryption = connectionTestParameters.TdsEncryptionType,
            });

            server.Start();

            builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                ConnectTimeout = 15,
                UserID = userId,
                Password = password,
                TrustServerCertificate = connectionTestParameters.TrustServerCertificate,
                Encrypt = connectionTestParameters.Encrypt,
            };

            if (!string.IsNullOrEmpty(connectionTestParameters.Certificate))
            {
                builder.ServerCertificate = connectionTestParameters.Certificate;
            }

            if (!string.IsNullOrEmpty(connectionTestParameters.HostNameInCertificate))
            {
                builder.HostNameInCertificate = connectionTestParameters.HostNameInCertificate;
            }

            using SqlConnection connection = new(builder.ConnectionString);
            try
            {
                connection.Open();
                Assert.Equal(connectionTestParameters.TestResult, (connection.State == ConnectionState.Open));
            }
            catch (Exception)
            {
                Assert.False(connectionTestParameters.TestResult);
            }
        }

        private static void RunPowershellScript(string script)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string powerShellCommand = "powershell.exe";
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                powerShellCommand = "pwsh";
            }

            if (File.Exists(script))
            {
                StringBuilder output = new();
                Process proc = new()
                {
                    StartInfo =
                    {
                        FileName = powerShellCommand,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        Arguments = $"{script} -OutDir {currentDirectory} > result.txt",
                        CreateNoWindow = false,
                        Verb = "runas"
                    }
                };

                proc.EnableRaisingEvents = true;

                proc.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                });

                proc.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                });

                proc.Start();

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                if (!proc.WaitForExit(60000))
                {
                    proc.Kill();
                    proc.WaitForExit(2000);
                    throw new Exception($"Could not generate certificate. Error output: {output}");
                }
            }
            else
            {
                throw new Exception($"Could not find makepfxcert.ps1");
            }
        }

        private void RemoveCertificate()
        {
            string thumbprint = File.ReadAllText(s_fullPathTothumbprint);
            using X509Store certStore = new(StoreName.Root, StoreLocation.LocalMachine);
            certStore.Open(OpenFlags.ReadWrite);
            X509Certificate2Collection certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if (certCollection.Count > 0)
            {
                certStore.Remove(certCollection[0]);
            }
            certStore.Close();

            File.Delete(s_fullPathTothumbprint);
            Directory.Delete(s_fullPathToClientCert, true);
        }

        private static void RemoveForceEncryptionFromRegistryPath(string registryPath)
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath, true);
            key?.SetValue("ForceEncryption", 0, RegistryValueKind.DWord);
            key?.SetValue("Certificate", "", RegistryValueKind.String);
            ServiceController sc = new($"{s_instanceNamePrefix}{s_instanceName}");
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped);
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (disposing && !string.IsNullOrEmpty(s_fullPathTothumbprint))
                {
                    RemoveCertificate();
                    RemoveForceEncryptionFromRegistryPath(ForceEncryptionRegistryPath);
                }
            }
            else
            {
                RunPowershellScript(s_fullPathToCleanupPowershellScript);
            }
        }
    }
}
