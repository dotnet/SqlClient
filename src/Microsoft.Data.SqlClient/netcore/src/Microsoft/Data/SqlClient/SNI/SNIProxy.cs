// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// Managed SNI proxy implementation. Contains many SNI entry points used by SqlClient.
    /// </summary>
    internal class SNIProxy
    {
        private const int DefaultSqlServerPort = 1433;
        private const int DefaultSqlServerDacPort = 1434;
        private const string SqlServerSpnHeader = "MSSQLSvc";

        private static readonly SNIProxy s_singleton = new SNIProxy();

        internal static SNIProxy Instance => s_singleton;

        /// <summary>
        /// Generate SSPI context
        /// </summary>
        /// <param name="sspiClientContextStatus">SSPI client context status</param>
        /// <param name="receivedBuff">Receive buffer</param>
        /// <param name="sendBuff">Send buffer</param>
        /// <param name="serverName">Service Principal Name buffer</param>
        /// <returns>SNI error code</returns>
        internal static void GenSspiClientContext(SspiClientContextStatus sspiClientContextStatus, byte[] receivedBuff, ref byte[] sendBuff, byte[][] serverName)
        {
            SafeDeleteContext securityContext = sspiClientContextStatus.SecurityContext;
            ContextFlagsPal contextFlags = sspiClientContextStatus.ContextFlags;
            SafeFreeCredentials credentialsHandle = sspiClientContextStatus.CredentialsHandle;

            string securityPackage = NegotiationInfoClass.Negotiate;

            if (securityContext == null)
            {
                credentialsHandle = NegotiateStreamPal.AcquireDefaultCredential(securityPackage, false);
            }

            SecurityBuffer[] inSecurityBufferArray;
            if (receivedBuff != null)
            {
                inSecurityBufferArray = new SecurityBuffer[] { new SecurityBuffer(receivedBuff, SecurityBufferType.SECBUFFER_TOKEN) };
            }
            else
            {
                inSecurityBufferArray = Array.Empty<SecurityBuffer>();
            }

            int tokenSize = NegotiateStreamPal.QueryMaxTokenSize(securityPackage);

            SecurityBuffer outSecurityBuffer = new SecurityBuffer(tokenSize, SecurityBufferType.SECBUFFER_TOKEN);

            ContextFlagsPal requestedContextFlags = ContextFlagsPal.Connection
                | ContextFlagsPal.Confidentiality
                | ContextFlagsPal.Delegate
                | ContextFlagsPal.MutualAuth;

            string[] serverSPNs = new string[serverName.Length];
            for (int i = 0; i < serverName.Length; i++)
            {
                serverSPNs[i] = Encoding.Unicode.GetString(serverName[i]);
            }
            SecurityStatusPal statusCode = NegotiateStreamPal.InitializeSecurityContext(
                       credentialsHandle,
                       ref securityContext,
                       serverSPNs,
                       requestedContextFlags,
                       inSecurityBufferArray,
                       outSecurityBuffer,
                       ref contextFlags);

            if (statusCode.ErrorCode == SecurityStatusPalErrorCode.CompleteNeeded ||
                statusCode.ErrorCode == SecurityStatusPalErrorCode.CompAndContinue)
            {
                inSecurityBufferArray = new SecurityBuffer[] { outSecurityBuffer };
                statusCode = NegotiateStreamPal.CompleteAuthToken(ref securityContext, inSecurityBufferArray);
                outSecurityBuffer.token = null;
            }

            sendBuff = outSecurityBuffer.token;
            if (sendBuff == null)
            {
                sendBuff = Array.Empty<byte>();
            }

            sspiClientContextStatus.SecurityContext = securityContext;
            sspiClientContextStatus.ContextFlags = contextFlags;
            sspiClientContextStatus.CredentialsHandle = credentialsHandle;

            if (IsErrorStatus(statusCode.ErrorCode))
            {
                // Could not access Kerberos Ticket.
                //
                // SecurityStatusPalErrorCode.InternalError only occurs in Unix and always comes with a GssApiException,
                // so we don't need to check for a GssApiException here.
                if (statusCode.ErrorCode == SecurityStatusPalErrorCode.InternalError)
                {
                    throw new InvalidOperationException(SQLMessage.KerberosTicketMissingError() + "\n" + statusCode);
                }
                else
                {
                    throw new InvalidOperationException(SQLMessage.SSPIGenerateError() + "\n" + statusCode);
                }
            }
        }

        private static bool IsErrorStatus(SecurityStatusPalErrorCode errorCode)
        {
            return errorCode != SecurityStatusPalErrorCode.NotSet &&
                errorCode != SecurityStatusPalErrorCode.OK &&
                errorCode != SecurityStatusPalErrorCode.ContinueNeeded &&
                errorCode != SecurityStatusPalErrorCode.CompleteNeeded &&
                errorCode != SecurityStatusPalErrorCode.CompAndContinue &&
                errorCode != SecurityStatusPalErrorCode.ContextExpired &&
                errorCode != SecurityStatusPalErrorCode.CredentialsNeeded &&
                errorCode != SecurityStatusPalErrorCode.Renegotiate;
        }

        /// <summary>
        /// Create a SNI connection handle
        /// </summary>
        /// <param name="fullServerName">Full server name from connection string</param>
        /// <param name="ignoreSniOpenTimeout">Ignore open timeout</param>
        /// <param name="timerExpire">Timer expiration</param>
        /// <param name="instanceName">Instance name</param>
        /// <param name="spnBuffer">SPN</param>
        /// <param name="serverSPN">pre-defined SPN</param>
        /// <param name="flushCache">Flush packet cache</param>
        /// <param name="async">Asynchronous connection</param>
        /// <param name="parallel">Attempt parallel connects</param>
        /// <param name="isIntegratedSecurity"></param>
        /// <param name="ipPreference">IP address preference</param>
        /// <param name="cachedFQDN">Used for DNS Cache</param>
        /// <param name="pendingDNSInfo">Used for DNS Cache</param>
        /// <param name="tlsFirst">Support TDS8.0</param>
        /// <param name="hostNameInCertificate">Used for the HostName in certificate</param>
        /// <param name="serverCertificateFilename">Used for the path to the Server Certificate</param>
        /// <param name="details">DataSource deatils</param>
        /// <returns>SNI handle</returns>
        internal static SNIHandle CreateConnectionHandle(
            string fullServerName,
            bool ignoreSniOpenTimeout,
            long timerExpire,
            out byte[] instanceName,
            ref byte[][] spnBuffer,
            string serverSPN,
            bool flushCache,
            bool async,
            bool parallel,
            bool isIntegratedSecurity,
            SqlConnectionIPAddressPreference ipPreference,
            string cachedFQDN,
            ref SQLDNSInfo pendingDNSInfo,
            bool tlsFirst,
            string hostNameInCertificate,
            string serverCertificateFilename,
            DataSource details)
        {
            instanceName = new byte[1];
            SNIHandle sniHandle = null;
            switch (details._connectionProtocol)
            {
                case DataSource.Protocol.Admin:
                case DataSource.Protocol.None: // default to using tcp if no protocol is provided
                case DataSource.Protocol.TCP:
                    sniHandle = CreateTcpHandle(details, timerExpire, parallel, ipPreference, cachedFQDN, ref pendingDNSInfo,
                        tlsFirst, hostNameInCertificate, serverCertificateFilename);
                    break;
                case DataSource.Protocol.NP:
                    sniHandle = CreateNpHandle(details, timerExpire, parallel, tlsFirst);
                    break;
                default:
                    Debug.Fail($"Unexpected connection protocol: {details._connectionProtocol}");
                    break;
            }

            if (isIntegratedSecurity)
            {
                try
                {
                    spnBuffer = GetSqlServerSPNs(details, serverSPN);
                }
                catch (Exception e)
                {
                    SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.INVALID_PROV, SNICommon.ErrorSpnLookup, e);
                }
            }

            SqlClientEventSource.Log.TryTraceEvent("SNIProxy.CreateConnectionHandle | Info | Session Id {0}, SNI Handle Type: {1}", sniHandle?.ConnectionId, sniHandle?.GetType());
            return sniHandle;
        }

        private static byte[][] GetSqlServerSPNs(DataSource dataSource, string serverSPN)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(dataSource.ServerName));
            if (!string.IsNullOrWhiteSpace(serverSPN))
            {
                return new byte[1][] { Encoding.Unicode.GetBytes(serverSPN) };
            }

            string hostName = dataSource.ServerName;
            string postfix = null;
            if (dataSource.Port != -1)
            {
                postfix = dataSource.Port.ToString();
            }
            else if (!string.IsNullOrWhiteSpace(dataSource.InstanceName))
            {
                postfix = dataSource.InstanceName;
            }

            SqlClientEventSource.Log.TryTraceEvent("SNIProxy.GetSqlServerSPN | Info | ServerName {0}, InstanceName {1}, Port {2}, postfix {3}", dataSource?.ServerName, dataSource?.InstanceName, dataSource?.Port, postfix);
            return GetSqlServerSPNs(hostName, postfix, dataSource._connectionProtocol);
        }

        private static byte[][] GetSqlServerSPNs(string hostNameOrAddress, string portOrInstanceName, DataSource.Protocol protocol)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(hostNameOrAddress));
            IPHostEntry hostEntry = null;
            string fullyQualifiedDomainName;
            try
            {
                hostEntry = Dns.GetHostEntry(hostNameOrAddress);
            }
            catch (SocketException)
            {
                // A SocketException can occur while resolving the hostname.
                // We will fallback on using hostname from the connection string in the finally block
            }
            finally
            {
                // If the DNS lookup failed, then resort to using the user provided hostname to construct the SPN.
                fullyQualifiedDomainName = hostEntry?.HostName ?? hostNameOrAddress;
            }

            string serverSpn = SqlServerSpnHeader + "/" + fullyQualifiedDomainName;

            if (!string.IsNullOrWhiteSpace(portOrInstanceName))
            {
                serverSpn += ":" + portOrInstanceName;
            }
            else if (protocol == DataSource.Protocol.None || protocol == DataSource.Protocol.TCP) // Default is TCP
            {
                string serverSpnWithDefaultPort = serverSpn + $":{DefaultSqlServerPort}";
                // Set both SPNs with and without Port as Port is optional for default instance
                SqlClientEventSource.Log.TryAdvancedTraceEvent("SNIProxy.GetSqlServerSPN | Info | ServerSPNs {0} and {1}", serverSpn, serverSpnWithDefaultPort);
                return new byte[][] { Encoding.Unicode.GetBytes(serverSpn), Encoding.Unicode.GetBytes(serverSpnWithDefaultPort) };
            }
            // else Named Pipes do not need to valid port

            SqlClientEventSource.Log.TryAdvancedTraceEvent("SNIProxy.GetSqlServerSPN | Info | ServerSPN {0}", serverSpn);
            return new byte[][] { Encoding.Unicode.GetBytes(serverSpn) };
        }

        /// <summary>
        /// Creates an SNITCPHandle object
        /// </summary>
        /// <param name="details">Data source</param>
        /// <param name="timerExpire">Timer expiration</param>
        /// <param name="parallel">Should MultiSubnetFailover be used</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <param name="cachedFQDN">Key for DNS Cache</param>
        /// <param name="pendingDNSInfo">Used for DNS Cache</param>
        /// <param name="tlsFirst">Support TDS8.0</param>
        /// <param name="hostNameInCertificate">Host name in certificate</param>
        /// <param name="serverCertificateFilename">Used for the path to the Server Certificate</param>
        /// <returns>SNITCPHandle</returns>
        private static SNITCPHandle CreateTcpHandle(
            DataSource details,
            long timerExpire,
            bool parallel,
            SqlConnectionIPAddressPreference ipPreference,
            string cachedFQDN,
            ref SQLDNSInfo pendingDNSInfo,
            bool tlsFirst,
            string hostNameInCertificate,
            string serverCertificateFilename)
        {
            // TCP Format:
            // tcp:<host name>\<instance name>
            // tcp:<host name>,<TCP/IP port number>

            string hostName = details.ServerName;
            if (string.IsNullOrWhiteSpace(hostName))
            {
                SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.TCP_PROV, 0, SNICommon.InvalidConnStringError, Strings.SNI_ERROR_25);
                return null;
            }

            int port = -1;
            bool isAdminConnection = details._connectionProtocol == DataSource.Protocol.Admin;
            if (details.IsSsrpRequired)
            {
                try
                {
                    port = isAdminConnection ?
                            SSRP.GetDacPortByInstanceName(hostName, details.InstanceName, timerExpire, parallel, ipPreference) :
                            SSRP.GetPortByInstanceName(hostName, details.InstanceName, timerExpire, parallel, ipPreference);
                }
                catch (SocketException se)
                {
                    SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.TCP_PROV, SNICommon.ErrorLocatingServerInstance, se);
                    return null;
                }
            }
            else if (details.Port != -1)
            {
                port = details.Port;
            }
            else
            {
                port = isAdminConnection ? DefaultSqlServerDacPort : DefaultSqlServerPort;
            }

            return new SNITCPHandle(hostName, port, timerExpire, parallel, ipPreference, cachedFQDN, ref pendingDNSInfo,
                tlsFirst, hostNameInCertificate, serverCertificateFilename);
        }

        /// <summary>
        /// Creates an SNINpHandle object
        /// </summary>
        /// <param name="details">Data source</param>
        /// <param name="timerExpire">Timer expiration</param>
        /// <param name="parallel">Should MultiSubnetFailover be used. Only returns an error for named pipes.</param>
        /// <param name="tlsFirst"></param>
        /// <returns>SNINpHandle</returns>
        private static SNINpHandle CreateNpHandle(DataSource details, long timerExpire, bool parallel, bool tlsFirst)
        {
            if (parallel)
            {
                // Connecting to a SQL Server instance using the MultiSubnetFailover connection option is only supported when using the TCP protocol
                SNICommon.ReportSNIError(SNIProviders.NP_PROV, 0, SNICommon.MultiSubnetFailoverWithNonTcpProtocol, Strings.SNI_ERROR_49);
                return null;
            }
            return new SNINpHandle(details.PipeHostName, details.PipeName, timerExpire, tlsFirst);
        }

        /// <summary>
        /// Get last SNI error on this thread
        /// </summary>
        /// <returns></returns>
        internal SNIError GetLastError()
        {
            return SNILoadHandle.SingletonInstance.LastError;
        }
    }
}
