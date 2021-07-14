// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
    internal partial class StringsHelper : Strings
    {
        static StringsHelper loader = null;
        ResourceManager resources;

        internal StringsHelper()
        {
            resources = new ResourceManager("Microsoft.Data.SqlClient.Resources.Strings", this.GetType().Assembly);
        }

        private static StringsHelper GetLoader()
        {
            if (loader == null)
            {
                StringsHelper sr = new StringsHelper();
                Interlocked.CompareExchange(ref loader, sr, null);
            }
            return loader;
        }

        private static CultureInfo CultureHelper => null/*use ResourceManager default, CultureInfo.CurrentUICulture*/;

        public static ResourceManager Resources => GetLoader().resources;


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

        public static string GetResourceString(string res)
        {
            StringsHelper sys = GetLoader();
            if (sys == null)
                return null;

            // If "res" is a resource id, temp will not be null, "res" will contain the retrieved resource string.
            // If "res" is not a resource id, temp will be null.
            string temp = sys.resources.GetString(res, StringsHelper.Culture);
            if (temp != null)
                res = temp;

            return res;
        }
        public static string GetString(string res, params object[] args)
        {
            res = GetResourceString(res);
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string value = args[i] as string;
                    if (value != null && value.Length > 1024)
                    {
                        args[i] = value.Substring(0, 1024 - 3) + "...";
                    }
                }
                return string.Format(CultureInfo.CurrentCulture, res, args);
            }
            else
            {
                return res;
            }
        }

        public static string Format(string resourceFormat, params object[] args)
        {
            if (args != null)
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

    internal partial class StringsHelper
    {
        internal class ResourceNames
        {
            internal const string DataCategory_Advanced = @"Advanced";
            internal const string DataCategory_ConnectionResilency = @"Connection Resiliency";
            internal const string DataCategory_Context = @"Context";
            internal const string DataCategory_Initialization = @"Initialization";
            internal const string DataCategory_Pooling = @"Pooling";
            internal const string DataCategory_Replication = @"Replication";
            internal const string DataCategory_Security = @"Security";
            internal const string DataCategory_Source = @"Source";
            internal const string DbCommand_CommandTimeout = @"Time to wait for command to execute.";
            internal const string DbConnectionString_ApplicationIntent = @"Declares the application workload type when connecting to a server.";
            internal const string DbConnectionString_ApplicationName = @"The name of the application.";
            internal const string DbConnectionString_AttachDBFilename = @"The name of the primary file, including the full path name, of an attachable database.";
            internal const string DbConnectionString_Authentication = @"Specifies the method of authenticating with SQL Server.";
            internal const string DbConnectionString_ConnectRetryCount = @"Number of attempts to restore connection.";
            internal const string DbConnectionString_ConnectRetryInterval = @"Delay between attempts to restore connection.";
            internal const string DbConnectionString_ConnectTimeout = @"The length of time (in seconds) to wait for a connection to the server before terminating the attempt and generating an error.";
            internal const string DbConnectionString_CurrentLanguage = @"The SQL Server Language record name.";
            internal const string DbConnectionString_DataSource = @"Indicates the name of the data source to connect to.";
            internal const string DbConnectionString_Encrypt = @"When true, SQL Server uses SSL encryption for all data sent between the client and server if the server has a certificate installed.";
            internal const string DbConnectionString_Enlist = @"Sessions in a Component Services (or MTS, if you are using Microsoft Windows NT) environment should automatically be enlisted in a global transaction where required.";
            internal const string DbConnectionString_FailoverPartner = @"The name or network address of the instance of SQL Server that acts as a failover partner.";
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
            internal const string DbConnectionString_PoolBlockingPeriod = @"Defines the blocking period behavior for a connection pool.";
            internal const string DbConnectionString_Pooling = @"When true, the connection object is drawn from the appropriate pool, or if necessary, is created and added to the appropriate pool.";
            internal const string DbConnectionString_Replication = @"Used by SQL Server in Replication.";
            internal const string DbConnectionString_TrustServerCertificate = @"When true (and encrypt=true), SQL Server uses SSL encryption for all data sent between the client and server without validating the server certificate.";
            internal const string DbConnectionString_TypeSystemVersion = @"Indicates which server type system the provider will expose through the DataReader.";
            internal const string DbConnectionString_UserID = @"Indicates the user ID to be used when connecting to the data source.";
            internal const string DbConnectionString_UserInstance = @"Indicates whether the connection will be re-directed to connect to an instance of SQL Server running under the user's account.";
            internal const string DbConnectionString_WorkstationID = @"The name of the workstation connecting to SQL Server.";
            internal const string DbConnectionString_TransactionBinding = @"Indicates binding behavior of connection to a System.Transactions Transaction when enlisted.";
            internal const string TCE_DbConnectionString_ColumnEncryptionSetting = @"Default column encryption setting for all the commands on the connection.";
            internal const string TCE_DbConnectionString_EnclaveAttestationUrl = @"Specifies an endpoint of an enclave attestation service, which will be used to verify whether the enclave, configured in the SQL Server instance for computations on database columns encrypted using Always Encrypted, is valid and secure.";
            internal const string TCE_DbConnectionString_AttestationProtocol = @"Specifies an attestation protocol for its corresponding enclave attestation service.";
            internal const string TCE_DbConnectionString_IPAddressPreference = @"Specifies an IP address preference when connecting to SQL instances.";
        }
    }
}
