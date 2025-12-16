// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;

namespace Microsoft.Data.Common.ConnectionString
{
    internal static class DbConnectionStringDefaults
    {
        internal const ApplicationIntent ApplicationIntent = Microsoft.Data.SqlClient.ApplicationIntent.ReadWrite;
        internal const string ApplicationName =
            #if NETFRAMEWORK
            "Framework Microsoft SqlClient Data Provider";
            #else
            "Core Microsoft SqlClient Data Provider";
            #endif
        internal const string AttachDBFilename = "";
        internal const SqlConnectionAttestationProtocol AttestationProtocol = SqlConnectionAttestationProtocol.NotSpecified;
        internal static readonly SqlAuthenticationMethod Authentication = SqlAuthenticationMethod.NotSpecified;
        internal const SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Disabled;
        internal const int CommandTimeout = 30;
        internal const int ConnectRetryCount = 1;
        internal const int ConnectRetryInterval = 10;
        internal const int ConnectTimeout = 15;
        internal const bool ContextConnection = false;
        internal const string CurrentLanguage = "";
        internal const string DataSource = "";
        internal const string EnclaveAttestationUrl = "";
        internal static readonly SqlConnectionEncryptOption Encrypt = SqlConnectionEncryptOption.Mandatory;
        internal const bool Enlist = true;
        internal const string FailoverPartner = "";
        internal const string FailoverPartnerSPN = "";
        internal const string HostNameInCertificate = "";
        internal const string InitialCatalog = "";
        internal const bool IntegratedSecurity = false;
        internal const SqlConnectionIPAddressPreference IPAddressPreference = SqlConnectionIPAddressPreference.IPv4First;
        internal const int LoadBalanceTimeout = 0; // default of 0 means don't use
        internal const int MaxPoolSize = 100;
        internal const int MinPoolSize = 0;
        internal const bool MultipleActiveResultSets = false;
        internal static bool MultiSubnetFailover => LocalAppContextSwitches.EnableMultiSubnetFailoverByDefault;
        internal const int PacketSize = 8000;
        internal const string Password = "";
        internal const bool PersistSecurityInfo = false;
        internal const PoolBlockingPeriod PoolBlockingPeriod = SqlClient.PoolBlockingPeriod.Auto;
        internal const bool Pooling = true;
        internal const bool Replication = false;
        internal const string ServerCertificate = "";
        internal const string ServerSPN = "";
        internal const string TransactionBinding = "Implicit Unbind";
        internal const bool TrustServerCertificate = false;
        internal const string TypeSystemVersion = "Latest";
        internal const string UserID = "";
        internal const bool UserInstance = false;
        internal const string WorkstationID = "";
        
        #if NETFRAMEWORK
        internal const bool ConnectionReset = true;
        internal static bool TransparentNetworkIpResolution => !LocalAppContextSwitches.DisableTnirByDefault;
        internal const string NetworkLibrary = "";
        #endif
    }
}
