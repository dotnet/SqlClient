// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    // Implementation of an Enclave provider for Windows Virtual Secure Mode enclaves
    internal class HostGuardianServiceEnclaveProvider : VirtualizationBasedSecurityEnclaveProviderBase
    {
        #region Members

        // HttpClient is intended to be instantiated once per application, rather than per-use.
        // see https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-6.0#remarks
        private static readonly HttpClient s_client = new HttpClient();

        // this is endpoint given to us by HGS team from windows
        private const string AttestationUrlSuffix = @"/v2.0/signingCertificates";

        public uint MaxNumRetries { get; set; }

        private int enclaveRetrySleepInSeconds = 3;

        public int EnclaveRetrySleepInSeconds
        {
            get
            {
                return enclaveRetrySleepInSeconds;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException(Strings.EnclaveRetrySleepInSecondsValueException);
                }

                enclaveRetrySleepInSeconds = value;
            }
        }

        #endregion

        #region Private helpers

        // Return the endpoint for given attestation url
        protected override string GetAttestationUrl(string attestationUrl)
        {
            return attestationUrl.TrimEnd('/') + AttestationUrlSuffix;
        }

        // Makes a web request to the provided url and returns the response as a byte[]
        protected override byte[] MakeRequest(string url)
        {
            Exception exception = null;

            for (int n = 0; n < MaxNumRetries + 1 /* Initial attempt + numRetries */; n++)
            {
                try
                {
                    if (n != 0)
                    {
                        Thread.Sleep(EnclaveRetrySleepInSeconds * 1000);
                    }

                    using (Stream stream = s_client.GetStreamAsync(url).ConfigureAwait(false).GetAwaiter().GetResult())
                    {
                        return JsonSerializer.Deserialize<List<byte>>(stream)?.ToArray();
                    }
                }
                catch (Exception e)
                {
                    exception = e;
                }
            }

            throw SQL.AttestationFailed(string.Format(Strings.GetAttestationSigningCertificateRequestFailedFormat, url, exception.Message), exception);
        }

        #endregion
    }

    #region Models

    // A model class representing the deserialization of the byte payload the client
    // receives from SQL Server while setting up a session.
    internal class AttestationInfo
    {
        public uint TotalSize { get; set; }

        // The enclave's RSA Public Key.
        // Needed to establish trust of the enclave.
        // Used to verify the enclave's DiffieHellman info.
        public EnclavePublicKey Identity { get; set; }

        // The SQL Server host's health report the server received from the attestation service
        // and forwarded to the client.
        // Needed to establish trust of the enclave report received from SQL Server.
        // Used to verify the enclave report's signature.
        public HealthReport HealthReport { get; set; }

        // The enclave report from the SQL Server host's enclave.
        public EnclaveReportPackage EnclaveReportPackage { get; set; }

        // The id of the current session.
        // Needed to set up a secure session between the client and enclave.
        public long SessionId { get; set; }

        // The DiffieHellman public key and signature of SQL Server host's enclave.
        // Needed to set up a secure session between the client and enclave.
        public EnclaveDiffieHellmanInfo EnclaveDHInfo { get; set; }

        public AttestationInfo(byte[] attestationInfo)
        {
            int offset = 0;

            TotalSize = BitConverter.ToUInt32(attestationInfo, offset);
            offset += sizeof(uint);

            int identitySize = BitConverter.ToInt32(attestationInfo, offset);
            offset += sizeof(uint);

            int healthReportSize = BitConverter.ToInt32(attestationInfo, offset);
            offset += sizeof(uint);

            int enclaveReportSize = BitConverter.ToInt32(attestationInfo, offset);
            offset += sizeof(uint);

            byte[] identityBuffer = EnclaveHelpers.TakeBytesAndAdvance(attestationInfo, ref offset, identitySize);
            Identity = new EnclavePublicKey(identityBuffer);

            byte[] healthReportBuffer = EnclaveHelpers.TakeBytesAndAdvance(attestationInfo, ref offset, healthReportSize);
            HealthReport = new HealthReport(healthReportBuffer);

            byte[] enclaveReportBuffer = new byte[enclaveReportSize];
            Buffer.BlockCopy(attestationInfo, offset, enclaveReportBuffer, 0, enclaveReportSize);
            EnclaveReportPackage = new EnclaveReportPackage(enclaveReportBuffer);
            offset += EnclaveReportPackage.GetSizeInPayload();

            uint secureSessionInfoResponseSize = BitConverter.ToUInt32(attestationInfo, offset);
            offset += sizeof(uint);

            SessionId = BitConverter.ToInt64(attestationInfo, offset);
            offset += sizeof(long);

            EnclaveDHInfo = new EnclaveDiffieHellmanInfo(attestationInfo, offset);
            offset += Convert.ToInt32(EnclaveDHInfo.Size);

            Debug.Assert(offset == attestationInfo.Length);
        }
    }

    // A model class to hold the SQL Server's host health report in an X509Certificate2
    internal class HealthReport
    {
        private int Size { get; set; }

        public X509Certificate2 Certificate { get; set; }

        public HealthReport(byte[] payload)
        {
            Size = payload.Length;
#if NET9_0_OR_GREATER
            Certificate = X509CertificateLoader.LoadCertificate(payload);
#else
            Certificate = new X509Certificate2(payload);
#endif
        }

        public int GetSizeInPayload()
        {
            return Size;
        }
    }

    // A managed model representing the output of EnclaveGetAttestationReport
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844233(v=vs.85).aspx
    internal class EnclaveReportPackage
    {
        private int Size { get; set; }

        public EnclaveReportPackageHeader PackageHeader { get; set; }

        public EnclaveReport Report { get; set; }

        public List<EnclaveReportModule> Modules { get; set; }

        public byte[] ReportAsBytes { get; set; }

        public byte[] SignatureBlob { get; set; }

        public EnclaveReportPackage(byte[] payload)
        {
            Size = payload.Length;

            int offset = 0;
            byte[] headerBuffer = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, EnclaveReportPackageHeader.SizeInPayload);
            PackageHeader = new EnclaveReportPackageHeader(headerBuffer);

            byte[] reportBuffer = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, EnclaveReport.SizeInPayload);
            Report = new EnclaveReport(reportBuffer);

            // Modules are not used for anything currently, ignore parsing for now
            //
            // Modules = new List<VSMEnclaveReportModule>();
            // int reportSizeRemaining = Convert.ToInt32(Report.ReportSize) - Report.GetSizeInPayload();
            // while (reportSizeRemaining > 0)
            // {
            //    var module = new VSMEnclaveReportModule(payload.Skip(offset).ToArray());
            //    Modules.Add(module);
            //    reportSizeRemaining -= module.GetSizeInPayload();
            //    offset += module.GetSizeInPayload();
            // }

            // Moving the offset back to the start of the report,
            // we need the report as a byte buffer for signature verification.
            offset = EnclaveReportPackageHeader.SizeInPayload;

            int dataToHashSize = Convert.ToInt32(PackageHeader.SignedStatementSize);
            ReportAsBytes = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, dataToHashSize);

            int signatureSize = Convert.ToInt32(PackageHeader.SignatureSize);
            SignatureBlob = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, signatureSize);
        }

        public int GetSizeInPayload()
        {
            return Size;
        }
    }

    // A managed model of struct VBS_ENCLAVE_REPORT_PKG_HEADER
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844257(v=vs.85).aspx
    internal class EnclaveReportPackageHeader
    {
        public uint PackageSize { get; set; }

        public uint Version { get; set; }

        public uint SignatureScheme { get; set; }

        public uint SignedStatementSize { get; set; }

        public uint SignatureSize { get; set; }

        public uint Reserved { get; set; }

        public EnclaveReportPackageHeader(byte[] payload)
        {
            int offset = 0;
            PackageSize = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            Version = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            SignatureScheme = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            SignedStatementSize = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            SignatureSize = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            Reserved = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);
        }

        public static int SizeInPayload => 6 * sizeof(uint);
    }

    // A managed model of struct VBS_ENCLAVE_REPORT
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844255(v=vs.85).aspx
    internal class EnclaveReport
    {
        private const int EnclaveDataLength = 64;

        public uint ReportSize { get; set; }

        public uint ReportVersion { get; set; }

        public byte[] EnclaveData { get; set; }

        public EnclaveIdentity Identity { get; set; }

        public EnclaveReport(byte[] payload)
        {
            int offset = 0;

            ReportSize = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            ReportVersion = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            EnclaveData = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, EnclaveDataLength);

            Identity = new EnclaveIdentity(EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, EnclaveIdentity.SizeInPayload));
        }

        public static int SizeInPayload => sizeof(uint) * 2 + sizeof(byte) * 64 + EnclaveIdentity.SizeInPayload;
    }

    // A managed model of struct ENCLAVE_IDENTITY
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844239(v=vs.85).aspx
    internal class EnclaveIdentity
    {
        private const int ImageEnclaveLongIdLength = 32;

        private const int ImageEnclaveShortIdLength = 16;

        public byte[] OwnerId;

        public byte[] UniqueId;

        public byte[] AuthorId;

        public byte[] FamilyId;

        public byte[] ImageId;

        public uint EnclaveSvn { get; set; }

        public uint SecureKernelSvn { get; set; }

        public uint PlatformSvn { get; set; }

        public uint Flags { get; set; }

        public uint SigningLevel { get; set; }

        public uint Reserved { get; set; }

        public EnclaveIdentity() { }

        public EnclaveIdentity(byte[] payload)
        {
            int offset = 0;

            OwnerId = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, ImageEnclaveLongIdLength);

            UniqueId = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, ImageEnclaveLongIdLength);

            AuthorId = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, ImageEnclaveLongIdLength);

            FamilyId = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, ImageEnclaveShortIdLength);

            ImageId = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, ImageEnclaveShortIdLength);

            EnclaveSvn = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            SecureKernelSvn = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            PlatformSvn = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            Flags = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            SigningLevel = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            Reserved = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);
        }

        public static int SizeInPayload => sizeof(byte) * ImageEnclaveLongIdLength * 3 + sizeof(byte) * ImageEnclaveShortIdLength * 2 + sizeof(uint) * 6;
        
    }

    // A managed model of struct VBS_ENCLAVE_REPORT_VARDATA_HEADER
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt827065(v=vs.85).aspx
    internal class EnclaveReportModuleHeader
    {
        public uint DataType { get; set; }

        public uint ModuleSize { get; set; }

        public EnclaveReportModuleHeader(byte[] payload)
        {
            int offset = 0;
            DataType = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            ModuleSize = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);
        }

        public static int SizeInPayload => 2 * sizeof(uint);
    }

    // A managed model of struct VBS_ENCLAVE_REPORT_MODULE
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844256(v=vs.85).aspx
    internal class EnclaveReportModule
    {
        private const int ImageEnclaveLongIdLength = 32;

        private const int ImageEnclaveShortIdLength = 16;

        public EnclaveReportModuleHeader Header { get; set; }

        public byte[] UniqueId;

        public byte[] AuthorId;

        public byte[] FamilyId;

        public byte[] ImageId;

        public uint Svn { get; set; }

        public string ModuleName { get; set; }

        public EnclaveReportModule(byte[] payload)
        {
            int offset = 0;
            Header = new EnclaveReportModuleHeader(payload);
            offset += EnclaveReportModuleHeader.SizeInPayload;

            UniqueId = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, ImageEnclaveLongIdLength);

            AuthorId = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, ImageEnclaveLongIdLength);

            FamilyId = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, ImageEnclaveShortIdLength);

            ImageId = EnclaveHelpers.TakeBytesAndAdvance(payload, ref offset, ImageEnclaveShortIdLength);

            Svn = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            ModuleName = BitConverter.ToString(payload, offset, 1);
            offset += sizeof(char) * 1;
        }

        public int GetSizeInPayload()
        {
            return EnclaveReportModuleHeader.SizeInPayload + Convert.ToInt32(Header.ModuleSize);
        }
    }

    // An enum representing the Flags property of ENCLAVE_IDENTITY
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844239(v=vs.85).aspx
    internal enum EnclaveIdentityFlags
    {
        ENCLAVE_FLAG_NONE = 0x00000000,
        ENCLAVE_FLAG_FULL_DEBUG_ENABLED = 0x00000001,
        ENCLAVE_FLAG_DYNAMIC_DEBUG_ENABLED = 0x00000002,
        ENCLAVE_FLAG_DYNAMIC_DEBUG_ACTIVE = 0x00000004
    }

#endregion

    internal static class EnclaveHelpers
    {
        public static byte[] TakeBytesAndAdvance(byte[] input, ref int offset, int count)
        {
            byte[] output = new byte[count];
            Buffer.BlockCopy(input, offset, output, 0, count);
            offset += count;
            return output;
        }
    }
}
