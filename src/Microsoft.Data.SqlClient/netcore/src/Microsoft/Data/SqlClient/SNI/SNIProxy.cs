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
            string serverCertificateFilename)
        {
            instanceName = new byte[1];

            bool errorWithLocalDBProcessing;
            string localDBDataSource = GetLocalDBDataSource(fullServerName, out errorWithLocalDBProcessing);

            if (errorWithLocalDBProcessing)
            {
                return null;
            }
            // If a localDB Data source is available, we need to use it.
            fullServerName = localDBDataSource ?? fullServerName;

            DataSource details = DataSource.ParseServerName(fullServerName);
            if (details == null)
            {
                return null;
            }

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

        /// <summary>
        /// Gets the Local db Named pipe data source if the input is a localDB server.
        /// </summary>
        /// <param name="fullServerName">The data source</param>
        /// <param name="error">Set true when an error occurred while getting LocalDB up</param>
        /// <returns></returns>
        private static string GetLocalDBDataSource(string fullServerName, out bool error)
        {
            string localDBConnectionString = null;
            string localDBInstance = DataSource.GetLocalDBInstance(fullServerName, out bool isBadLocalDBDataSource);

            if (isBadLocalDBDataSource)
            {
                error = true;
                return null;
            }

            else if (!string.IsNullOrEmpty(localDBInstance))
            {
                // We have successfully received a localDBInstance which is valid.
                Debug.Assert(!string.IsNullOrWhiteSpace(localDBInstance), "Local DB Instance name cannot be empty.");
                localDBConnectionString = LocalDB.GetLocalDBConnectionString(localDBInstance);

                if (fullServerName == null)
                {
                    // The Last error is set in LocalDB.GetLocalDBConnectionString. We don't need to set Last here.
                    error = true;
                    return null;
                }
            }
            error = false;
            return localDBConnectionString;
        }
    }

    internal class DataSource
    {
        private const char CommaSeparator = ',';
        private const char SemiColon = ':';
        private const char BackSlashCharacter = '\\';

        private const string DefaultHostName = "localhost";
        private const string DefaultSqlServerInstanceName = "mssqlserver";
        private const string PipeBeginning = @"\\";
        private const string Slash = @"/";
        private const string PipeToken = "pipe";
        private const string LocalDbHost = "(localdb)";
        private const string LocalDbHost_NP = @"np:\\.\pipe\LOCALDB#";
        private const string NamedPipeInstanceNameHeader = "mssql$";
        private const string DefaultPipeName = "sql\\query";

        internal enum Protocol { TCP, NP, None, Admin };

        internal Protocol _connectionProtocol = Protocol.None;

        /// <summary>
        /// Provides the HostName of the server to connect to for TCP protocol.
        /// This information is also used for finding the SPN of SqlServer
        /// </summary>
        internal string ServerName { get; private set; }

        /// <summary>
        /// Provides the port on which the TCP connection should be made if one was specified in Data Source
        /// </summary>
        internal int Port { get; private set; } = -1;

        /// <summary>
        /// Provides the inferred Instance Name from Server Data Source
        /// </summary>
        internal string InstanceName { get; private set; }

        /// <summary>
        /// Provides the pipe name in case of Named Pipes
        /// </summary>
        internal string PipeName { get; private set; }

        /// <summary>
        /// Provides the HostName to connect to in case of Named pipes Data Source
        /// </summary>
        internal string PipeHostName { get; private set; }

        private string _workingDataSource;
        private string _dataSourceAfterTrimmingProtocol;

        internal bool IsBadDataSource { get; private set; } = false;

        internal bool IsSsrpRequired { get; private set; } = false;

        private DataSource(string dataSource)
        {
            // Remove all whitespaces from the datasource and all operations will happen on lower case.
            _workingDataSource = dataSource.Trim().ToLowerInvariant();

            int firstIndexOfColon = _workingDataSource.IndexOf(SemiColon);

            PopulateProtocol();

            _dataSourceAfterTrimmingProtocol = (firstIndexOfColon > -1) && _connectionProtocol != Protocol.None
                ? _workingDataSource.Substring(firstIndexOfColon + 1).Trim() : _workingDataSource;

            if (_dataSourceAfterTrimmingProtocol.Contains(Slash)) // Pipe paths only allow back slashes
            {
                if (_connectionProtocol == Protocol.None)
                    ReportSNIError(SNIProviders.INVALID_PROV);
                else if (_connectionProtocol == Protocol.NP)
                    ReportSNIError(SNIProviders.NP_PROV);
                else if (_connectionProtocol == Protocol.TCP)
                    ReportSNIError(SNIProviders.TCP_PROV);
            }
        }

        private void PopulateProtocol()
        {
            string[] splitByColon = _workingDataSource.Split(SemiColon);

            if (splitByColon.Length <= 1)
            {
                _connectionProtocol = Protocol.None;
            }
            else
            {
                // We trim before switching because " tcp : server , 1433 " is a valid data source
                switch (splitByColon[0].Trim())
                {
                    case TdsEnums.TCP:
                        _connectionProtocol = Protocol.TCP;
                        break;
                    case TdsEnums.NP:
                        _connectionProtocol = Protocol.NP;
                        break;
                    case TdsEnums.ADMIN:
                        _connectionProtocol = Protocol.Admin;
                        break;
                    default:
                        // None of the supported protocols were found. This may be a IPv6 address
                        _connectionProtocol = Protocol.None;
                        break;
                }
            }
        }

        // LocalDbInstance name always starts with (localdb)
        // possible scenarios:
        // (localdb)\<instance name>
        // or (localdb)\. which goes to default localdb
        // or (localdb)\.\<sharedInstance name>
        internal static string GetLocalDBInstance(string dataSource, out bool error)
        {
            string instanceName = null;
            // ReadOnlySpan is not supported in netstandard 2.0, but installing System.Memory solves the issue
            ReadOnlySpan<char> input = dataSource.AsSpan().TrimStart();
            error = false;
            // NetStandard 2.0 does not support passing a string to ReadOnlySpan<char>
            if (input.StartsWith(LocalDbHost.AsSpan().Trim(), StringComparison.InvariantCultureIgnoreCase))
            {
                // When netcoreapp support for netcoreapp2.1 is dropped these slice calls could be converted to System.Range\System.Index
                // Such ad input = input[1..];
                input = input.Slice(LocalDbHost.Length);
                if (!input.IsEmpty && input[0] == BackSlashCharacter)
                {
                    input = input.Slice(1);
                }
                if (!input.IsEmpty)
                {
                    instanceName = input.Trim().ToString();
                }
                else
                {
                    SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.INVALID_PROV, 0, SNICommon.LocalDBNoInstanceName, Strings.SNI_ERROR_51);
                    error = true;
                }
            }
            else if (input.StartsWith(LocalDbHost_NP.AsSpan().Trim(), StringComparison.InvariantCultureIgnoreCase))
            {
                instanceName = input.Trim().ToString();
            }

            return instanceName;
        }

        internal static DataSource ParseServerName(string dataSource)
        {
            DataSource details = new DataSource(dataSource);

            if (details.IsBadDataSource)
            {
                return null;
            }

            if (details.InferNamedPipesInformation())
            {
                return details;
            }

            if (details.IsBadDataSource)
            {
                return null;
            }

            if (details.InferConnectionDetails())
            {
                return details;
            }

            return null;
        }

        private void InferLocalServerName()
        {
            // If Server name is empty or localhost, then use "localhost"
            if (string.IsNullOrEmpty(ServerName) || IsLocalHost(ServerName) ||
                (Environment.MachineName.Equals(ServerName, StringComparison.CurrentCultureIgnoreCase) &&
                 _connectionProtocol == Protocol.Admin))
            {
                // For DAC use "localhost" instead of the server name.
                ServerName = DefaultHostName;
            }
        }

        private bool InferConnectionDetails()
        {
            string[] tokensByCommaAndSlash = _dataSourceAfterTrimmingProtocol.Split(BackSlashCharacter, CommaSeparator);
            ServerName = tokensByCommaAndSlash[0].Trim();

            int commaIndex = _dataSourceAfterTrimmingProtocol.IndexOf(CommaSeparator);

            int backSlashIndex = _dataSourceAfterTrimmingProtocol.IndexOf(BackSlashCharacter);

            // Check the parameters. The parameters are Comma separated in the Data Source. The parameter we really care about is the port
            // If Comma exists, the try to get the port number
            if (commaIndex > -1)
            {
                string parameter = backSlashIndex > -1
                        ? ((commaIndex > backSlashIndex) ? tokensByCommaAndSlash[2].Trim() : tokensByCommaAndSlash[1].Trim())
                        : tokensByCommaAndSlash[1].Trim();

                // Bad Data Source like "server, "
                if (string.IsNullOrEmpty(parameter))
                {
                    ReportSNIError(SNIProviders.INVALID_PROV);
                    return false;
                }

                // For Tcp and Only Tcp are parameters allowed.
                if (_connectionProtocol == Protocol.None)
                {
                    _connectionProtocol = Protocol.TCP;
                }
                else if (_connectionProtocol != Protocol.TCP)
                {
                    // Parameter has been specified for non-TCP protocol. This is not allowed.
                    ReportSNIError(SNIProviders.INVALID_PROV);
                    return false;
                }

                int port;
                if (!int.TryParse(parameter, out port))
                {
                    ReportSNIError(SNIProviders.TCP_PROV);
                    return false;
                }

                // If the user explicitly specified a invalid port in the connection string.
                if (port < 1)
                {
                    ReportSNIError(SNIProviders.TCP_PROV);
                    return false;
                }

                Port = port;
            }
            // Instance Name Handling. Only if we found a '\' and we did not find a port in the Data Source
            else if (backSlashIndex > -1)
            {
                // This means that there will not be any part separated by comma.
                InstanceName = tokensByCommaAndSlash[1].Trim();

                if (string.IsNullOrWhiteSpace(InstanceName))
                {
                    ReportSNIError(SNIProviders.INVALID_PROV);
                    return false;
                }

                if (DefaultSqlServerInstanceName.Equals(InstanceName))
                {
                    ReportSNIError(SNIProviders.INVALID_PROV);
                    return false;
                }

                IsSsrpRequired = true;
            }

            InferLocalServerName();

            return true;
        }

        private void ReportSNIError(SNIProviders provider)
        {
            SNILoadHandle.SingletonInstance.LastError = new SNIError(provider, 0, SNICommon.InvalidConnStringError, Strings.SNI_ERROR_25);
            IsBadDataSource = true;
        }

        private bool InferNamedPipesInformation()
        {
            // If we have a datasource beginning with a pipe or we have already determined that the protocol is Named Pipe
            if (_dataSourceAfterTrimmingProtocol.StartsWith(PipeBeginning) || _connectionProtocol == Protocol.NP)
            {
                // If the data source is "np:servername"
                if (!_dataSourceAfterTrimmingProtocol.Contains(PipeBeginning))
                {
                    PipeHostName = ServerName = _dataSourceAfterTrimmingProtocol;
                    InferLocalServerName();
                    PipeName = SNINpHandle.DefaultPipePath;
                    return true;
                }

                try
                {
                    string[] tokensByBackSlash = _dataSourceAfterTrimmingProtocol.Split(BackSlashCharacter);

                    // The datasource is of the format \\host\pipe\sql\query [0]\[1]\[2]\[3]\[4]\[5]
                    // It would at least have 6 parts.
                    // Another valid Sql named pipe for an named instance is \\.\pipe\MSSQL$MYINSTANCE\sql\query
                    if (tokensByBackSlash.Length < 6)
                    {
                        ReportSNIError(SNIProviders.NP_PROV);
                        return false;
                    }

                    string host = tokensByBackSlash[2];

                    if (string.IsNullOrEmpty(host))
                    {
                        ReportSNIError(SNIProviders.NP_PROV);
                        return false;
                    }

                    //Check if the "pipe" keyword is the first part of path
                    if (!PipeToken.Equals(tokensByBackSlash[3]))
                    {
                        ReportSNIError(SNIProviders.NP_PROV);
                        return false;
                    }

                    if (tokensByBackSlash[4].StartsWith(NamedPipeInstanceNameHeader))
                    {
                        InstanceName = tokensByBackSlash[4].Substring(NamedPipeInstanceNameHeader.Length);
                    }

                    StringBuilder pipeNameBuilder = new StringBuilder();

                    for (int i = 4; i < tokensByBackSlash.Length - 1; i++)
                    {
                        pipeNameBuilder.Append(tokensByBackSlash[i]);
                        pipeNameBuilder.Append(Path.DirectorySeparatorChar);
                    }
                    // Append the last part without a "/"
                    pipeNameBuilder.Append(tokensByBackSlash[tokensByBackSlash.Length - 1]);
                    PipeName = pipeNameBuilder.ToString();

                    if (string.IsNullOrWhiteSpace(InstanceName) && !DefaultPipeName.Equals(PipeName))
                    {
                        InstanceName = PipeToken + PipeName;
                    }

                    ServerName = IsLocalHost(host) ? Environment.MachineName : host;
                    // Pipe hostname is the hostname after leading \\ which should be passed down as is to open Named Pipe.
                    // For Named Pipes the ServerName makes sense for SPN creation only.
                    PipeHostName = host;
                }
                catch (UriFormatException)
                {
                    ReportSNIError(SNIProviders.NP_PROV);
                    return false;
                }

                // DataSource is something like "\\pipename"
                if (_connectionProtocol == Protocol.None)
                {
                    _connectionProtocol = Protocol.NP;
                }
                else if (_connectionProtocol != Protocol.NP)
                {
                    // In case the path began with a "\\" and protocol was not Named Pipes
                    ReportSNIError(SNIProviders.NP_PROV);
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool IsLocalHost(string serverName)
            => ".".Equals(serverName) || "(local)".Equals(serverName) || "localhost".Equals(serverName);
    }
}
