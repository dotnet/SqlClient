// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.Common.ConnectionString
{
    internal static class DbConnectionStringSynonyms
    {
        //internal const string TransparentNetworkIPResolution = TRANSPARENTNETWORKIPRESOLUTION;
        internal const string TRANSPARENTNETWORKIPRESOLUTION = "transparentnetworkipresolution";

        //internal const string ApplicationName = APP;
        internal const string APP = "app";

        // internal const string IPAddressPreference = IPADDRESSPREFERENCE;
        internal const string IPADDRESSPREFERENCE = "ipaddresspreference";

        //internal const string ApplicationIntent = APPLICATIONINTENT;
        internal const string APPLICATIONINTENT = "applicationintent";

        //internal const string AttachDBFilename = EXTENDEDPROPERTIES + "," + INITIALFILENAME;
        internal const string EXTENDEDPROPERTIES = "extended properties";
        internal const string INITIALFILENAME = "initial file name";

        // internal const string HostNameInCertificate = HOSTNAMEINCERTIFICATE;
        internal const string HOSTNAMEINCERTIFICATE = "hostnameincertificate";

        // internal const string ServerCertificate = SERVERCERTIFICATE;
        internal const string SERVERCERTIFICATE = "servercertificate";

        //internal const string ConnectTimeout = CONNECTIONTIMEOUT + "," +TIMEOUT;
        internal const string CONNECTIONTIMEOUT = "connection timeout";
        internal const string TIMEOUT = "timeout";

        //internal const string ConnectRetryCount = CONNECTRETRYCOUNT;
        internal const string CONNECTRETRYCOUNT = "connectretrycount";

        //internal const string ConnectRetryInterval = CONNECTRETRYINTERVAL;
        internal const string CONNECTRETRYINTERVAL = "connectretryinterval";

        //internal const string CurrentLanguage = LANGUAGE;
        internal const string LANGUAGE = "language";

        //internal const string OraDataSource = SERVER;
        //internal const string SqlDataSource = ADDR + "," + ADDRESS + "," + SERVER + "," + NETWORKADDRESS;
        internal const string ADDR = "addr";
        internal const string ADDRESS = "address";
        internal const string SERVER = "server";
        internal const string NETWORKADDRESS = "network address";

        //internal const string InitialCatalog = DATABASE;
        internal const string DATABASE = "database";

        //internal const string IntegratedSecurity = TRUSTEDCONNECTION;
        internal const string TRUSTEDCONNECTION = "trusted_connection"; // underscore introduced in everett

        //internal const string LoadBalanceTimeout = ConnectionLifetime;
        internal const string ConnectionLifetime = "connection lifetime";

        //internal const string MultipleActiveResultSets = MULTIPLEACTIVERESULTSETS;
        internal const string MULTIPLEACTIVERESULTSETS = "multipleactiveresultsets";

        //internal const string MultiSubnetFailover = MULTISUBNETFAILOVER;
        internal const string MULTISUBNETFAILOVER = "multisubnetfailover";

        //internal const string NetworkLibrary = NET + "," + NETWORK;
        internal const string NET = "net";
        internal const string NETWORK = "network";

        //internal const string PoolBlockingPeriod = POOLBLOCKINGPERIOD;
        internal const string POOLBLOCKINGPERIOD = "poolblockingperiod";

        //internal const string Password = Pwd;
        internal const string Pwd = "pwd";

        //internal const string PersistSecurityInfo = PERSISTSECURITYINFO;
        internal const string PERSISTSECURITYINFO = "persistsecurityinfo";

        //internal const string TrustServerCertificate = TRUSTSERVERCERTIFICATE;
        internal const string TRUSTSERVERCERTIFICATE = "trustservercertificate";

        //internal const string UserID = UID + "," + User;
        internal const string UID = "uid";
        internal const string User = "user";

        //internal const string WorkstationID = WSID;
        internal const string WSID = "wsid";

        //internal const string server SPNs
        internal const string ServerSPN = "ServerSPN";
        internal const string FailoverPartnerSPN = "FailoverPartnerSPN";
    }
}
