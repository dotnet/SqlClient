// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Data
{
    internal partial class StringsHelper : Strings
    {
        // This method is used to decide if we need to append the exception message parameters to the message when calling Strings.Format. 
        // by default it returns false.
        // Native code generators can replace the value this returns based on user input at the time of native code generation.
        // Marked as NoInlining because if this is used in an AoT compiled app that is not compiled into a single file, the user
        // could compile each module with a different setting for this. We want to make sure there's a consistent behavior
        // that doesn't depend on which native module this method got inlined into.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool UsingResourceKeys()
        {
            return false;
        }

        public static string Format(string resourceFormat, params object[] args)
        {
            if (args is not null)
            {
                if (UsingResourceKeys())
                {
                    return resourceFormat + string.Join(", ", args);
                }

                return string.Format(resourceFormat, args);
            }

            return resourceFormat;
        }

        public static string Format(string resourceFormat, object p1)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1);
            }

            return string.Format(resourceFormat, p1);
        }

        public static string Format(string resourceFormat, object p1, object p2)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2);
            }

            return string.Format(resourceFormat, p1, p2);
        }

        public static string Format(string resourceFormat, object p1, object p2, object p3)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2, p3);
            }

            return string.Format(resourceFormat, p1, p2, p3);
        }
    }

    // This class is added temporary in order to have all Strings.resx as constant.
    // NetFx is creating them on build time with powershell and target file located in netfx/tools folder and adds exact same class as below to obj folder for netfx.
    // When we have the localization available for netcore we can follow the same pattern and add MetdaDataAttribute class and run them only on windows platform
    internal partial class StringsHelper
    {
        internal class ResourceNames
        {
            internal const string DataCategory_Data = @"Data";
            internal const string DataCategory_Update = @"Update";
            internal const string DataCategory_Xml = @"XML";
            internal const string DbCommand_CommandTimeout = @"Time to wait for command to execute.";
            internal const string DbConnection_State = @"The ConnectionState indicating whether the connection is open or closed.";
            internal const string DataCategory_Fill = @"Fill";
            internal const string DataCategory_InfoMessage = @"InfoMessage";
            internal const string DataCategory_StatementCompleted = @"StatementCompleted";
            internal const string DataCategory_Notification = @"Notification";
            internal const string DataCategory_Advanced = @"Advanced";
            internal const string DataCategory_Context = @"Context";
            internal const string DataCategory_Initialization = @"Initialization";
            internal const string DataCategory_Pooling = @"Pooling";
            internal const string DataCategory_Security = @"Security";
            internal const string DataCategory_Source = @"Source";
            internal const string DataCategory_Replication = @"Replication";
            internal const string DataCategory_ConnectionResilency = @"Connection Resiliency";
            internal const string DbDataAdapter_DeleteCommand = @"Used during Update for deleted rows in DataSet.";
            internal const string DbDataAdapter_InsertCommand = @"Used during Update for new rows in DataSet.";
            internal const string DbDataAdapter_SelectCommand = @"Used during Fill/FillSchema.";
            internal const string DbDataAdapter_UpdateCommand = @"Used during Update for modified rows in DataSet.";
            internal const string DbDataAdapter_RowUpdated = @"Event triggered before every DataRow during Update.";
            internal const string DbDataAdapter_RowUpdating = @"Event triggered after every DataRow during Update.";
            internal const string DbConnectionString_ApplicationName = @"The name of the application.";
            internal const string DbConnectionString_AttachDBFilename = @"The name of the primary file, including the full path name, of an attachable database.";
            internal const string DbConnectionString_ConnectTimeout = @"The length of time (in seconds) to wait for a connection to the server before terminating the attempt and generating an error.";
            internal const string DbConnectionString_CurrentLanguage = @"The SQL Server Language record name.";
            internal const string DbConnectionString_DataSource = @"Indicates the name of the data source to connect to.";
            internal const string DbConnectionString_Encrypt = @"When true, SQL Server uses SSL encryption for all data sent between the client and server if the server has a certificate installed.";
            internal const string DbConnectionString_Enlist = @"Sessions in a Component Services (or MTS, if you are using Microsoft Windows NT) environment should automatically be enlisted in a global transaction where required.";
            internal const string DbConnectionString_FailoverPartner = @"The name or network address of the instance of SQL Server that acts as a failover partner.";
            internal const string DbConnectionString_FailoverPartnerSPN = @"The service principal name (SPN) of the failover partner.";
            internal const string DbConnectionString_HostNameInCertificate = @"The hostname to be expected in the server's certificate when encryption is negotiated, if it's different from the default value derived from Addr/Address/Server.";
            internal const string DbConnectionString_InitialCatalog = @"The name of the initial catalog or database in the data source.";
            internal const string DbConnectionString_IntegratedSecurity = @"Whether the connection is to be a secure connection or not.";
            internal const string DbConnectionString_LoadBalanceTimeout = @"The minimum amount of time (in seconds) for this connection to live in the pool before being destroyed.";
            internal const string DbConnectionString_MaxPoolSize = @"The maximum number of connections allowed in the pool.";
            internal const string DbConnectionString_MinPoolSize = @"The minimum number of connections allowed in the pool.";
            internal const string DbConnectionString_MultipleActiveResultSets = @"When true, multiple result sets can be returned and read from one connection.";
            internal const string DbConnectionString_MultiSubnetFailover = @"If your application is connecting to a high-availability, disaster recovery (AlwaysOn) availability group (AG) on different subnets, MultiSubnetFailover=Yes configures SqlConnection to provide faster detection of and connection to the (currently) active server.";
            internal const string DbConnectionString_PacketSize = @"Size in bytes of the network packets used to communicate with an instance of SQL Server.";
            internal const string DbConnectionString_Password = @"Indicates the password to be used when connecting to the data source.";
            internal const string DbConnectionString_PersistSecurityInfo = @"When false, security-sensitive information, such as the password, is not returned as part of the connection if the connection is open or has ever been in an open state.";
            internal const string DbConnectionString_Pooling = @"When true, the connection object is drawn from the appropriate pool, or if necessary, is created and added to the appropriate pool.";
            internal const string DbConnectionString_Replication = @"Used by SQL Server in Replication.";
            internal const string DbConnectionString_ServerCertificate = @"The path to a certificate file to match against the SQL Server TLS/SSL certificate.";
            internal const string DbConnectionString_ServerSPN = @"The service principal name (SPN) of the server.";
            internal const string DbConnectionString_TransactionBinding = @"Indicates binding behavior of connection to a System.Transactions Transaction when enlisted.";
            internal const string DbConnectionString_TrustServerCertificate = @"When true (and encrypt=true), SQL Server uses SSL encryption for all data sent between the client and server without validating the server certificate.";
            internal const string DbConnectionString_TypeSystemVersion = @"Indicates which server type system the provider will expose through the DataReader.";
            internal const string DbConnectionString_UserID = @"Indicates the user ID to be used when connecting to the data source.";
            internal const string DbConnectionString_UserInstance = @"Indicates whether the connection will be re-directed to connect to an instance of SQL Server running under the user's account.";
            internal const string DbConnectionString_WorkstationID = @"The name of the workstation connecting to SQL Server.";
            internal const string DbConnectionString_ApplicationIntent = @"Declares the application workload type when connecting to a server.";
            internal const string DbConnectionString_ConnectRetryCount = @"Number of attempts to restore connection.";
            internal const string DbConnectionString_ConnectRetryInterval = @"Delay between attempts to restore connection.";
            internal const string DbConnectionString_Authentication = @"Specifies the method of authenticating with SQL Server.";
            internal const string DbConnectionString_Certificate = @"Specified client certificate for authenticating with SQL Server.  ";
            internal const string SqlConnection_AccessToken = @"Access token to use for authentication.";
            internal const string SqlConnection_ConnectionString = @"Information used to connect to a DataSource, such as 'Data Source=x;Initial Catalog=x;Integrated Security=SSPI'.";
            internal const string SqlConnection_ConnectionTimeout = @"Current connection timeout value, 'Connect Timeout=X' in the ConnectionString.";
            internal const string SqlConnection_Database = @"Current SQL Server database, 'Initial Catalog=X' in the connection string.";
            internal const string SqlConnection_DataSource = @"Current SqlServer that the connection is opened to, 'Data Source=X' in the connection string.";
            internal const string SqlConnection_PacketSize = @"Network packet size, 'Packet Size=x' in the connection string.";
            internal const string SqlConnection_ServerVersion = @"Version of the SQL Server accessed by the SqlConnection.";
            internal const string SqlConnection_WorkstationId = @"Workstation Id, 'Workstation ID=x' in the connection string.";
            internal const string SqlConnection_StatisticsEnabled = @"Collect statistics for this connection.";
            internal const string SqlConnection_ClientConnectionId = @"A guid to represent the physical connection.";
            internal const string SqlConnection_Credential = @"User Id and secure password to use for authentication.";
            internal const string DbConnection_InfoMessage = @"Event triggered when messages arrive from the DataSource.";
            internal const string DbCommand_CommandText = @"Command text to execute.";
            internal const string DbCommand_CommandType = @"How to interpret the CommandText.";
            internal const string DbCommand_Connection = @"Connection used by the command.";
            internal const string DbCommand_Parameters = @"The parameters collection.";
            internal const string DbCommand_Transaction = @"The transaction used by the command.";
            internal const string DbCommand_UpdatedRowSource = @"When used by a DataAdapter.Update, how command results are applied to the current DataRow.";
            internal const string DbCommand_StatementCompleted = @"When records are affected by a given statement by the execution of the command.";
            internal const string SqlParameter_SourceColumnNullMapping = @"When used by DataAdapter.Update, the parameter value is changed from DBNull.Value into (Int32)1 or (Int32)0 if non-null.";
            internal const string SqlCommand_Notification = @"Notification values used by Microsoft SQL Server.";
            internal const string TCE_DbConnectionString_EnclaveAttestationUrl = @"Specifies an endpoint of an enclave attestation service, which will be used to verify whether the enclave, configured in the SQL Server instance for computations on database columns encrypted using Always Encrypted, is valid and secure.";
            internal const string TCE_SqlCommand_ColumnEncryptionSetting = @"Column encryption setting for the command. Overrides the connection level default.";
            internal const string TCE_DbConnectionString_ColumnEncryptionSetting = @"Default column encryption setting for all the commands on the connection.";
            internal const string TCE_SqlConnection_TrustedColumnMasterKeyPaths = @"Dictionary object containing SQL Server names and their trusted column master key paths.";
            internal const string DbConnectionString_PoolBlockingPeriod = @"Defines the blocking period behavior for a connection pool.";
            internal const string TCE_SqlConnection_ColumnEncryptionQueryMetadataCacheEnabled = @"Defines whether query metadata caching is enabled.";
            internal const string TCE_SqlConnection_ColumnEncryptionKeyCacheTtl = @"Defines the time-to-live of entries in the column encryption key cache.";
            internal const string TCE_DbConnectionString_AttestationProtocol = @"Specifies an attestation protocol for its corresponding enclave attestation service.";
            internal const string TCE_DbConnectionString_IPAddressPreference = @"Specifies an IP address preference when connecting to SQL instances.";
            internal const string SqlConnection_ServerProcessId = @"Server Process Id (SPID) of the active connection.";
            internal const string SqlCommandBuilder_DataAdapter = @"The DataAdapter for which to automatically generate SqlCommands.";
        }
    }
}

