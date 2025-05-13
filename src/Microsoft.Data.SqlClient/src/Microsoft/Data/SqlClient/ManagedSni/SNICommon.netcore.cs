// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    

    /// <summary>
    /// SMUX packet header
    /// </summary>
    internal sealed class SNISMUXHeader
    {
        public const int HEADER_LENGTH = 16;

        public byte SMID;
        public byte flags;
        public ushort sessionId;
        public uint length;
        public uint sequenceNumber;
        public uint highwater;

        public void Read(byte[] bytes)
        {
            SMID = bytes[0];
            flags = bytes[1];
            Span<byte> span = bytes.AsSpan();
            sessionId = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2));
            length = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4)) - SNISMUXHeader.HEADER_LENGTH;
            sequenceNumber = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8));
            highwater = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12));
        }

        public void Write(Span<byte> bytes)
        {
            uint value = highwater;
            // access the highest element first to cause the largest range check in the jit, then fill in the rest of the value and carry on as normal
            bytes[15] = (byte)((value >> 24) & 0xff);
            bytes[12] = (byte)(value & 0xff); // BitConverter.GetBytes(_currentHeader.highwater).CopyTo(headerBytes, 12);
            bytes[13] = (byte)((value >> 8) & 0xff);
            bytes[14] = (byte)((value >> 16) & 0xff);

            bytes[0] = SMID; // BitConverter.GetBytes(_currentHeader.SMID).CopyTo(headerBytes, 0);
            bytes[1] = flags; // BitConverter.GetBytes(_currentHeader.flags).CopyTo(headerBytes, 1);

            value = sessionId;
            bytes[2] = (byte)(value & 0xff); // BitConverter.GetBytes(_currentHeader.sessionId).CopyTo(headerBytes, 2);
            bytes[3] = (byte)((value >> 8) & 0xff);

            value = length;
            bytes[4] = (byte)(value & 0xff); // BitConverter.GetBytes(_currentHeader.length).CopyTo(headerBytes, 4);
            bytes[5] = (byte)((value >> 8) & 0xff);
            bytes[6] = (byte)((value >> 16) & 0xff);
            bytes[7] = (byte)((value >> 24) & 0xff);

            value = sequenceNumber;
            bytes[8] = (byte)(value & 0xff); // BitConverter.GetBytes(_currentHeader.sequenceNumber).CopyTo(headerBytes, 8);
            bytes[9] = (byte)((value >> 8) & 0xff);
            bytes[10] = (byte)((value >> 16) & 0xff);
            bytes[11] = (byte)((value >> 24) & 0xff);

        }
    }

    /// <summary>
    /// SMUX packet flags
    /// </summary>
    [Flags]
    internal enum SNISMUXFlags
    {
        SMUX_SYN = 1,       // Begin SMUX connection
        SMUX_ACK = 2,       // Acknowledge SMUX packets
        SMUX_FIN = 4,       // End SMUX connection
        SMUX_DATA = 8       // SMUX data packet
    }

    internal class SNICommon
    {
        // Each error number maps to SNI_ERROR_* in String.resx
        internal const int ConnTerminatedError = 2;
        internal const int InvalidParameterError = 5;
        internal const int ProtocolNotSupportedError = 8;
        internal const int ConnTimeoutError = 11;
        internal const int ConnNotUsableError = 19;
        internal const int InvalidConnStringError = 25;
        internal const int ErrorLocatingServerInstance = 26;
        internal const int HandshakeFailureError = 31;
        internal const int InternalExceptionError = 35;
        internal const int ConnOpenFailedError = 40;
        internal const int ErrorSpnLookup = 44;
        internal const int LocalDBErrorCode = 50;
        internal const int MultiSubnetFailoverWithMoreThan64IPs = 47;
        internal const int MultiSubnetFailoverWithInstanceSpecified = 48;
        internal const int MultiSubnetFailoverWithNonTcpProtocol = 49;
        internal const int MaxErrorValue = 50157;
        internal const int LocalDBNoInstanceName = 51;
        internal const int LocalDBNoInstallation = 52;
        internal const int LocalDBInvalidConfig = 53;
        internal const int LocalDBNoSqlUserInstanceDllPath = 54;
        internal const int LocalDBInvalidSqlUserInstanceDllPath = 55;
        internal const int LocalDBFailedToLoadDll = 56;
        internal const int LocalDBBadRuntime = 57;

        /// <summary>
        /// We either validate that the provided 'validationCert' matches the 'serverCert', or we validate that the server name in the 'serverCert' matches 'targetServerName'.
        /// Certificate validation and chain trust validations are done by SSLStream class [System.Net.Security.SecureChannel.VerifyRemoteCertificate method]
        /// This method is called as a result of callback for SSL Stream Certificate validation.
        /// </summary>
        /// <param name="connectionId">Connection ID/GUID for tracing</param>
        /// <param name="targetServerName">Server that client is expecting to connect to</param>
        /// <param name="hostNameInCertificate">Optional hostname to use for server certificate validation</param>
        /// <param name="serverCert">X.509 certificate from the server</param>
        /// <param name="validationCertFileName">Path to an X.509 certificate file from the application to compare with the serverCert</param>
        /// <param name="policyErrors">Policy errors</param>
        /// <returns>True if certificate is valid</returns>
        internal static bool ValidateSslServerCertificate(Guid connectionId, string targetServerName, string hostNameInCertificate, X509Certificate serverCert, string validationCertFileName, SslPolicyErrors policyErrors)
        {
            using (TrySNIEventScope.Create(nameof(SNICommon)))
            {
                if (policyErrors == SslPolicyErrors.None)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.INFO, "Connection Id {0}, targetServerName {1}, SSL Server certificate not validated as PolicyErrors set to None.", args0: connectionId, args1: targetServerName);
                    return true;
                }

                string serverNameToValidate;
                X509Certificate validationCertificate = null;
                if (!string.IsNullOrEmpty(hostNameInCertificate))
                {
                    serverNameToValidate = hostNameInCertificate;
                }
                else
                {
                    serverNameToValidate = targetServerName;
                }

                if (!string.IsNullOrEmpty(validationCertFileName))
                {
                    try
                    {
#if NET9_0_OR_GREATER
                        validationCertificate = X509CertificateLoader.LoadCertificateFromFile(validationCertFileName);
#else
                        validationCertificate = new X509Certificate(validationCertFileName);
#endif
                    }
                    catch (Exception e)
                    {
                        // if this fails, then fall back to the HostNameInCertificate or TargetServer validation.
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniTcpHandle), EventType.INFO, "Connection Id {0}, Exception occurred loading specified ServerCertificate: {1}, treating it as if ServerCertificate has not been specified.", args0: connectionId, args1: e.Message);
                    }
                }

                if (validationCertificate != null)
                {
                    if (serverCert.GetRawCertData().AsSpan().SequenceEqual(validationCertificate.GetRawCertData().AsSpan()))
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.INFO, "Connection Id {0}, ServerCertificate matches the certificate provided by the server. Certificate validation passed.", args0: connectionId);
                        return true;
                    }
                    else
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.INFO, "Connection Id {0}, ServerCertificate doesn't match the certificate provided by the server. Certificate validation failed.", args0: connectionId);
                        throw ADP.SSLCertificateAuthenticationException(Strings.SQL_RemoteCertificateDoesNotMatchServerCertificate);
                    }
                }
                else
                {
                    // If we get to this point then there is a ssl policy flag.
                    StringBuilder messageBuilder = new();
                    if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.ERR, "Connection Id {0}, targetServerName {1}, SSL Server certificate not validated as PolicyErrors set to RemoteCertificateNotAvailable.", args0: connectionId, args1: targetServerName);
                        messageBuilder.AppendLine(Strings.SQL_RemoteCertificateNotAvailable);
                    }

                    if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.ERR, "Connection Id {0}, targetServerName {0}, SslPolicyError {1}, SSL Policy certificate chain has errors.", args0: connectionId, args1: targetServerName, args2: policyErrors);

                        // get the chain status from the certificate
                        X509Certificate2 cert2 = serverCert as X509Certificate2;
                        X509Chain chain = new();
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.Offline;
                        StringBuilder chainStatusInformation = new();
                        bool chainIsValid = chain.Build(cert2);
                        Debug.Assert(!chainIsValid, "RemoteCertificateChainError flag is detected, but certificate chain is valid.");
                        if (!chainIsValid)
                        {
                            foreach (X509ChainStatus chainStatus in chain.ChainStatus)
                            {
                                chainStatusInformation.Append($"{chainStatus.StatusInformation}, [Status: {chainStatus.Status}]");
                                chainStatusInformation.AppendLine();
                            }
                        }
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.ERR, "Connection Id {0}, targetServerName {1}, SslPolicyError {2}, SSL Policy certificate chain has errors. ChainStatus {3}", args0: connectionId, args1: targetServerName, args2: policyErrors, args3: chainStatusInformation);
                        messageBuilder.AppendFormat(Strings.SQL_RemoteCertificateChainErrors, chainStatusInformation);
                        messageBuilder.AppendLine();
                    }

                    if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                    {
                        X509Certificate2 cert2 = serverCert as X509Certificate2;
                        if (!cert2.MatchesHostname(serverNameToValidate))
                        {
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.ERR, "Connection Id {0}, serverNameToValidate {1}, Target Server name or HNIC does not match the Subject/SAN in Certificate.", args0: connectionId, args1: serverNameToValidate);
                            messageBuilder.AppendLine(Strings.SQL_RemoteCertificateNameMismatch);
                        }
                    }

                    if (messageBuilder.Length > 0)
                    {
                        throw ADP.SSLCertificateAuthenticationException(messageBuilder.ToString());
                    }
                }

                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.INFO, "Connection Id {0}, certificate with subject: {1}, validated successfully.", args0: connectionId, args1: serverCert.Subject);
                return true;
            }
        }

        internal static IPAddress[] GetDnsIpAddresses(string serverName, TimeoutTimer timeout)
        {
            IPAddress[] ipAddresses = GetDnsIpAddresses(serverName);

            // We cannot timeout accurately in sync code above, so throw TimeoutException if we've now exceeded the timeout.
            if (timeout.IsExpired)
            {
                throw new TimeoutException();
            }
            return ipAddresses;
        }

        internal static IPAddress[] GetDnsIpAddresses(string serverName)
        {
            using (TrySNIEventScope.Create(nameof(SNICommon)))
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.INFO, "Getting DNS host entries for serverName {0}.", args0: serverName);
                return Dns.GetHostAddresses(serverName);
            }
        }

        /// <summary>
        /// Sets last error encountered for SNI
        /// </summary>
        /// <param name="provider">SNI provider</param>
        /// <param name="nativeError">Native error code</param>
        /// <param name="sniError">SNI error code</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns></returns>
        internal static uint ReportSNIError(SniProviders provider, uint nativeError, uint sniError, string errorMessage)
        {
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.ERR, "Provider = {0}, native Error = {1}, SNI Error = {2}, Error Message = {3}", args0: provider, args1: nativeError, args2: sniError, args3: errorMessage);
            return ReportSNIError(new SniError(provider, nativeError, sniError, errorMessage));
        }

        /// <summary>
        /// Sets last error encountered for SNI
        /// </summary>
        /// <param name="provider">SNI provider</param>
        /// <param name="sniError">SNI error code</param>
        /// <param name="sniException">SNI Exception</param>
        /// <param name="nativeErrorCode">Native SNI error code</param>
        /// <returns></returns>
        internal static uint ReportSNIError(SniProviders provider, uint sniError, Exception sniException, uint nativeErrorCode = 0)
        {
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNICommon), EventType.ERR, "Provider = {0}, SNI Error = {1}, Exception = {2}", args0: provider, args1: sniError, args2: sniException?.Message);
            return ReportSNIError(new SniError(provider, sniError, sniException, nativeErrorCode));
        }

        /// <summary>
        /// Sets last error encountered for SNI
        /// </summary>
        /// <param name="error">SNI error</param>
        /// <returns></returns>
        internal static uint ReportSNIError(SniError error)
        {
            SniLoadHandle.SingletonInstance.LastError = error;
            return TdsEnums.SNI_ERROR;
        }
    }
}
