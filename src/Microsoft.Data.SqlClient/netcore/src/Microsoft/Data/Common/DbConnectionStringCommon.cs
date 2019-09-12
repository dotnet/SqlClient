// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.Common
{
    internal static partial class DbConnectionStringBuilderUtil
    {
        internal static bool ConvertToBoolean(object value)
        {
            Debug.Assert(null != value, "ConvertToBoolean(null)");
            string svalue = (value as string);
            if (null != svalue)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(svalue, "true") || StringComparer.OrdinalIgnoreCase.Equals(svalue, "yes"))
                    return true;
                else if (StringComparer.OrdinalIgnoreCase.Equals(svalue, "false") || StringComparer.OrdinalIgnoreCase.Equals(svalue, "no"))
                    return false;
                else
                {
                    string tmp = svalue.Trim();  // Remove leading & trailing whitespace.
                    if (StringComparer.OrdinalIgnoreCase.Equals(tmp, "true") || StringComparer.OrdinalIgnoreCase.Equals(tmp, "yes"))
                        return true;
                    else if (StringComparer.OrdinalIgnoreCase.Equals(tmp, "false") || StringComparer.OrdinalIgnoreCase.Equals(tmp, "no"))
                        return false;
                }
                return bool.Parse(svalue);
            }
            try
            {
                return Convert.ToBoolean(value);
            }
            catch (InvalidCastException e)
            {
                throw ADP.ConvertFailed(value.GetType(), typeof(bool), e);
            }
        }

        internal static bool ConvertToIntegratedSecurity(object value)
        {
            Debug.Assert(null != value, "ConvertToIntegratedSecurity(null)");
            string svalue = (value as string);
            if (null != svalue)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(svalue, "sspi") || StringComparer.OrdinalIgnoreCase.Equals(svalue, "true") || StringComparer.OrdinalIgnoreCase.Equals(svalue, "yes"))
                    return true;
                else if (StringComparer.OrdinalIgnoreCase.Equals(svalue, "false") || StringComparer.OrdinalIgnoreCase.Equals(svalue, "no"))
                    return false;
                else
                {
                    string tmp = svalue.Trim();  // Remove leading & trailing whitespace.
                    if (StringComparer.OrdinalIgnoreCase.Equals(tmp, "sspi") || StringComparer.OrdinalIgnoreCase.Equals(tmp, "true") || StringComparer.OrdinalIgnoreCase.Equals(tmp, "yes"))
                        return true;
                    else if (StringComparer.OrdinalIgnoreCase.Equals(tmp, "false") || StringComparer.OrdinalIgnoreCase.Equals(tmp, "no"))
                        return false;
                }
                return bool.Parse(svalue);
            }
            try
            {
                return Convert.ToBoolean(value);
            }
            catch (InvalidCastException e)
            {
                throw ADP.ConvertFailed(value.GetType(), typeof(bool), e);
            }
        }

        internal static int ConvertToInt32(object value)
        {
            try
            {
                return Convert.ToInt32(value);
            }
            catch (InvalidCastException e)
            {
                throw ADP.ConvertFailed(value.GetType(), typeof(int), e);
            }
        }

        internal static string ConvertToString(object value)
        {
            try
            {
                return Convert.ToString(value);
            }
            catch (InvalidCastException e)
            {
                throw ADP.ConvertFailed(value.GetType(), typeof(string), e);
            }
        }

        private const string ApplicationIntentReadWriteString = "ReadWrite";
        private const string ApplicationIntentReadOnlyString = "ReadOnly";
        const string SqlPasswordString = "Sql Password";
        const string ActiveDirectoryPasswordString = "Active Directory Password";

        internal static bool TryConvertToAuthenticationType(string value, out SqlAuthenticationMethod result)
        {
            Debug.Assert(Enum.GetNames(typeof(SqlAuthenticationMethod)).Length == 5, "SqlAuthenticationMethod enum has changed, update needed");

            bool isSuccess = false;

            if (StringComparer.InvariantCultureIgnoreCase.Equals(value, SqlPasswordString)
                || StringComparer.InvariantCultureIgnoreCase.Equals(value, Convert.ToString(SqlAuthenticationMethod.SqlPassword, CultureInfo.InvariantCulture)))
            {
                result = SqlAuthenticationMethod.SqlPassword;
                isSuccess = true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ActiveDirectoryPasswordString)
                || StringComparer.InvariantCultureIgnoreCase.Equals(value, Convert.ToString(SqlAuthenticationMethod.ActiveDirectoryPassword, CultureInfo.InvariantCulture)))
            {
                result = SqlAuthenticationMethod.ActiveDirectoryPassword;
                isSuccess = true;
            }
            else
            {
                result = DbConnectionStringDefaults.Authentication;
            }
            return isSuccess;
        }

        internal static bool TryConvertToApplicationIntent(string value, out ApplicationIntent result)
        {
            Debug.Assert(Enum.GetNames(typeof(ApplicationIntent)).Length == 2, "ApplicationIntent enum has changed, update needed");
            Debug.Assert(null != value, "TryConvertToApplicationIntent(null,...)");

            if (StringComparer.OrdinalIgnoreCase.Equals(value, ApplicationIntentReadOnlyString))
            {
                result = ApplicationIntent.ReadOnly;
                return true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(value, ApplicationIntentReadWriteString))
            {
                result = ApplicationIntent.ReadWrite;
                return true;
            }
            else
            {
                result = DbConnectionStringDefaults.ApplicationIntent;
                return false;
            }
        }

        /// <summary>
        /// Column Encryption Setting.
        /// </summary>
        const string ColumnEncryptionSettingEnabledString = "Enabled";
        const string ColumnEncryptionSettingDisabledString = "Disabled";

        /// <summary>
        /// Convert a string value to the corresponding SqlConnectionColumnEncryptionSetting.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal static bool TryConvertToColumnEncryptionSetting(string value, out SqlConnectionColumnEncryptionSetting result)
        {
            bool isSuccess = false;

            if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ColumnEncryptionSettingEnabledString))
            {
                result = SqlConnectionColumnEncryptionSetting.Enabled;
                isSuccess = true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ColumnEncryptionSettingDisabledString))
            {
                result = SqlConnectionColumnEncryptionSetting.Disabled;
                isSuccess = true;
            }
            else
            {
                result = DbConnectionStringDefaults.ColumnEncryptionSetting;
            }

            return isSuccess;
        }

        /// <summary>
        /// Is it a valid connection level column encryption setting ?
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static bool IsValidColumnEncryptionSetting(SqlConnectionColumnEncryptionSetting value)
        {
            Debug.Assert(Enum.GetNames(typeof(SqlConnectionColumnEncryptionSetting)).Length == 2, "SqlConnectionColumnEncryptionSetting enum has changed, update needed");
            return value == SqlConnectionColumnEncryptionSetting.Enabled || value == SqlConnectionColumnEncryptionSetting.Disabled;
        }

        /// <summary>
        /// Convert connection level column encryption setting value to string.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static string ColumnEncryptionSettingToString(SqlConnectionColumnEncryptionSetting value)
        {
            Debug.Assert(IsValidColumnEncryptionSetting(value), "value is not a valid connection level column encryption setting.");

            switch (value)
            {
                case SqlConnectionColumnEncryptionSetting.Enabled:
                    return ColumnEncryptionSettingEnabledString;
                case SqlConnectionColumnEncryptionSetting.Disabled:
                    return ColumnEncryptionSettingDisabledString;

                default:
                    return null;
            }
        }


        internal static bool IsValidApplicationIntentValue(ApplicationIntent value)
        {
            Debug.Assert(Enum.GetNames(typeof(ApplicationIntent)).Length == 2, "ApplicationIntent enum has changed, update needed");
            return value == ApplicationIntent.ReadOnly || value == ApplicationIntent.ReadWrite;
        }

        internal static string ApplicationIntentToString(ApplicationIntent value)
        {
            Debug.Assert(IsValidApplicationIntentValue(value));
            if (value == ApplicationIntent.ReadOnly)
            {
                return ApplicationIntentReadOnlyString;
            }
            else
            {
                return ApplicationIntentReadWriteString;
            }
        }

        /// <summary>
        /// This method attempts to convert the given value tp ApplicationIntent enum. The algorithm is:
        /// * if the value is from type string, it will be matched against ApplicationIntent enum names only, using ordinal, case-insensitive comparer
        /// * if the value is from type ApplicationIntent, it will be used as is
        /// * if the value is from integral type (SByte, Int16, Int32, Int64, Byte, UInt16, UInt32, or UInt64), it will be converted to enum
        /// * if the value is another enum or any other type, it will be blocked with an appropriate ArgumentException
        /// 
        /// in any case above, if the converted value is out of valid range, the method raises ArgumentOutOfRangeException.
        /// </summary>
        /// <returns>application intent value in the valid range</returns>
        internal static ApplicationIntent ConvertToApplicationIntent(string keyword, object value)
        {
            Debug.Assert(null != value, "ConvertToApplicationIntent(null)");
            string sValue = (value as string);
            ApplicationIntent result;
            if (null != sValue)
            {
                // We could use Enum.TryParse<ApplicationIntent> here, but it accepts value combinations like
                // "ReadOnly, ReadWrite" which are unwelcome here
                // Also, Enum.TryParse is 100x slower than plain StringComparer.OrdinalIgnoreCase.Equals method.

                if (TryConvertToApplicationIntent(sValue, out result))
                {
                    return result;
                }

                // try again after remove leading & trailing whitespace.
                sValue = sValue.Trim();
                if (TryConvertToApplicationIntent(sValue, out result))
                {
                    return result;
                }

                // string values must be valid
                throw ADP.InvalidConnectionOptionValue(keyword);
            }
            else
            {
                // the value is not string, try other options
                ApplicationIntent eValue;

                if (value is ApplicationIntent)
                {
                    // quick path for the most common case
                    eValue = (ApplicationIntent)value;
                }
                else if (value.GetType().GetTypeInfo().IsEnum)
                {
                    // explicitly block scenarios in which user tries to use wrong enum types, like:
                    // builder["ApplicationIntent"] = EnvironmentVariableTarget.Process;
                    // workaround: explicitly cast non-ApplicationIntent enums to int
                    throw ADP.ConvertFailed(value.GetType(), typeof(ApplicationIntent), null);
                }
                else
                {
                    try
                    {
                        // Enum.ToObject allows only integral and enum values (enums are blocked above), raising ArgumentException for the rest
                        eValue = (ApplicationIntent)Enum.ToObject(typeof(ApplicationIntent), value);
                    }
                    catch (ArgumentException e)
                    {
                        // to be consistent with the messages we send in case of wrong type usage, replace 
                        // the error with our exception, and keep the original one as inner one for troubleshooting
                        throw ADP.ConvertFailed(value.GetType(), typeof(ApplicationIntent), e);
                    }
                }

                // ensure value is in valid range
                if (IsValidApplicationIntentValue(eValue))
                {
                    return eValue;
                }
                else
                {
                    throw ADP.InvalidEnumerationValue(typeof(ApplicationIntent), (int)eValue);
                }
            }
        }

        internal static bool IsValidAuthenticationTypeValue(SqlAuthenticationMethod value)
        {
            Debug.Assert(Enum.GetNames(typeof(SqlAuthenticationMethod)).Length == 5, "SqlAuthenticationMethod enum has changed, update needed");
            return value == SqlAuthenticationMethod.SqlPassword
                || value == SqlAuthenticationMethod.ActiveDirectoryPassword
                || value == SqlAuthenticationMethod.ActiveDirectoryIntegrated
                || value == SqlAuthenticationMethod.ActiveDirectoryInteractive
                || value == SqlAuthenticationMethod.NotSpecified;
        }

        internal static string AuthenticationTypeToString(SqlAuthenticationMethod value)
        {
            Debug.Assert(IsValidAuthenticationTypeValue(value));

            switch (value)
            {
                case SqlAuthenticationMethod.SqlPassword:
                    return SqlPasswordString;
                case SqlAuthenticationMethod.ActiveDirectoryPassword:
                    return ActiveDirectoryPasswordString;
                default:
                    return null;
            }
        }

        internal static SqlAuthenticationMethod ConvertToAuthenticationType(string keyword, object value)
        {
            if (null == value)
            {
                return DbConnectionStringDefaults.Authentication;
            }

            string sValue = (value as string);
            SqlAuthenticationMethod result;
            if (null != sValue)
            {
                if (TryConvertToAuthenticationType(sValue, out result))
                {
                    return result;
                }

                // try again after remove leading & trailing whitespaces.
                sValue = sValue.Trim();
                if (TryConvertToAuthenticationType(sValue, out result))
                {
                    return result;
                }

                // string values must be valid
                throw ADP.InvalidConnectionOptionValue(keyword);
            }
            else
            {
                // the value is not string, try other options
                SqlAuthenticationMethod eValue;

                if (value is SqlAuthenticationMethod)
                {
                    // quick path for the most common case
                    eValue = (SqlAuthenticationMethod)value;
                }
                else if (value.GetType().IsEnum)
                {
                    // explicitly block scenarios in which user tries to use wrong enum types, like:
                    // builder["ApplicationIntent"] = EnvironmentVariableTarget.Process;
                    // workaround: explicitly cast non-ApplicationIntent enums to int
                    throw ADP.ConvertFailed(value.GetType(), typeof(SqlAuthenticationMethod), null);
                }
                else
                {
                    try
                    {
                        // Enum.ToObject allows only integral and enum values (enums are blocked above), rasing ArgumentException for the rest
                        eValue = (SqlAuthenticationMethod)Enum.ToObject(typeof(SqlAuthenticationMethod), value);
                    }
                    catch (ArgumentException e)
                    {
                        // to be consistent with the messages we send in case of wrong type usage, replace 
                        // the error with our exception, and keep the original one as inner one for troubleshooting
                        throw ADP.ConvertFailed(value.GetType(), typeof(SqlAuthenticationMethod), e);
                    }
                }

                // ensure value is in valid range
                if (IsValidAuthenticationTypeValue(eValue))
                {
                    return eValue;
                }
                else
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlAuthenticationMethod), (int)eValue);
                }
            }
        }

        /// <summary>
        /// Convert the provided value to a SqlConnectionColumnEncryptionSetting.
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static SqlConnectionColumnEncryptionSetting ConvertToColumnEncryptionSetting(string keyword, object value)
        {
            if (null == value)
            {
                return DbConnectionStringDefaults.ColumnEncryptionSetting;
            }

            string sValue = (value as string);
            SqlConnectionColumnEncryptionSetting result;
            if (null != sValue)
            {
                if (TryConvertToColumnEncryptionSetting(sValue, out result))
                {
                    return result;
                }

                // try again after remove leading & trailing whitespaces.
                sValue = sValue.Trim();
                if (TryConvertToColumnEncryptionSetting(sValue, out result))
                {
                    return result;
                }

                // string values must be valid
                throw ADP.InvalidConnectionOptionValue(keyword);
            }
            else
            {
                // the value is not string, try other options
                SqlConnectionColumnEncryptionSetting eValue;

                if (value is SqlConnectionColumnEncryptionSetting)
                {
                    // quick path for the most common case
                    eValue = (SqlConnectionColumnEncryptionSetting)value;
                }
                else if (value.GetType().IsEnum)
                {
                    // explicitly block scenarios in which user tries to use wrong enum types, like:
                    // builder["SqlConnectionColumnEncryptionSetting"] = EnvironmentVariableTarget.Process;
                    // workaround: explicitly cast non-SqlConnectionColumnEncryptionSetting enums to int
                    throw ADP.ConvertFailed(value.GetType(), typeof(SqlConnectionColumnEncryptionSetting), null);
                }
                else
                {
                    try
                    {
                        // Enum.ToObject allows only integral and enum values (enums are blocked above), rasing ArgumentException for the rest
                        eValue = (SqlConnectionColumnEncryptionSetting)Enum.ToObject(typeof(SqlConnectionColumnEncryptionSetting), value);
                    }
                    catch (ArgumentException e)
                    {
                        // to be consistent with the messages we send in case of wrong type usage, replace 
                        // the error with our exception, and keep the original one as inner one for troubleshooting
                        throw ADP.ConvertFailed(value.GetType(), typeof(SqlConnectionColumnEncryptionSetting), e);
                    }
                }

                // ensure value is in valid range
                if (IsValidColumnEncryptionSetting(eValue))
                {
                    return eValue;
                }
                else
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlConnectionColumnEncryptionSetting), (int)eValue);
                }
            }
        }
    }

    internal static partial class DbConnectionStringDefaults
    {
        // all
        // internal const string NamedConnection = "";

        // SqlClient
        internal const ApplicationIntent ApplicationIntent = Microsoft.Data.SqlClient.ApplicationIntent.ReadWrite;
        internal const string ApplicationName = "Core Microsoft SqlClient Data Provider";
        internal const string AttachDBFilename = "";
        internal const int ConnectTimeout = 15;
        internal const string CurrentLanguage = "";
        internal const string DataSource = "";
        internal const bool Encrypt = false;
        internal const bool Enlist = true;
        internal const string FailoverPartner = "";
        internal const string InitialCatalog = "";
        internal const bool IntegratedSecurity = false;
        internal const int LoadBalanceTimeout = 0; // default of 0 means don't use
        internal const bool MultipleActiveResultSets = false;
        internal const bool MultiSubnetFailover = false;
        internal const int MaxPoolSize = 100;
        internal const int MinPoolSize = 0;
        internal const int PacketSize = 8000;
        internal const string Password = "";
        internal const bool PersistSecurityInfo = false;
        internal const bool Pooling = true;
        internal const bool TrustServerCertificate = false;
        internal const string TypeSystemVersion = "Latest";
        internal const string UserID = "";
        internal const bool UserInstance = false;
        internal const bool Replication = false;
        internal const string WorkstationID = "";
        internal const string TransactionBinding = "Implicit Unbind";
        internal const int ConnectRetryCount = 1;
        internal const int ConnectRetryInterval = 10;
        internal static readonly SqlAuthenticationMethod Authentication = SqlAuthenticationMethod.NotSpecified;
        internal const SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Disabled;
        internal const string EnclaveAttestationUrl = "";
    }


    internal static partial class DbConnectionStringKeywords
    {
        // all
        // internal const string NamedConnection = "Named Connection";

        // SqlClient
        internal const string ApplicationIntent = "ApplicationIntent";
        internal const string ApplicationName = "Application Name";
        internal const string AsynchronousProcessing = "Asynchronous Processing";
        internal const string AttachDBFilename = "AttachDbFilename";
        internal const string ConnectTimeout = "Connect Timeout";
        internal const string ConnectionReset = "Connection Reset";
        internal const string ContextConnection = "Context Connection";
        internal const string CurrentLanguage = "Current Language";
        internal const string Encrypt = "Encrypt";
        internal const string FailoverPartner = "Failover Partner";
        internal const string InitialCatalog = "Initial Catalog";
        internal const string MultipleActiveResultSets = "MultipleActiveResultSets";
        internal const string MultiSubnetFailover = "MultiSubnetFailover";
        internal const string NetworkLibrary = "Network Library";
        internal const string PacketSize = "Packet Size";
        internal const string Replication = "Replication";
        internal const string TransactionBinding = "Transaction Binding";
        internal const string TrustServerCertificate = "TrustServerCertificate";
        internal const string TypeSystemVersion = "Type System Version";
        internal const string UserInstance = "User Instance";
        internal const string WorkstationID = "Workstation ID";
        internal const string ConnectRetryCount = "ConnectRetryCount";
        internal const string ConnectRetryInterval = "ConnectRetryInterval";
        internal const string Authentication = "Authentication";
        internal const string ColumnEncryptionSetting = "Column Encryption Setting";
        internal const string EnclaveAttestationUrl = "Enclave Attestation Url";

        // common keywords (OleDb, OracleClient, SqlClient)
        internal const string DataSource = "Data Source";
        internal const string IntegratedSecurity = "Integrated Security";
        internal const string Password = "Password";
        internal const string Driver = "Driver";
        internal const string PersistSecurityInfo = "Persist Security Info";
        internal const string UserID = "User ID";

        // managed pooling (OracleClient, SqlClient)
        internal const string Enlist = "Enlist";
        internal const string LoadBalanceTimeout = "Load Balance Timeout";
        internal const string MaxPoolSize = "Max Pool Size";
        internal const string Pooling = "Pooling";
        internal const string MinPoolSize = "Min Pool Size";
#if netcoreapp
        internal const string PoolBlockingPeriod = "PoolBlockingPeriod";
#endif
    }

    internal static class DbConnectionStringSynonyms
    {
        //internal const string AsynchronousProcessing = Async;
        internal const string Async = "async";

        //internal const string ApplicationName        = APP;
        internal const string APP = "app";

        //internal const string AttachDBFilename       = EXTENDEDPROPERTIES+","+INITIALFILENAME;
        internal const string EXTENDEDPROPERTIES = "extended properties";
        internal const string INITIALFILENAME = "initial file name";

        //internal const string ConnectTimeout         = CONNECTIONTIMEOUT+","+TIMEOUT;
        internal const string CONNECTIONTIMEOUT = "connection timeout";
        internal const string TIMEOUT = "timeout";

        //internal const string CurrentLanguage        = LANGUAGE;
        internal const string LANGUAGE = "language";

        //internal const string OraDataSource          = SERVER;
        //internal const string SqlDataSource          = ADDR+","+ADDRESS+","+SERVER+","+NETWORKADDRESS;
        internal const string ADDR = "addr";
        internal const string ADDRESS = "address";
        internal const string SERVER = "server";
        internal const string NETWORKADDRESS = "network address";

        //internal const string InitialCatalog         = DATABASE;
        internal const string DATABASE = "database";

        //internal const string IntegratedSecurity     = TRUSTEDCONNECTION;
        internal const string TRUSTEDCONNECTION = "trusted_connection"; // underscore introduced in Everett

        //internal const string LoadBalanceTimeout     = ConnectionLifetime;
        internal const string ConnectionLifetime = "connection lifetime";

        //internal const string NetworkLibrary         = NET+","+NETWORK;
        internal const string NET = "net";
        internal const string NETWORK = "network";


        //internal const string Password               = Pwd;
        internal const string Pwd = "pwd";

        //internal const string PersistSecurityInfo    = PERSISTSECURITYINFO;
        internal const string PERSISTSECURITYINFO = "persistsecurityinfo";

        //internal const string UserID                 = UID+","+User;
        internal const string UID = "uid";
        internal const string User = "user";

        //internal const string WorkstationID          = WSID;
        internal const string WSID = "wsid";
    }
}
