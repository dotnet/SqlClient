// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
#nullable enable

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

            // The certificate must remain untrusted for the negative validation cases.
            // Clean up partial setup because xUnit does not call Dispose when construction fails.
            try
            {
                RunPowershellScript(s_fullPathToPowershellScript, Directory.GetCurrentDirectory());
            }
            catch (Exception setupException)
            {
                try
                {
                    RemoveCertificate();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("Certificate setup and cleanup failed.", setupException, cleanupException);
                }
                throw;
            }
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

            using X509Certificate2 certificate = GetEncryptionCertificate(s_fullPathToPfx, "nopassword", X509KeyStorageFlags.UserKeySet);
            using (X509Chain chain = new())
            {
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                Assert.False(chain.Build(certificate), "The simulated server certificate must remain untrusted.");
                Assert.Contains(chain.ChainStatus, status => (status.Status & X509ChainStatusFlags.UntrustedRoot) != 0);
            }

            using TdsServer server = new TdsServer(new TdsServerArguments
            {
                EncryptionCertificate = certificate,
                EncryptionProtocols = connectionTestParameters.EncryptionProtocols,
                Encryption = connectionTestParameters.TdsEncryptionType,
                ExpectTlsHandshakeFailure = !connectionTestParameters.TestResult,
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
            if (connectionTestParameters.TestResult)
            {
                connection.Open();
                Assert.Equal(ConnectionState.Open, connection.State);
            }
            else
            {
                Assert.Throws<SqlException>(() => connection.Open());
            }
        }

        /// <summary>Runs a certificate script without changing trust stores and reports failed exits.</summary>
        /// <param name="script">The certificate script to execute.</param>
        /// <param name="currentDirectory">The directory containing scenario-owned certificate files.</param>
        internal static void RunPowershellScript(string script, string currentDirectory)
        {
            string powerShellCommand =
              RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
              ? "powershell.exe"
              : "pwsh";

            if (! File.Exists(script))
            {
                throw new Exception($"Script {script} does not exist");
            }

            StringBuilder output = new();
            using Process proc = new()
            {
                StartInfo =
                {
                    FileName = powerShellCommand,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    Arguments = $"-NoProfile -NonInteractive -File \"{script}\" -OutDir \"{currentDirectory}\" -SkipTrust",
                    CreateNoWindow = false,
                    Verb = "runas"
                }
            };

            proc.EnableRaisingEvents = true;

            proc.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (output)
                    {
                        output.AppendLine($"[OUT] {e.Data}");
                    }
                }
            });

            proc.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (output)
                    {
                        output.AppendLine($"[ERR] {e.Data}");
                    }
                }
            });

            proc.Start();

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            if (!proc.WaitForExit(60000))
            {
                proc.Kill();
                proc.WaitForExit(2000);
                throw new Exception($"Could not generate certificate; script output: {output}");
            }
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException($"Certificate script exited with code {proc.ExitCode}; script output: {output}");
            }
        }

        /// <summary>
        /// Loads the specified certificate.
        /// </summary>
        /// <param name="fileName">The full path of the certificate.</param>
        /// <param name="password">The certificate's password.</param>
        /// <param name="keyStorageFlags">Key storage flags to apply when loading the certificate</param>
        /// <returns>An <see cref="X509Certificate2"/> instance.</returns>
        private X509Certificate2 GetEncryptionCertificate(string fileName, string? password, X509KeyStorageFlags keyStorageFlags)
        {
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12FromFile(fileName, password, keyStorageFlags);
#else
            return new X509Certificate2(fileName, password, keyStorageFlags);
#endif
        }

        private void RemoveCertificate()
        {
            RemoveCertificate(Directory.GetCurrentDirectory(), () =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(s_fullPathTothumbprint))
                {
                    string thumbprint = File.ReadAllText(s_fullPathTothumbprint);
                    using X509Store certStore = new(StoreName.My, StoreLocation.LocalMachine);
                    certStore.Open(OpenFlags.ReadWrite);
                    X509Certificate2Collection certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                    if (certCollection.Count > 0)
                    {
                        certStore.Remove(certCollection[0]);
                    }
                    certStore.Close();
                }
            });
        }

        /// <summary>Attempts all owned file cleanup and retains ownership if store removal fails.</summary>
        /// <param name="directory">The directory containing scenario-owned certificate files.</param>
        /// <param name="removeFromStore">Removes the owned certificate without changing SQL Server state.</param>
        internal static void RemoveCertificate(string directory, Action removeFromStore)
        {
            List<Exception> errors = new();
            bool removedFromStore = false;
            try
            {
                removeFromStore();
                removedFromStore = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(CertificateTestWithTdsServer)}: failed to remove certificate: {ex.Message}");
                errors.Add(ex);
            }

            if (removedFromStore)
            {
                TryCleanup(() => File.Delete(Path.Combine(directory, "thumbprint.txt")), errors);
            }
            foreach (string extension in new[] { "pfx", "key", "cer", "pem" })
            {
                TryCleanup(() => File.Delete(Path.Combine(directory, $"localhostcert.{extension}")), errors);
            }
            TryCleanup(() =>
            {
                string clientCertificates = Path.Combine(directory, "clientcert");
                if (Directory.Exists(clientCertificates))
                {
                    Directory.Delete(clientCertificates, true);
                }
            }, errors);
            if (errors.Count > 0)
            {
                throw new AggregateException("Certificate cleanup failed.", errors);
            }
        }

        private static void TryCleanup(Action cleanupAction, List<Exception> errors)
        {
            try
            {
                cleanupAction();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(CertificateTestWithTdsServer)}: cleanup step failed: {ex.Message}");
                errors.Add(ex);
            }
        }

        private static void RemoveForceEncryptionFromRegistryPath(string registryPath)
        {
            RegistryKey? key = Registry.LocalMachine.OpenSubKey(registryPath, true);
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
                RunPowershellScript(s_fullPathToCleanupPowershellScript, Directory.GetCurrentDirectory());
            }
        }
    }

    /// <summary>Verifies certificate-script failures without constructing a SQL Server fixture.</summary>
    public class CertificateScriptTests
    {
        /// <summary>Cleanup preserves store/file failures and ownership while attempting remaining files.</summary>
        [Fact]
        public void RemoveCertificate_StoreAndFileFailures_PreservesErrorsAndOwnership()
        {
            // Arrange
            string directory = Path.Combine(Path.GetTempPath(), $"{nameof(CertificateScriptTests)}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            IOException storeFailure = new("Intentional certificate-store failure.");
            try
            {
                string thumbprint = Path.Combine(directory, "thumbprint.txt");
                string certificate = Path.Combine(directory, "localhostcert.cer");
                File.WriteAllText(thumbprint, "fixture-owned");
                File.WriteAllText(certificate, "fixture-owned");
                Directory.CreateDirectory(Path.Combine(directory, "localhostcert.pfx"));

                // Act
                AggregateException error = Assert.Throws<AggregateException>(() =>
                    CertificateTestWithTdsServer.RemoveCertificate(directory, () => throw storeFailure));

                // Assert
                Assert.Contains(storeFailure, error.Flatten().InnerExceptions);
                Assert.Equal(2, error.Flatten().InnerExceptions.Count);
                Assert.True(File.Exists(thumbprint));
                Assert.False(File.Exists(certificate));
            }
            finally
            {
                // Cleanup
                Directory.Delete(directory, true);
            }
        }

#if NET
        /// <summary>A native generation failure must stop setup without producing a stale certificate.</summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Linux)]
        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        public void RunPowershellScript_NativeFailure_ReportsFailure()
        {
            // Arrange
            string directory = Path.Combine(Path.GetTempPath(), $"{nameof(CertificateScriptTests)}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                string nativeCommand = Path.Combine(directory, "openssl");
                File.WriteAllText(nativeCommand, "#!/bin/sh\nexit 42\n");
                File.SetUnixFileMode(nativeCommand, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                string certificateScript = Path.Combine(AppContext.BaseDirectory, "makepfxcert.ps1").Replace("'", "''");
                string script = Path.Combine(directory, "native-failure.ps1");
                File.WriteAllText(script,
                    "param($OutDir, [switch] $SkipTrust)\n$ErrorActionPreference = 'Stop'\n" +
                    "$env:PATH = $PSScriptRoot + [IO.Path]::PathSeparator + $env:PATH\n" +
                    "$env:computerName = [Environment]::MachineName\n" +
                    $"& '{certificateScript}' -OutDir $OutDir -SkipTrust\n");
                // Act
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                    () => CertificateTestWithTdsServer.RunPowershellScript(script, directory));
                // Assert
                Assert.Contains("code 1", error.Message);
                Assert.Contains("exited with code 42", error.Message);
                Assert.False(File.Exists(Path.Combine(directory, "localhostcert.pfx")));
            }
            finally
            {
                // Cleanup
                Directory.Delete(directory, true);
            }
        }
#endif

        /// <summary>Nonzero script exits must preserve diagnostics and fail certificate setup.</summary>
        [Theory]
        [InlineData("exit 23", 23)]
        [InlineData("throw 'Intentional script failure.'", 1)]
        public void RunPowershellScript_UnsuccessfulExit_ReportsFailure(string command, int exitCode)
        {
            // Arrange
            string directory = Path.Combine(Path.GetTempPath(), $"{nameof(CertificateScriptTests)}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                string script = Path.Combine(directory, "failure.ps1");
                File.WriteAllText(script, "param($OutDir, [switch] $SkipTrust)\nWrite-Output 'Intentional script output.'\n" + command);
                // Act
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                    () => CertificateTestWithTdsServer.RunPowershellScript(script, directory));
                // Assert
                Assert.Contains($"code {exitCode}", error.Message);
                Assert.Contains("Intentional script output.", error.Message);
            }
            finally
            {
                // Cleanup
                Directory.Delete(directory, true);
            }
        }
    }
}
