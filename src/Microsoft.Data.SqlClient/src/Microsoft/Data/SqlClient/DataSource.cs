// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System;
using System.Configuration.Provider;

namespace Microsoft.Data.SqlClient
{
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

        internal const int LocalDBNoInstanceName = 51;
        internal const int LocalDBNoInstallation = 52;
        internal const int LocalDBInvalidConfig = 53;
        internal const int LocalDBNoSqlUserInstanceDllPath = 54;
        internal const int LocalDBInvalidSqlUserInstanceDllPath = 55;
        internal const int LocalDBFailedToLoadDll = 56;
        internal const int LocalDBBadRuntime = 57;
        internal const int InvalidConnStringError = 25;

        private enum ProviderEnum
        {
            HTTP_PROV,
            NP_PROV,
            SESSION_PROV,
            SIGN_PROV,
            SM_PROV,
            SMUX_PROV,
            SSL_PROV,
            TCP_PROV,
            VIA_PROV,
            CTAIP_PROV,
            MAX_PROVS,
            INVALID_PROV,
        }
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
                {
                    IsBadDataSource = true;
                    throw new Exception(LocalDBErrorFormat(ProviderEnum.INVALID_PROV));
                }
                else if (_connectionProtocol == Protocol.NP)
                {
                    IsBadDataSource = true;
                    throw new Exception(LocalDBErrorFormat(ProviderEnum.NP_PROV));
                }
                else if (_connectionProtocol == Protocol.TCP)
                {
                    IsBadDataSource = true;
                    throw new Exception(LocalDBErrorFormat(ProviderEnum.TCP_PROV));
                }
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
                    error = true;
                    throw new Exception(string.Format("(provider: {0}, error: {1} - {2})",
                    ProviderEnum.INVALID_PROV, LocalDBNoInstanceName, Strings.SNI_ERROR_51));
                }
            }
            else if (input.StartsWith(LocalDbHost_NP.AsSpan().Trim(), StringComparison.InvariantCultureIgnoreCase))
            {
                instanceName = input.Trim().ToString();
            }

            return instanceName;
        }
        private static string LocalDBErrorFormat(ProviderEnum providerName)
        {
            return string.Format("(provider: {0}, error: {1} - {2})",
                    providerName, InvalidConnStringError ,Strings.SNI_ERROR_25);

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
                    throw new Exception(Strings.SNI_ERROR_25);
                }

                // For Tcp and Only Tcp are parameters allowed.
                if (_connectionProtocol == Protocol.None)
                {
                    _connectionProtocol = Protocol.TCP;
                }
                else if (_connectionProtocol != Protocol.TCP)
                {
                    IsBadDataSource = true;
                    // Parameter has been specified for non-TCP protocol. This is not allowed.
                    throw new Exception(LocalDBErrorFormat(ProviderEnum.INVALID_PROV));
                }

                int port;
                if (!int.TryParse(parameter, out port))
                {
                    IsBadDataSource = true;
                    throw new Exception(LocalDBErrorFormat(ProviderEnum.TCP_PROV));
                }

                // If the user explicitly specified a invalid port in the connection string.
                if (port < 1)
                {
                    IsBadDataSource = true;
                    throw new Exception(LocalDBErrorFormat(ProviderEnum.TCP_PROV));
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
                    IsBadDataSource= true;
                    throw new Exception(ProviderEnum.INVALID_PROV.ToString());
                }

                if (DefaultSqlServerInstanceName.Equals(InstanceName))
                {
                    IsBadDataSource= true;
                    throw new Exception(ProviderEnum.INVALID_PROV.ToString());
                }

                IsSsrpRequired = true;
            }

            InferLocalServerName();

            return true;
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
                    PipeName = DefaultPipeName;
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
                        IsBadDataSource = true;
                        throw new Exception(LocalDBErrorFormat(ProviderEnum.NP_PROV));
                    }

                    string host = tokensByBackSlash[2];

                    if (string.IsNullOrEmpty(host))
                    {
                        IsBadDataSource = true;
                        throw new Exception(LocalDBErrorFormat(ProviderEnum.NP_PROV));
                    }

                    //Check if the "pipe" keyword is the first part of path
                    if (!PipeToken.Equals(tokensByBackSlash[3]))
                    {
                        IsBadDataSource = true;
                        throw new Exception(LocalDBErrorFormat(ProviderEnum.NP_PROV));
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
                    IsBadDataSource = true;
                    throw new Exception(LocalDBErrorFormat(ProviderEnum.NP_PROV));
                }

                // DataSource is something like "\\pipename"
                if (_connectionProtocol == Protocol.None)
                {
                    _connectionProtocol = Protocol.NP;
                }
                else if (_connectionProtocol != Protocol.NP)
                {
                    IsBadDataSource = true;
                    // In case the path began with a "\\" and protocol was not Named Pipes
                    throw new Exception(LocalDBErrorFormat(ProviderEnum.NP_PROV));
                }
                return true;
            }
            return false;
        }

        private static bool IsLocalHost(string serverName)
            => ".".Equals(serverName) || "(local)".Equals(serverName) || "localhost".Equals(serverName);
    }

}
