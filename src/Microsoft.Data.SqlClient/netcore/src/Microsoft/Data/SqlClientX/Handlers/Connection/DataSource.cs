// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using SNILoadHandle = Microsoft.Data.SqlClient.SNI.SNILoadHandle;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    internal sealed class DataSource
    {
        #region Constants

        private const char Backslash = '\\';
        private const char CommaSeparator = ',';
        private const string DefaultHostname = "localhost";
        private const string DefaultPipeName = "sql\\query";
        private const string DefaultSqlServerInstanceName = "mssqlserver";
        private const string InstancePrefix = "MSSQL$";
        private const string LocalDbHost = "(localdb)";
        private const string LocalDbHostNamedPipe = @"np:\\.\pipe\LOCALDB#";
        private const string NamedPipeInstanceNameHeader = "mssql$";
        private const string PathSeparator = "\\";
        private const string PipeBeginning = @"\\";
        private const string PipeToken = "pipe";
        private const char SemicolonSeparator = ':';
        private const string Slash = @"/";

        #endregion
        
        private string _workingDataSource;
        private string _dataSourceAfterTrimmingProtocol;

        /// <summary>
        /// Parameter-less constructor intended for use in unit tests.
        /// </summary>
        internal DataSource()
        {
        }
        
        // @TODO: Move logic for parsing from DataSource to DataSourceParsingHandler
        private DataSource(string dataSource)
        {
            // Remove all whitespaces from the datasource and all operations will happen on lower case.
            _workingDataSource = dataSource.Trim().ToLowerInvariant();

            int firstIndexOfColon = _workingDataSource.IndexOf(SemicolonSeparator);

            PopulateProtocol();

            _dataSourceAfterTrimmingProtocol = (firstIndexOfColon > -1) && Protocol != DataSourceProtocol.NotSpecified
                ? _workingDataSource.Substring(firstIndexOfColon + 1).Trim()
                : _workingDataSource;

            if (_dataSourceAfterTrimmingProtocol.Contains(Slash)) // Pipe paths only allow backslashes
            {
                if (Protocol == DataSourceProtocol.NotSpecified)
                    ReportSniError(SNIProviders.INVALID_PROV);
                else if (Protocol == DataSourceProtocol.NamedPipe)
                    ReportSniError(SNIProviders.NP_PROV);
                else if (Protocol == DataSourceProtocol.Tcp)
                    ReportSniError(SNIProviders.TCP_PROV);
            }
        }
        
        #region Properties

        /// <summary>
        /// Gets or sets the inferred instance name as parsed from the data source string.
        /// </summary>
        internal string InstanceName { get; set; }

        /// <summary>
        /// Gets or sets whether the data source is "bad".
        /// </summary>
        internal bool IsBadDataSource { get; set; } = false;

        /// <summary>
        /// Gets or sets whether SSRP resolution is required to determine the port for the
        /// desired server instance.
        /// </summary>
        internal bool IsSsrpRequired { get; set; } = false;

        /// <summary>
        /// Gets or sets the hostname to connect to in case of named pipe data source.
        /// </summary>
        // @TODO: Can this be rolled into the servername property?
        internal string PipeHostname { get; private set; }

        /// <summary>
        /// Gets or sets the pipe name in case of named pipe data source.
        /// </summary>
        internal string PipeName { get; set; }

        /// <summary>
        /// Gets or sets the port to use when a TCP data source is specified. This will be set to
        /// the default port number if a port number was not specified.
        /// </summary>
        internal int Port { get; set; } = -1;

        /// <summary>
        /// Gets or sets the protocol that was resolved from the connection string. If this is
        /// <see cref="DataSourceProtocol.NotSpecified"/>, the protocol could not be reliably determined.
        /// </summary>
        internal DataSourceProtocol Protocol { get; set; }

        /// <summary>
        /// Gets or sets the port resolved by SSRP if an instance name was specified. If this is
        /// <c>null</c>, use <see cref="Port"/> instead.
        /// </summary>
        internal int? ResolvedPort { get; set; }

        /// <summary>
        /// Gets or sets the hostname of the server to connect to when a TCP data source is
        /// specified. This information is also used for finding the SPN of SqlServer
        /// </summary>
        internal string ServerName { get; set; }

        #endregion
        
        #region Parsing Logic
        // @TODO: Move this logic into the data source parsing handler
        
        internal static string GetLocalDbInstance(string dataSource, out bool error)
        {
            // LocalDbInstance name always starts with (localdb)
            // possible scenarios:
            // (localdb)\<instance name>
            // or (localdb)\. which goes to default localdb
            // or (localdb)\.\<sharedInstance name>
            
            string instanceName = null;
            ReadOnlySpan<char> input = dataSource.AsSpan().TrimStart();
            error = false;
            
            int index = input.IndexOf(LocalDbHost, StringComparison.InvariantCultureIgnoreCase);
            if (input.StartsWith(LocalDbHostNamedPipe, StringComparison.InvariantCultureIgnoreCase))
            {
                instanceName = input.Trim().ToString();
            }
            else if (index > 0)
            {
                SNILoadHandle.SingletonInstance.LastError = new SNIError(
                    SNIProviders.INVALID_PROV,
                    0,
                    SNICommon.ErrorLocatingServerInstance,
                    Strings.SNI_ERROR_26);
                SqlClientEventSource.Log.TrySNITraceEvent(
                    nameof(SNIProxy),
                    EventType.ERR,
                    "Incompatible use of prefix with LocalDb: '{0}'",
                    dataSource);
                error = true;
            }
            else if (index == 0)
            {
                input = input.Slice(LocalDbHost.Length);
                if (!input.IsEmpty && input[0] == Backslash)
                {
                    input = input.Slice(1);
                }
                if (!input.IsEmpty)
                {
                    instanceName = input.Trim().ToString();
                }
                else
                {
                    SNILoadHandle.SingletonInstance.LastError = new SNIError(
                        SNIProviders.INVALID_PROV, 
                        0, 
                        SNICommon.LocalDBNoInstanceName,
                        Strings.SNI_ERROR_51);
                    error = true;
                }
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
        
        private static bool IsLocalHost(string serverName) => serverName is "." or "(local)" or "localhost";

        private bool InferConnectionDetails()
        {
            string[] tokensByCommaAndSlash = _dataSourceAfterTrimmingProtocol.Split(Backslash, CommaSeparator);
            ServerName = tokensByCommaAndSlash[0].Trim();

            int commaIndex = _dataSourceAfterTrimmingProtocol.IndexOf(CommaSeparator);

            int backSlashIndex = _dataSourceAfterTrimmingProtocol.IndexOf(Backslash);

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
                    ReportSniError(SNIProviders.INVALID_PROV);
                    return false;
                }

                // For Tcp and Only Tcp are parameters allowed.
                if (Protocol is DataSourceProtocol.NotSpecified)
                {
                    Protocol = DataSourceProtocol.Tcp;
                }
                else if (Protocol is not DataSourceProtocol.Tcp)
                {
                    // Parameter has been specified for non-TCP protocol. This is not allowed.
                    ReportSniError(SNIProviders.INVALID_PROV);
                    return false;
                }

                int port;
                if (!int.TryParse(parameter, out port))
                {
                    ReportSniError(SNIProviders.TCP_PROV);
                    return false;
                }

                // If the user explicitly specified an invalid port in the connection string.
                if (port < 1)
                {
                    ReportSniError(SNIProviders.TCP_PROV);
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
                    ReportSniError(SNIProviders.INVALID_PROV);
                    return false;
                }

                if (InstanceName == DefaultSqlServerInstanceName)
                {
                    ReportSniError(SNIProviders.INVALID_PROV);
                    return false;
                }

                IsSsrpRequired = true;
            }

            InferLocalServerName();

            return true;
        }

        private void InferLocalServerName()
        {
            // If Server name is empty or localhost, then use "localhost"
            if (string.IsNullOrEmpty(ServerName) || IsLocalHost(ServerName) ||
                (Environment.MachineName.Equals(ServerName, StringComparison.CurrentCultureIgnoreCase) &&
                 Protocol is DataSourceProtocol.Admin))
            {
                // For DAC use "localhost" instead of the server name.
                ServerName = DefaultHostname;
            }
        }

        private bool InferNamedPipesInformation()
        {
            // If we have a datasource beginning with a pipe, or we have already determined that
            // the protocol is named pipe
            if (_dataSourceAfterTrimmingProtocol.StartsWith(PipeBeginning, StringComparison.Ordinal) || Protocol is DataSourceProtocol.NamedPipe)
            {
                // If the data source starts with "np:servername"
                if (!_dataSourceAfterTrimmingProtocol.Contains(PipeBeginning))
                {
                    // Assuming that user did not change default NamedPipe name, if the datasource
                    // is in the format servername\instance, separate servername and instance and
                    // prepend instance with MSSQL$ and append default pipe path 
                    // https://learn.microsoft.com/en-us/sql/tools/configuration-manager/named-pipes-properties?view=sql-server-ver16
                    if (_dataSourceAfterTrimmingProtocol.Contains(PathSeparator) && Protocol is DataSourceProtocol.NamedPipe)
                    {
                        string[] tokensByBackSlash = _dataSourceAfterTrimmingProtocol.Split(Backslash);
                        if (tokensByBackSlash.Length == 2)
                        {
                            // NamedPipeClientStream object will create the network path using
                            // PipeHostname and PipeName and can be seen in its _normalizedPipePath
                            // variable in the format `\\servername\pipe\MSSQL$<instancename>\sql\query`
                            PipeHostname = ServerName = tokensByBackSlash[0];
                            PipeName = $"{InstancePrefix}{tokensByBackSlash[1]}{PathSeparator}{DefaultPipeName}";
                        }
                        else
                        {
                            ReportSniError(SNIProviders.NP_PROV);
                            return false;
                        }
                    }
                    else
                    {
                        PipeHostname = ServerName = _dataSourceAfterTrimmingProtocol;
                        PipeName = SNINpHandle.DefaultPipePath;
                    }

                    InferLocalServerName();
                    return true;
                }

                try
                {
                    string[] tokensByBackSlash = _dataSourceAfterTrimmingProtocol.Split(Backslash);

                    // The datasource is of the format \\host\pipe\sql\query [0]\[1]\[2]\[3]\[4]\[5]
                    // It would at least have 6 parts.
                    // Another valid Sql named pipe for a named instance is \\.\pipe\MSSQL$MYINSTANCE\sql\query
                    if (tokensByBackSlash.Length < 6)
                    {
                        ReportSniError(SNIProviders.NP_PROV);
                        return false;
                    }

                    string host = tokensByBackSlash[2];
                    if (string.IsNullOrEmpty(host))
                    {
                        ReportSniError(SNIProviders.NP_PROV);
                        return false;
                    }

                    //Check if the "pipe" keyword is the first part of path
                    if (tokensByBackSlash[3] == PipeToken)
                    {
                        ReportSniError(SNIProviders.NP_PROV);
                        return false;
                    }

                    if (tokensByBackSlash[4].StartsWith(NamedPipeInstanceNameHeader, StringComparison.Ordinal))
                    {
                        InstanceName = tokensByBackSlash[4].Substring(NamedPipeInstanceNameHeader.Length);
                    }

                    StringBuilder pipeNameBuilder = new();
                    for (int i = 4; i < tokensByBackSlash.Length - 1; i++)
                    {
                        pipeNameBuilder.Append(tokensByBackSlash[i]);
                        pipeNameBuilder.Append(Path.DirectorySeparatorChar);
                    }
                    
                    // Append the last part without a "/"
                    pipeNameBuilder.Append(tokensByBackSlash[^1]);
                    PipeName = pipeNameBuilder.ToString();

                    if (string.IsNullOrWhiteSpace(InstanceName) && PipeName != DefaultPipeName)
                    {
                        InstanceName = PipeToken + PipeName;
                    }

                    ServerName = IsLocalHost(host) ? Environment.MachineName : host;
                    
                    // Pipe hostname is the hostname after leading \\ which should be passed down as is to open Named Pipe.
                    // For Named Pipes the ServerName makes sense for SPN creation only.
                    PipeHostname = host;
                }
                catch (UriFormatException)
                {
                    ReportSniError(SNIProviders.NP_PROV);
                    return false;
                }

                // DataSource is something like "\\pipename"
                if (Protocol is DataSourceProtocol.NotSpecified)
                {
                    Protocol = DataSourceProtocol.NamedPipe;
                }
                else if (Protocol is not DataSourceProtocol.NamedPipe)
                {
                    // In case the path began with a "\\" and protocol was not Named Pipes
                    ReportSniError(SNIProviders.NP_PROV);
                    return false;
                }
                return true;
            }
            return false;
        }

        private void PopulateProtocol()
        {
            string[] splitByColon = _workingDataSource.Split(SemicolonSeparator);

            if (splitByColon.Length <= 1)
            {
                Protocol = DataSourceProtocol.NotSpecified;
            }
            else
            {
                // We trim before switching because " tcp : server , 1433 " is a valid data source
                switch (splitByColon[0].Trim())
                {
                    case TdsEnums.TCP:
                        Protocol = DataSourceProtocol.Tcp;
                        break;
                    case TdsEnums.NP:
                        Protocol = DataSourceProtocol.NamedPipe;
                        break;
                    case TdsEnums.ADMIN:
                        Protocol = DataSourceProtocol.Admin;
                        break;
                    default:
                        // None of the supported protocols were found. This may be a IPv6 address
                        Protocol = DataSourceProtocol.NotSpecified;
                        break;
                }
            }
        }

        private void ReportSniError(SNIProviders provider)
        {
            SNILoadHandle.SingletonInstance.LastError = new SNIError(provider, 0, SNICommon.InvalidConnStringError, Strings.SNI_ERROR_25);
            IsBadDataSource = true;
        }

        #endregion
    }
}
