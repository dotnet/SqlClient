// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Data.SqlClient.ManualTesting.Tests.DataCommon;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class CertificateTestWithTdsServer
    {
        private static readonly string s_fullPathToPowershellScript = Path.Combine(Directory.GetCurrentDirectory(), "makepfxcert.ps1");
        private static readonly string s_fullPathToPfx = Path.Combine(Directory.GetCurrentDirectory(), "localhostcert.pfx");

        public CertificateTestWithTdsServer()
        {
            CreatePfxCertificate(s_fullPathToPowershellScript);
        }

        [Theory]
        [MemberData(nameof(ConnectionTestParametersData.GetConnectionTestParameters), MemberType = typeof(ConnectionTestParametersData))]
        public void ConnectionTest(ConnectionTestParameters connectionTestParameters)
        {
            string userId = string.Empty;
            string password = string.Empty;
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            userId = builder.UserID;
            password = builder.Password;

            using TestTdsServer server = TestTdsServer.StartTestServer(enableFedAuth: false, enableLog: false, connectionTimeout: 15,
                methodName: "", new X509Certificate2(s_fullPathToPfx, "nopassword", X509KeyStorageFlags.UserKeySet),
                encryptionType: connectionTestParameters.TdsEncryptionType);

            if (userId != string.Empty)
            {
                builder = new(server.ConnectionString)
                {
                    UserID = userId,
                    Password = password,
                    TrustServerCertificate = connectionTestParameters.TrustServerCertificate,
                    Encrypt = connectionTestParameters.Encrypt,
                };
            }
            else
            {
                builder = new(server.ConnectionString)
                {
                    IntegratedSecurity = true,
                    TrustServerCertificate = connectionTestParameters.TrustServerCertificate,
                    Encrypt = connectionTestParameters.Encrypt,
                };
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && userId == string.Empty)
            {
                builder.IntegratedSecurity = false;
                builder.UserID = "user";
                builder.Password = "password";
            }

            if (connectionTestParameters.Certificate != null)
            {
                builder.ServerCertificate = connectionTestParameters.Certificate;
            }

            if (connectionTestParameters.HostNameInCertificate != null)
            {
                builder.HostNameInCertificate = connectionTestParameters.HostNameInCertificate;
            }

            try
            {
                using SqlConnection connection = new(builder.ConnectionString);
                connection.Open();
                Debug.WriteLine($">>>>>>>>>>>> Connected to {builder.ConnectionString} with encrypt={connectionTestParameters.Encrypt}, trustServerCertificate={connectionTestParameters.TrustServerCertificate} <<<<<<<<<<<<<<<");
                Assert.Equal(connectionTestParameters.TestResult, (connection.State == ConnectionState.Open));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex.Message} {ex.InnerException?.Message}" );
                Debug.WriteLine($">>>>>>>>>>>> Failed to connect using {builder.ConnectionString} with encrypt={connectionTestParameters.Encrypt}, trustServerCertificate={connectionTestParameters.TrustServerCertificate} <<<<<<<<<<<<<<<");
            }
        }

        private static void CreatePfxCertificate(string script)
        {
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
                        Arguments = $"{script} > result.txt",
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
    }
}

