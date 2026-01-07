// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [PlatformSpecific(TestPlatforms.Windows)]
    public class CertificateTest : IDisposable
    {
        #region Private Fields
        private const string IPV4 = @"127.0.0.1";
        private const string IPV6 = @"::1";
        private static readonly string s_fullPathToPowershellScript = Path.Combine(Directory.GetCurrentDirectory(), "SQL", "ConnectionTestWithSSLCert", "GenerateSelfSignedCertificate.ps1");
        private const string LocalHost = "localhost";
        private static readonly string s_fQDN = Dns.GetHostEntry(Environment.MachineName).HostName;
        private readonly string _thumbprint;
        private const string ThumbPrintEnvName = "Thumbprint";

        // InstanceName will get replaced with an instance name in the connection string
        private static string InstanceName = "MSSQLSERVER";
        
        // s_instanceNamePrefix will get replaced with MSSQL$ is there is an instance name in connection string
        private static string InstanceNamePrefix = "";

        // SlashInstance is used to override IPV4 and IPV6 defined about so it includes an instance name
        private static string SlashInstanceName = "";

        private static string ForceEncryptionRegistryPath
        {
            get
            {
                if (DataTestUtility.IsSQL2022())
                {
                    return $@"SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.{InstanceName}\MSSQLSERVER\SuperSocketNetLib";
                }
                if (DataTestUtility.IsSQL2019())
                {
                    return $@"SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL15.{InstanceName}\MSSQLSERVER\SuperSocketNetLib";
                }
                if (DataTestUtility.IsSQL2016())
                {
                    return $@"SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL14.{InstanceName}\MSSQLSERVER\SuperSocketNetLib";
                }
                return string.Empty;
            }
        }
        #endregion

        public CertificateTest()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out _, out string instanceName));
            if (!LocalHost.Equals(hostname, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.IsNullOrEmpty(instanceName))
            {
                InstanceName = instanceName;
                InstanceNamePrefix = "MSSQL$";
                SlashInstanceName = $"\\{InstanceName}";
            }

            if (IsAdmin())
            {
                CreateValidCertificate(s_fullPathToPowershellScript);
                _thumbprint = Environment.GetEnvironmentVariable(ThumbPrintEnvName, EnvironmentVariableTarget.Machine);
            }
        }

        private static bool IsLocalHost()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out _, out _));
            return LocalHost.Equals(hostname, StringComparison.OrdinalIgnoreCase);
        }

        private static bool AreConnStringsSetup() => DataTestUtility.AreConnStringsSetup();
        private static bool IsAdmin() => DataTestUtility.IsAdmin;
        private static bool IsNotAzureServer() => DataTestUtility.IsNotAzureServer();
        private static bool UseManagedSNIOnWindows() => DataTestUtility.UseManagedSNIOnWindows;

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsNotAzureServer), nameof(IsLocalHost), nameof(IsAdmin))]
        public void OpeningConnectionWithGoodCertificateTest()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);

            // confirm that ForceEncryption is enabled
            builder.Encrypt = SqlConnectionEncryptOption.Optional;
            using SqlConnection notEncryptedConnection = new(builder.ConnectionString);
            notEncryptedConnection.Open();
            Assert.Equal(ConnectionState.Open, notEncryptedConnection.State);

            // Test with Mandatory encryption
            builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
            using SqlConnection mandatoryConnection = new(builder.ConnectionString);
            mandatoryConnection.Open();
            Assert.Equal(ConnectionState.Open, mandatoryConnection.State);
            if (DataTestUtility.IsTDS8Supported)
            {
                // Test with strict encryption
                builder.Encrypt = SqlConnectionEncryptOption.Strict;
                using SqlConnection strictConnection = new(builder.ConnectionString);
                strictConnection.Open();
                Assert.Equal(ConnectionState.Open, strictConnection.State);
            }
        }

        // Provided hostname in certificate are:
        // localhost, FQDN, Loopback IPv4: 127.0.0.1, IPv6: ::1
        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsNotAzureServer), nameof(IsLocalHost), nameof(IsAdmin))]
        public void OpeningConnectionWithNicTest()
        {
            // Mandatory
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                // 127.0.0.1 most of the cases does not cause any Remote certificate validation error depending on name resolution on the machine
                //  It mostly returns SslPolicyErrors.None
                //DataSource = IPV4,
                DataSource = IPV4 + SlashInstanceName,
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                HostNameInCertificate = LocalHost
                //HostNameInCertificate = Dns.GetHostEntry(Environment.MachineName).HostName
            };
            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();
            Assert.Equal(ConnectionState.Open, connection.State);

            // Ipv6 however causes name mistmatch error
            // In net6 Manged SNI does not check for SAN. Therefore, Application using Net6 have to use FQDN as HNIC
            // According to above no other hostname in certificate than FQDN will work in net6 which is same as SubjectName in case of RemoteCertificateNameMismatch
            // Net7.0 the new API added by dotnet runtime will check SANS and then SubjectName

            builder.DataSource = IPV6 + SlashInstanceName;
            builder.HostNameInCertificate = Dns.GetHostEntry(Environment.MachineName).HostName;
            builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
            using SqlConnection connection2 = new(builder.ConnectionString);
            connection2.Open();
            Assert.Equal(ConnectionState.Open, connection2.State);

            if (DataTestUtility.IsTDS8Supported)
            {
                // Strict
                builder.DataSource = IPV6 + SlashInstanceName;
                builder.HostNameInCertificate = Dns.GetHostEntry(Environment.MachineName).HostName;
                builder.Encrypt = SqlConnectionEncryptOption.Strict;
                using SqlConnection connection3 = new(builder.ConnectionString);
                connection3.Open();
                Assert.Equal(ConnectionState.Open, connection3.State);
            }
        }

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(UseManagedSNIOnWindows), nameof(IsNotAzureServer), nameof(IsLocalHost), nameof(IsAdmin))]
        public void RemoteCertificateNameMismatchErrorTest()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetLocalIpAddress(),
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = false,
                HostNameInCertificate = "BadHostName"
            };
            using SqlConnection connection = new(builder.ConnectionString);
            SqlException exception = Assert.Throws<SqlException>(() => connection.Open());
            Assert.StartsWith("A connection was successfully established with the server, but then an error occurred during the pre-login handshake. (provider: TCP Provider, error: 35 - An internal exception was caught)", exception.Message);
            Assert.Equal(20, exception.Class);
            Assert.IsType<AuthenticationException>(exception.InnerException);
            Assert.StartsWith("Certificate name mismatch. The provided 'DataSource' or 'HostNameInCertificate' does not match the name in the certificate.", exception.InnerException.Message);
        }

        private static void CreateValidCertificate(string script)
        {
            if (File.Exists(script))
            {
                StringBuilder output = new();
                Process proc = new()
                {
                    StartInfo =
                {
                    FileName = "powershell.exe",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    Arguments = $"{script} -Prefix {InstanceNamePrefix} -Instance {InstanceName}",
                    CreateNoWindow = false,
                    Verb = "runas"
                }
                };

                proc.EnableRaisingEvents = true;

                // Use async event handlers to avoid deadlocks
                proc.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    output.AppendLine(e.Data);
                });

                proc.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    output.AppendLine(e.Data);
                });

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                Console.WriteLine(output);
                if (!proc.WaitForExit(60000))
                {
                    proc.Kill();
                    // allow async output to process
                    proc.WaitForExit(2000);
                    throw new Exception($"Could not generate certificate.Error out put: {output}");
                }
            }
            else
            {
                throw new Exception($"Could not find GenerateSelfSignedCertificate.ps1");
            }
        }

        private static string GetLocalIpAddress()
        {
            string hostname = Dns.GetHostEntry(Environment.MachineName).HostName;
            IPHostEntry iphostentry = Dns.GetHostEntry(hostname);
            List<IPAddress> ipaddress = iphostentry.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6).ToList();
            foreach (IPAddress ip in ipaddress)
            {
                Console.WriteLine($"{ip}");
                if (ip != IPAddress.IPv6Loopback)
                {
                    Console.WriteLine(ip);
                    return ip.ToString();
                }
            }
            return null;
        }

        private void RemoveCertificate()
        {
            using X509Store certStore = new(StoreName.Root, StoreLocation.LocalMachine);
            certStore.Open(OpenFlags.ReadWrite);
            X509Certificate2Collection certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, _thumbprint, false);
            if (certCollection.Count > 0)
            {
                certStore.Remove(certCollection[0]);
            }
            certStore.Close();
        }

        private static void RemoveForceEncryptionFromRegistryPath(string registryPath)
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath, true);
            key?.SetValue("ForceEncryption", 0, RegistryValueKind.DWord);
            key?.SetValue("Certificate", "", RegistryValueKind.String);
            ServiceController sc = new($"{InstanceNamePrefix}{InstanceName}");
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped);
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running);
        }

        private static void RemoveEnvironmentVariable(string variableName)
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !string.IsNullOrEmpty(ForceEncryptionRegistryPath))
            {
                RemoveCertificate();
                RemoveForceEncryptionFromRegistryPath(ForceEncryptionRegistryPath);
                RemoveEnvironmentVariable(ThumbPrintEnvName);
            }
        }
    }
}
