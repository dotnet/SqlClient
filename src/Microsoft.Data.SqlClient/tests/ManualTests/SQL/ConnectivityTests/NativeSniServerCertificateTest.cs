// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Live checks that Native SNI reports the certificate the server presented during the TLS
    /// handshake through <see cref="SqlConnectionEncryptionInfo.GetServerCertificate"/>.
    /// </summary>
    public static class NativeSniServerCertificateTest
    {
        private const string ServerCertificateExport = "SNIGetServerCertificateWrapper";

        public static bool CanRun =>
            DataTestUtility.IsTCPConnStringSetup() &&
            DataTestUtility.IsUsingNativeSNI() &&
            DataTestUtility.TcpConnectionStringDoesNotUseAadAuth;

        [ConditionalTheory(typeof(NativeSniServerCertificateTest), nameof(CanRun))]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task Open_EncryptedConnection_ReportsPresentedServerCertificate(bool async)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = true,
                Pooling = false,
            };

            using SqlConnection connection = new(builder.ConnectionString);
            if (async)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }

            SqlConnectionEncryptionInfo info = connection.EncryptionInfo;
            Assert.NotNull(info.NegotiatedEncryptionProtocol);

            using X509Certificate2? certificate = info.GetServerCertificate();

            // The certificate is reported only by an sni.dll that has the certificate export.
            // Older sni.dll versions keep the connection working and leave the certificate null.
            if (!IsServerCertificateExportAvailable())
            {
                Assert.Null(certificate);
                return;
            }

            Assert.NotNull(certificate);
            Assert.NotEmpty(certificate.RawData);
            Assert.False(certificate.HasPrivateKey);

            // Each call returns an independent instance with the same encoding.
            using X509Certificate2? second = info.GetServerCertificate();
            Assert.NotNull(second);
            Assert.NotSame(certificate, second);
            Assert.Equal(certificate.RawData, second.RawData);
        }

        private static bool IsServerCertificateExportAvailable()
        {
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                string name = Path.GetFileName(module.FileName);
                if (name.StartsWith("Microsoft.Data.SqlClient.SNI", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    return GetProcAddress(module.BaseAddress, ServerCertificateExport) != IntPtr.Zero;
                }
            }

            return false;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
