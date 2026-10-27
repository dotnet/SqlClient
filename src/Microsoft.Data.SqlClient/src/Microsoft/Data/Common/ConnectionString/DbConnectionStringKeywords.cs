// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.Common.ConnectionString
{
    internal static class DbConnectionStringKeywords
    {
        // SqlClient
        internal const string ApplicationIntent = "Application Intent";
        internal const string ApplicationName = "Application Name";
        internal const string AttachDBFilename = "AttachDbFilename";
        internal const string AttestationProtocol = "Attestation Protocol";
        internal const string Authentication = "Authentication";
        internal const string ColumnEncryptionSetting = "Column Encryption Setting";
        internal const string CommandTimeout = "Command Timeout";
        internal const string ConnectionReset = "Connection Reset";
        internal const string ConnectRetryCount = "Connect Retry Count";
        internal const string ConnectRetryInterval = "Connect Retry Interval";
        internal const string ConnectTimeout = "Connect Timeout";
        internal const string ContextConnection = "Context Connection";
        internal const string CurrentLanguage = "Current Language";
        internal const string DataSource = "Data Source";
        internal const string EnclaveAttestationUrl = "Enclave Attestation Url";
        internal const string Encrypt = "Encrypt";
        internal const string Enlist = "Enlist";
        internal const string FailoverPartner = "Failover Partner";
        internal const string FailoverPartnerSPN = "Failover Partner SPN";
        internal const string HostNameInCertificate = "Host Name In Certificate";
        internal const string InitialCatalog = "Initial Catalog";
        internal const string IntegratedSecurity = "Integrated Security";
        internal const string IPAddressPreference = "IP Address Preference";
        internal const string LoadBalanceTimeout = "Load Balance Timeout";
        internal const string MaxPoolSize = "Max Pool Size";
        internal const string MinPoolSize = "Min Pool Size";
        internal const string MultipleActiveResultSets = "Multiple Active Result Sets";
        internal const string MultiSubnetFailover = "Multi Subnet Failover";
        internal const string NetworkLibrary = "Network Library";
        internal const string PacketSize = "Packet Size";
        internal const string Password = "Password";
        internal const string PersistSecurityInfo = "Persist Security Info";
        internal const string PoolBlockingPeriod = "Pool Blocking Period";
        internal const string Pooling = "Pooling";
        internal const string Replication = "Replication";
        internal const string ServerCertificate = "Server Certificate";
        internal const string ServerSPN = "Server SPN";
        internal const string TransactionBinding = "Transaction Binding";
        internal const string TransparentNetworkIPResolution = "Transparent Network IP Resolution";
        internal const string TrustServerCertificate = "Trust Server Certificate";
        internal const string TypeSystemVersion = "Type System Version";
        internal const string UserID = "User ID";
        internal const string UserInstance = "User Instance";
        internal const string WorkstationID = "Workstation ID";
        
        #if NETFRAMEWORK
        // Odbc
        internal const string Driver = "Driver";
        internal const string Dsn = "Dsn";
        internal const string FileDsn = "FileDsn";
        internal const string SaveFile = "SaveFile";

        // OleDb
        internal const string FileName = "File Name";
        internal const string OleDbServices = "OLE DB Services";
        internal const string Provider = "Provider";

        // OracleClient
        internal const string Unicode = "Unicode";
        internal const string OmitOracleConnectionName = "Omit Oracle Connection Name";
        #endif
    }
}
