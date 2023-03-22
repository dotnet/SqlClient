// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography.X509Certificates;
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
                        int result = DecodeUtf8JsonBytes(stream, out byte[] bytes);
                        if (result == 0)
                        {
                            return bytes;
                        }
                        else
                        {
                            throw new InvalidOperationException("failed to decode json with result code " + result.ToString());
                        }
                    }
                }
                catch (Exception e)
                {
                    exception = e;
                }
            }

            throw SQL.AttestationFailed(string.Format(Strings.GetAttestationSigningCertificateRequestFailedFormat, url, exception.Message), exception);
        }

        private static int DecodeUtf8JsonBytes(Stream input, out byte[] bytes)
        {
            bytes = null;
            int code = 0;
            Span<byte> digits = stackalloc byte[3];
            State state = State.Start;
            int digitCount = 0;

            using (MemoryStream output = new MemoryStream())
            {
                int read = input.ReadByte();

                while (state != State.Failed && read != -1)
                {
                    byte current = (byte)read;

                    switch (state)
                    {
                        case State.Start:
                            if (current == (byte)'[')
                            {
                                state = State.ArrayStart;
                            }
                            else
                            {
                                code = 1; // invalid data prefix
                                state = State.Failed;
                            }
                            break;

                        case State.ArrayStart:
                            if (current >= 48 && current <= 57)
                            {
                                digits[0] = (byte)(read - 48);
                                digitCount = 1;
                                state = State.Digit;
                            }
                            else
                            {
                                code = 2; // invalid array start suffix
                                state = State.Failed;
                            }
                            break;

                        case State.Digit:
                            if (current >= 48 && current <= 57) // allow zero
                            {
                                switch (digitCount)
                                {
                                    case 3:
                                        code = 3; // too many digits
                                        state = State.Failed;
                                        break;
                                    case 1:
                                        if (digits[0] == 0)
                                        {
                                            code = 4; // leading zero in multi digit
                                            state = State.Failed;
                                            break;
                                        }
                                        else
                                        {
                                            goto default;
                                        }
                                    default:
                                        digits[digitCount] = (byte)(read - 48);
                                        digitCount += 1;
                                        break;

                                }
                            }
                            else if (current == (byte)',')
                            {
                                if (digitCount > 0)
                                {
                                    int pushCode = PushDigits(output, digits.Slice(0, digitCount));
                                    if (pushCode == 0)
                                    {
                                        digitCount = 0;
                                        state = State.Comma;
                                    }
                                    else
                                    {
                                        code = pushCode;
                                        state = State.Failed;
                                    }
                                }
                                else
                                {
                                    code = 5; // comma suffix comma
                                    state = State.Failed;
                                }
                            }
                            else if (current == (byte)']')
                            {
                                if (digitCount > 0)
                                {
                                    int pushCode = PushDigits(output, digits.Slice(0, digitCount));
                                    if (pushCode == 0)
                                    {
                                        digitCount = 0;
                                    }
                                    else
                                    {
                                        code = pushCode;
                                        state = State.Failed;
                                    }
                                }
                                state = State.ArrayEnd;
                            }
                            else
                            {
                                code = 6; // invalid character in digits
                                state = State.Failed;
                            }
                            break;

                        case State.Comma:
                            if (current >= 48 && current <= 57)
                            {
                                digits[0] = (byte)(read - 48);
                                digitCount = 1;
                                state = State.Digit;
                            }
                            else
                            {
                                code = 7; // invalid comma suffix
                                state = State.Failed;
                            }
                            break;

                        case State.ArrayEnd:
                            code = 8; // invalid array end suffix
                            state = State.Failed;
                            break;

                    }

                    read = input.ReadByte();
                }

                if (state == State.ArrayEnd)
                {
                    bytes = output.ToArray();
                }
            }

            return code;
        }

        private static int PushDigits(MemoryStream output, ReadOnlySpan<byte> digits)
        {
            int code = 0;
            int temp = 0;
            int index = 0;
            switch (digits.Length)
            {
                case 3:
                    temp += (digits[index]) * 100;
                    index += 1;
                    goto case 2;
                case 2:
                    temp += (digits[index]) * 10;
                    index += 1;
                    goto default;
                default:
                    temp += digits[index];
                    break;
            }
            if (temp <= 255)
            {
                output.WriteByte((byte)temp);
            }
            else
            {
                code = 9; // value is too large
            }
            return code;
        }

        private enum State
        {
            Start,
            ArrayStart,
            Digit,
            Comma,
            ArrayEnd,
            End,

            Failed
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

            byte[] identityBuffer = attestationInfo.Skip(offset).Take(identitySize).ToArray();
            Identity = new EnclavePublicKey(identityBuffer);
            offset += identitySize;

            byte[] healthReportBuffer = attestationInfo.Skip(offset).Take(healthReportSize).ToArray();
            HealthReport = new HealthReport(healthReportBuffer);
            offset += healthReportSize;

            byte[] enclaveReportBuffer = attestationInfo.Skip(offset).Take(enclaveReportSize).ToArray();
            EnclaveReportPackage = new EnclaveReportPackage(enclaveReportBuffer);
            offset += EnclaveReportPackage.GetSizeInPayload();

            uint secureSessionInfoResponseSize = BitConverter.ToUInt32(attestationInfo, offset);
            offset += sizeof(uint);

            SessionId = BitConverter.ToInt64(attestationInfo, offset);
            offset += sizeof(long);

            int secureSessionBufferSize = Convert.ToInt32(secureSessionInfoResponseSize) - sizeof(uint);
            byte[] secureSessionBuffer = attestationInfo.Skip(offset).Take(secureSessionBufferSize).ToArray();
            EnclaveDHInfo = new EnclaveDiffieHellmanInfo(secureSessionBuffer);
            offset += Convert.ToInt32(EnclaveDHInfo.Size);
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
            Certificate = new X509Certificate2(payload);
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
            PackageHeader = new EnclaveReportPackageHeader(payload.Skip(offset).ToArray());
            offset += PackageHeader.GetSizeInPayload();

            Report = new EnclaveReport(payload.Skip(offset).ToArray());
            offset += Report.GetSizeInPayload();

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
            offset = PackageHeader.GetSizeInPayload();
            int dataToHashSize = Convert.ToInt32(PackageHeader.SignedStatementSize);
            ReportAsBytes = payload.Skip(offset).Take(dataToHashSize).ToArray();
            offset += dataToHashSize;

            int signatureSize = Convert.ToInt32(PackageHeader.SignatureSize);
            SignatureBlob = payload.Skip(offset).Take(signatureSize).ToArray();
            offset += signatureSize;
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

        public int GetSizeInPayload()
        {
            return 6 * sizeof(uint);
        }
    }

    // A managed model of struct VBS_ENCLAVE_REPORT
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844255(v=vs.85).aspx
    internal class EnclaveReport
    {
        private int Size { get; set; }

        public uint ReportSize { get; set; }

        public uint ReportVersion { get; set; }

        public byte[] EnclaveData { get; set; }

        private const int EnclaveDataLength = 64;

        public EnclaveIdentity Identity { get; set; }

        public EnclaveReport(byte[] payload)
        {
            Size = payload.Length;

            int offset = 0;

            ReportSize = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            ReportVersion = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            EnclaveData = payload.Skip(offset).Take(EnclaveDataLength).ToArray();
            offset += EnclaveDataLength;

            Identity = new EnclaveIdentity(payload.Skip(offset).ToArray());
            offset += Identity.GetSizeInPayload();
        }

        public int GetSizeInPayload()
        {
            return sizeof(uint) * 2 + sizeof(byte) * 64 + Identity.GetSizeInPayload();
        }
    }

    // A managed model of struct ENCLAVE_IDENTITY
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844239(v=vs.85).aspx
    internal class EnclaveIdentity
    {
        private int Size { get; set; }

        private static readonly int ImageEnclaveLongIdLength = 32;

        private static readonly int ImageEnclaveShortIdLength = 16;

        public byte[] OwnerId = new byte[ImageEnclaveLongIdLength];

        public byte[] UniqueId = new byte[ImageEnclaveLongIdLength];

        public byte[] AuthorId = new byte[ImageEnclaveLongIdLength];

        public byte[] FamilyId = new byte[ImageEnclaveShortIdLength];

        public byte[] ImageId = new byte[ImageEnclaveShortIdLength];

        public uint EnclaveSvn { get; set; }

        public uint SecureKernelSvn { get; set; }

        public uint PlatformSvn { get; set; }

        public uint Flags { get; set; }

        public uint SigningLevel { get; set; }

        public uint Reserved { get; set; }

        public EnclaveIdentity() { }

        public EnclaveIdentity(byte[] payload)
        {
            Size = payload.Length;

            int offset = 0;

            int ownerIdLength = ImageEnclaveLongIdLength;
            OwnerId = payload.Skip(offset).Take(ownerIdLength).ToArray();
            offset += ownerIdLength;

            int uniqueIdLength = ImageEnclaveLongIdLength;
            UniqueId = payload.Skip(offset).Take(uniqueIdLength).ToArray();
            offset += uniqueIdLength;

            int authorIdLength = ImageEnclaveLongIdLength;
            AuthorId = payload.Skip(offset).Take(authorIdLength).ToArray();
            offset += authorIdLength;

            int familyIdLength = ImageEnclaveShortIdLength;
            FamilyId = payload.Skip(offset).Take(familyIdLength).ToArray();
            offset += familyIdLength;

            int imageIdLength = ImageEnclaveShortIdLength;
            ImageId = payload.Skip(offset).Take(imageIdLength).ToArray();
            offset += imageIdLength;

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

        public int GetSizeInPayload()
        {
            return sizeof(byte) * ImageEnclaveLongIdLength * 3 + sizeof(byte) * ImageEnclaveShortIdLength * 2 + sizeof(uint) * 6;
        }
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

        public int GetSizeInPayload()
        {
            return 2 * sizeof(uint);
        }
    }

    // A managed model of struct VBS_ENCLAVE_REPORT_MODULE
    // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844256(v=vs.85).aspx
    internal class EnclaveReportModule
    {
        private static readonly int ImageEnclaveLongIdLength = 32;

        private static readonly int ImageEnclaveShortIdLength = 16;

        public EnclaveReportModuleHeader Header { get; set; }

        public byte[] UniqueId = new byte[ImageEnclaveLongIdLength];

        public byte[] AuthorId = new byte[ImageEnclaveLongIdLength];

        public byte[] FamilyId = new byte[ImageEnclaveShortIdLength];

        public byte[] ImageId = new byte[ImageEnclaveShortIdLength];

        public uint Svn { get; set; }

        public string ModuleName { get; set; }

        public EnclaveReportModule(byte[] payload)
        {
            int offset = 0;
            Header = new EnclaveReportModuleHeader(payload);
            offset += Convert.ToInt32(Header.GetSizeInPayload());

            int uniqueIdLength = ImageEnclaveLongIdLength;
            UniqueId = payload.Skip(offset).Take(uniqueIdLength).ToArray();
            offset += uniqueIdLength;

            int authorIdLength = ImageEnclaveLongIdLength;
            AuthorId = payload.Skip(offset).Take(authorIdLength).ToArray();
            offset += authorIdLength;

            int familyIdLength = ImageEnclaveShortIdLength;
            FamilyId = payload.Skip(offset).Take(familyIdLength).ToArray();
            offset += familyIdLength;

            int imageIdLength = ImageEnclaveShortIdLength;
            ImageId = payload.Skip(offset).Take(familyIdLength).ToArray();
            offset += imageIdLength;

            Svn = BitConverter.ToUInt32(payload, offset);
            offset += sizeof(uint);

            int strLen = Convert.ToInt32(Header.ModuleSize) - offset;
            ModuleName = BitConverter.ToString(payload, offset, 1);
            offset += sizeof(char) * 1;
        }

        public int GetSizeInPayload()
        {
            return Header.GetSizeInPayload() + Convert.ToInt32(Header.ModuleSize);
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
}
