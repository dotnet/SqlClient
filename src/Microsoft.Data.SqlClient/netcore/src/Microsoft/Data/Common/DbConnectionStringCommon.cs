// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        const string ActiveDirectoryIntegratedString = "Active Directory Integrated";
        const string ActiveDirectoryInteractiveString = "Active Directory Interactive";
        const string ActiveDirectoryServicePrincipalString = "Active Directory Service Principal";
        const string ActiveDirectoryDeviceCodeFlowString = "Active Directory Device Code Flow";
        internal const string ActiveDirectoryManagedIdentityString = "Active Directory Managed Identity";
        internal const string ActiveDirectoryMSIString = "Active Directory MSI";
        internal const string ActiveDirectoryDefaultString = "Active Directory Default";

#if DEBUG
        private static string[] s_supportedAuthenticationModes =
        {
            "NotSpecified",
            "SqlPassword",
            "ActiveDirectoryPassword",
            "ActiveDirectoryIntegrated",
            "ActiveDirectoryInteractive",
            "ActiveDirectoryServicePrincipal",
            "ActiveDirectoryDeviceCodeFlow",
            "ActiveDirectoryManagedIdentity",
            "ActiveDirectoryMSI",
            "ActiveDirectoryDefault"
        };

        private static bool IsValidAuthenticationMethodEnum()
        {
            string[] names = Enum.GetNames(typeof(SqlAuthenticationMethod));
            int l = s_supportedAuthenticationModes.Length;
            bool listValid;
            if (listValid = names.Length == l)
            {
                for (int i = 0; i < l; i++)
                {
                    if (s_supportedAuthenticationModes[i].CompareTo(names[i]) != 0)
                    {
                        listValid = false;
                    }
                }
            }
            return listValid;
        }
#endif

        internal static bool TryConvertToAuthenticationType(string value, out SqlAuthenticationMethod result)
        {
#if DEBUG
            Debug.Assert(IsValidAuthenticationMethodEnum(), "SqlAuthenticationMethod enum has changed, update needed");
#endif
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
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ActiveDirectoryIntegratedString)
                || StringComparer.InvariantCultureIgnoreCase.Equals(value, Convert.ToString(SqlAuthenticationMethod.ActiveDirectoryIntegrated, CultureInfo.InvariantCulture)))
            {
                result = SqlAuthenticationMethod.ActiveDirectoryIntegrated;
                isSuccess = true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ActiveDirectoryInteractiveString)
                || StringComparer.InvariantCultureIgnoreCase.Equals(value, Convert.ToString(SqlAuthenticationMethod.ActiveDirectoryInteractive, CultureInfo.InvariantCulture)))
            {
                result = SqlAuthenticationMethod.ActiveDirectoryInteractive;
                isSuccess = true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ActiveDirectoryServicePrincipalString)
                || StringComparer.InvariantCultureIgnoreCase.Equals(value, Convert.ToString(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, CultureInfo.InvariantCulture)))
            {
                result = SqlAuthenticationMethod.ActiveDirectoryServicePrincipal;
                isSuccess = true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ActiveDirectoryDeviceCodeFlowString)
                || StringComparer.InvariantCultureIgnoreCase.Equals(value, Convert.ToString(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, CultureInfo.InvariantCulture)))
            {
                result = SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;
                isSuccess = true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ActiveDirectoryManagedIdentityString)
                || StringComparer.InvariantCultureIgnoreCase.Equals(value, Convert.ToString(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, CultureInfo.InvariantCulture)))
            {
                result = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
                isSuccess = true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ActiveDirectoryMSIString)
                || StringComparer.InvariantCultureIgnoreCase.Equals(value, Convert.ToString(SqlAuthenticationMethod.ActiveDirectoryMSI, CultureInfo.InvariantCulture)))
            {
                result = SqlAuthenticationMethod.ActiveDirectoryMSI;
                isSuccess = true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, ActiveDirectoryDefaultString)
                || StringComparer.InvariantCultureIgnoreCase.Equals(value, Convert.ToString(SqlAuthenticationMethod.ActiveDirectoryDefault, CultureInfo.InvariantCulture)))
            {
                result = SqlAuthenticationMethod.ActiveDirectoryDefault;
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

        #region <<AttestationProtocol Utility>>

        /// <summary>
        /// Attestation Protocol.
        /// </summary>
        const string AttestationProtocolHGS = "HGS";
        const string AttestationProtocolAAS = "AAS";
#if ENCLAVE_SIMULATOR
        const string AttestationProtocolSIM = "SIM";
#endif

        /// <summary>
        ///  Convert a string value to the corresponding SqlConnectionAttestationProtocol
        /// </summary>
        /// <param name="value"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal static bool TryConvertToAttestationProtocol(string value, out SqlConnectionAttestationProtocol result)
        {
            if (StringComparer.InvariantCultureIgnoreCase.Equals(value, AttestationProtocolHGS))
            {
                result = SqlConnectionAttestationProtocol.HGS;
                return true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, AttestationProtocolAAS))
            {
                result = SqlConnectionAttestationProtocol.AAS;
                return true;
            }
#if ENCLAVE_SIMULATOR
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, AttestationProtocolSIM))
            {
                result = SqlConnectionAttestationProtocol.SIM;
                return true;
            }
#endif
            else
            {
                result = DbConnectionStringDefaults.AttestationProtocol;
                return false;
            }
        }

        internal static bool IsValidAttestationProtocol(SqlConnectionAttestationProtocol value)
        {
#if ENCLAVE_SIMULATOR
            Debug.Assert(Enum.GetNames(typeof(SqlConnectionAttestationProtocol)).Length == 4, "SqlConnectionAttestationProtocol enum has changed, update needed");
            return value == SqlConnectionAttestationProtocol.NotSpecified
                || value == SqlConnectionAttestationProtocol.HGS
                || value == SqlConnectionAttestationProtocol.AAS
                || value == SqlConnectionAttestationProtocol.SIM;
#else
            Debug.Assert(Enum.GetNames(typeof(SqlConnectionAttestationProtocol)).Length == 3, "SqlConnectionAttestationProtocol enum has changed, update needed");
            return value == SqlConnectionAttestationProtocol.NotSpecified
                || value == SqlConnectionAttestationProtocol.HGS
                || value == SqlConnectionAttestationProtocol.AAS;
#endif
        }

        internal static string AttestationProtocolToString(SqlConnectionAttestationProtocol value)
        {
            Debug.Assert(IsValidAttestationProtocol(value), "value is not a valid attestation protocol");

            switch (value)
            {
                case SqlConnectionAttestationProtocol.HGS:
                    return AttestationProtocolHGS;
                case SqlConnectionAttestationProtocol.AAS:
                    return AttestationProtocolAAS;
#if ENCLAVE_SIMULATOR
                case SqlConnectionAttestationProtocol.SIM:
                    return AttestationProtocolSIM;
#endif
                default:
                    return null;
            }
        }

        internal static SqlConnectionAttestationProtocol ConvertToAttestationProtocol(string keyword, object value)
        {
            if (null == value)
            {
                return DbConnectionStringDefaults.AttestationProtocol;
            }

            string sValue = (value as string);
            SqlConnectionAttestationProtocol result;

            if (null != sValue)
            {
                // try again after remove leading & trailing whitespaces.
                sValue = sValue.Trim();
                if (TryConvertToAttestationProtocol(sValue, out result))
                {
                    return result;
                }

                // string values must be valid
                throw ADP.InvalidConnectionOptionValue(keyword);
            }
            else
            {
                // the value is not string, try other options
                SqlConnectionAttestationProtocol eValue;

                if (value is SqlConnectionAttestationProtocol)
                {
                    eValue = (SqlConnectionAttestationProtocol)value;
                }
                else if (value.GetType().IsEnum)
                {
                    // explicitly block scenarios in which user tries to use wrong enum types, like:
                    // builder["SqlConnectionAttestationProtocol"] = EnvironmentVariableTarget.Process;
                    // workaround: explicitly cast non-SqlConnectionAttestationProtocol enums to int
                    throw ADP.ConvertFailed(value.GetType(), typeof(SqlConnectionAttestationProtocol), null);
                }
                else
                {
                    try
                    {
                        // Enum.ToObject allows only integral and enum values (enums are blocked above), raising ArgumentException for the rest
                        eValue = (SqlConnectionAttestationProtocol)Enum.ToObject(typeof(SqlConnectionAttestationProtocol), value);
                    }
                    catch (ArgumentException e)
                    {
                        // to be consistent with the messages we send in case of wrong type usage, replace
                        // the error with our exception, and keep the original one as inner one for troubleshooting
                        throw ADP.ConvertFailed(value.GetType(), typeof(SqlConnectionAttestationProtocol), e);
                    }
                }

                if (IsValidAttestationProtocol(eValue))
                {
                    return eValue;
                }
                else
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlConnectionAttestationProtocol), (int)eValue);
                }
            }
        }

        #endregion

        #region <<IPAddressPreference Utility>>
        /// <summary>
        /// IP Address Preference.
        /// </summary>
        private readonly static Dictionary<string, SqlConnectionIPAddressPreference> s_preferenceNames = new(StringComparer.InvariantCultureIgnoreCase);

        static DbConnectionStringBuilderUtil()
        {
            foreach (SqlConnectionIPAddressPreference item in Enum.GetValues(typeof(SqlConnectionIPAddressPreference)))
            {
                s_preferenceNames.Add(item.ToString(), item);
            }
        }

        /// <summary>
        ///  Convert a string value to the corresponding IPAddressPreference.
        /// </summary>
        /// <param name="value">The string representation of the enumeration name to convert.</param>
        /// <param name="result">When this method returns, `result` contains an object of type `SqlConnectionIPAddressPreference` whose value is represented by `value` if the operation succeeds. 
        /// If the parse operation fails, `result` contains the default value of the `SqlConnectionIPAddressPreference` type.</param>
        /// <returns>`true` if the value parameter was converted successfully; otherwise, `false`.</returns>
        internal static bool TryConvertToIPAddressPreference(string value, out SqlConnectionIPAddressPreference result)
        {
            if (!s_preferenceNames.TryGetValue(value, out result))
            {
                result = DbConnectionStringDefaults.IPAddressPreference;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Verifies if the `value` is defined in the expected Enum.
        /// </summary>
        internal static bool IsValidIPAddressPreference(SqlConnectionIPAddressPreference value)
            => value == SqlConnectionIPAddressPreference.IPv4First
                || value == SqlConnectionIPAddressPreference.IPv6First
                || value == SqlConnectionIPAddressPreference.UsePlatformDefault;

        internal static string IPAddressPreferenceToString(SqlConnectionIPAddressPreference value)
            => Enum.GetName(typeof(SqlConnectionIPAddressPreference), value);

        internal static SqlConnectionIPAddressPreference ConvertToIPAddressPreference(string keyword, object value)
        {
            if (value is null)
            {
                return DbConnectionStringDefaults.IPAddressPreference;  // IPv4First
            }

            if (value is string sValue)
            {
                // try again after remove leading & trailing whitespaces.
                sValue = sValue.Trim();
                if (TryConvertToIPAddressPreference(sValue, out SqlConnectionIPAddressPreference result))
                {
                    return result;
                }

                // string values must be valid
                throw ADP.InvalidConnectionOptionValue(keyword);
            }
            else
            {
                // the value is not string, try other options
                SqlConnectionIPAddressPreference eValue;

                if (value is SqlConnectionIPAddressPreference preference)
                {
                    eValue = preference;
                }
                else if (value.GetType().IsEnum)
                {
                    // explicitly block scenarios in which user tries to use wrong enum types, like:
                    // builder["SqlConnectionIPAddressPreference"] = EnvironmentVariableTarget.Process;
                    // workaround: explicitly cast non-SqlConnectionIPAddressPreference enums to int
                    throw ADP.ConvertFailed(value.GetType(), typeof(SqlConnectionIPAddressPreference), null);
                }
                else
                {
                    try
                    {
                        // Enum.ToObject allows only integral and enum values (enums are blocked above), raising ArgumentException for the rest
                        eValue = (SqlConnectionIPAddressPreference)Enum.ToObject(typeof(SqlConnectionIPAddressPreference), value);
                    }
                    catch (ArgumentException e)
                    {
                        // to be consistent with the messages we send in case of wrong type usage, replace
                        // the error with our exception, and keep the original one as inner one for troubleshooting
                        throw ADP.ConvertFailed(value.GetType(), typeof(SqlConnectionIPAddressPreference), e);
                    }
                }

                if (IsValidIPAddressPreference(eValue))
                {
                    return eValue;
                }
                else
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlConnectionIPAddressPreference), (int)eValue);
                }
            }
        }
        #endregion

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
            Debug.Assert(Enum.GetNames(typeof(SqlAuthenticationMethod)).Length == 10, "SqlAuthenticationMethod enum has changed, update needed");
            return value == SqlAuthenticationMethod.SqlPassword
                || value == SqlAuthenticationMethod.ActiveDirectoryPassword
                || value == SqlAuthenticationMethod.ActiveDirectoryIntegrated
                || value == SqlAuthenticationMethod.ActiveDirectoryInteractive
                || value == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
                || value == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                || value == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                || value == SqlAuthenticationMethod.ActiveDirectoryMSI
                || value == SqlAuthenticationMethod.ActiveDirectoryDefault
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
                case SqlAuthenticationMethod.ActiveDirectoryIntegrated:
                    return ActiveDirectoryIntegratedString;
                case SqlAuthenticationMethod.ActiveDirectoryInteractive:
                    return ActiveDirectoryInteractiveString;
                case SqlAuthenticationMethod.ActiveDirectoryServicePrincipal:
                    return ActiveDirectoryServicePrincipalString;
                case SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow:
                    return ActiveDirectoryDeviceCodeFlowString;
                case SqlAuthenticationMethod.ActiveDirectoryManagedIdentity:
                    return ActiveDirectoryManagedIdentityString;
                case SqlAuthenticationMethod.ActiveDirectoryMSI:
                    return ActiveDirectoryMSIString;
                case SqlAuthenticationMethod.ActiveDirectoryDefault:
                    return ActiveDirectoryDefaultString;
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
                        // Enum.ToObject allows only integral and enum values (enums are blocked above), raising ArgumentException for the rest
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
                        // Enum.ToObject allows only integral and enum values (enums are blocked above), raising ArgumentException for the rest
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

        #region <<PoolBlockingPeriod Utility>>
        internal static bool TryConvertToPoolBlockingPeriod(string value, out PoolBlockingPeriod result)
        {
            Debug.Assert(Enum.GetNames(typeof(PoolBlockingPeriod)).Length == 3, "PoolBlockingPeriod enum has changed, update needed");
            Debug.Assert(null != value, "TryConvertToPoolBlockingPeriod(null,...)");

            if (StringComparer.OrdinalIgnoreCase.Equals(value, nameof(PoolBlockingPeriod.Auto)))
            {
                result = PoolBlockingPeriod.Auto;
                return true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(value, nameof(PoolBlockingPeriod.AlwaysBlock)))
            {
                result = PoolBlockingPeriod.AlwaysBlock;
                return true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(value, nameof(PoolBlockingPeriod.NeverBlock)))
            {
                result = PoolBlockingPeriod.NeverBlock;
                return true;
            }
            else
            {
                result = DbConnectionStringDefaults.PoolBlockingPeriod;
                return false;
            }
        }

        internal static bool IsValidPoolBlockingPeriodValue(PoolBlockingPeriod value)
        {
            Debug.Assert(Enum.GetNames(typeof(PoolBlockingPeriod)).Length == 3, "PoolBlockingPeriod enum has changed, update needed");
            return (uint)value <= (uint)PoolBlockingPeriod.NeverBlock;
        }

        internal static string PoolBlockingPeriodToString(PoolBlockingPeriod value)
        {
            Debug.Assert(IsValidPoolBlockingPeriodValue(value));

            switch (value)
            {
                case PoolBlockingPeriod.AlwaysBlock:
                    return nameof(PoolBlockingPeriod.AlwaysBlock);
                case PoolBlockingPeriod.NeverBlock:
                    return nameof(PoolBlockingPeriod.NeverBlock);
                default:
                    return nameof(PoolBlockingPeriod.Auto);
            }
        }

        /// <summary>
        /// This method attempts to convert the given value to a PoolBlockingPeriod enum. The algorithm is:
        /// * if the value is from type string, it will be matched against PoolBlockingPeriod enum names only, using ordinal, case-insensitive comparer
        /// * if the value is from type PoolBlockingPeriod, it will be used as is
        /// * if the value is from integral type (SByte, Int16, Int32, Int64, Byte, UInt16, UInt32, or UInt64), it will be converted to enum
        /// * if the value is another enum or any other type, it will be blocked with an appropriate ArgumentException
        /// 
        /// in any case above, if the converted value is out of valid range, the method raises ArgumentOutOfRangeException.
        /// </summary>
        /// <returns>PoolBlockingPeriod value in the valid range</returns>
        internal static PoolBlockingPeriod ConvertToPoolBlockingPeriod(string keyword, object value)
        {
            Debug.Assert(null != value, "ConvertToPoolBlockingPeriod(null)");
            string sValue = (value as string);
            PoolBlockingPeriod result;
            if (null != sValue)
            {
                // We could use Enum.TryParse<PoolBlockingPeriod> here, but it accepts value combinations like
                // "ReadOnly, ReadWrite" which are unwelcome here
                // Also, Enum.TryParse is 100x slower than plain StringComparer.OrdinalIgnoreCase.Equals method.
                if (TryConvertToPoolBlockingPeriod(sValue, out result))
                {
                    return result;
                }

                // try again after remove leading & trailing whitespaces.
                sValue = sValue.Trim();
                if (TryConvertToPoolBlockingPeriod(sValue, out result))
                {
                    return result;
                }

                // string values must be valid
                throw ADP.InvalidConnectionOptionValue(keyword);
            }
            else
            {
                // the value is not string, try other options
                PoolBlockingPeriod eValue;

                if (value is PoolBlockingPeriod)
                {
                    // quick path for the most common case
                    eValue = (PoolBlockingPeriod)value;
                }
                else if (value.GetType().IsEnum)
                {
                    // explicitly block scenarios in which user tries to use wrong enum types, like:
                    // builder["PoolBlockingPeriod"] = EnvironmentVariableTarget.Process;
                    // workaround: explicitly cast non-PoolBlockingPeriod enums to int
                    throw ADP.ConvertFailed(value.GetType(), typeof(PoolBlockingPeriod), null);
                }
                else
                {
                    try
                    {
                        // Enum.ToObject allows only integral and enum values (enums are blocked above), raising ArgumentException for the rest
                        eValue = (PoolBlockingPeriod)Enum.ToObject(typeof(PoolBlockingPeriod), value);
                    }
                    catch (ArgumentException e)
                    {
                        // to be consistent with the messages we send in case of wrong type usage, replace 
                        // the error with our exception, and keep the original one as inner one for troubleshooting
                        throw ADP.ConvertFailed(value.GetType(), typeof(PoolBlockingPeriod), e);
                    }
                }

                // ensure value is in valid range
                if (IsValidPoolBlockingPeriodValue(eValue))
                {
                    return eValue;
                }
                else
                {
                    throw ADP.InvalidEnumerationValue(typeof(ApplicationIntent), (int)eValue);
                }
            }
        }
        #endregion
    }

    internal static partial class DbConnectionStringDefaults
    {
        // all
        // internal const string NamedConnection = "";

        private const string _emptyString = "";
        // SqlClient
        internal const ApplicationIntent ApplicationIntent = Microsoft.Data.SqlClient.ApplicationIntent.ReadWrite;
        internal const string ApplicationName = "Core Microsoft SqlClient Data Provider";
        internal const string AttachDBFilename = _emptyString;
        internal const int CommandTimeout = 30;
        internal const int ConnectTimeout = 15;
        internal const string CurrentLanguage = _emptyString;
        internal const string DataSource = _emptyString;
        internal const bool Encrypt = false;
        internal const bool Enlist = true;
        internal const string FailoverPartner = _emptyString;
        internal const string InitialCatalog = _emptyString;
        internal const bool IntegratedSecurity = false;
        internal const int LoadBalanceTimeout = 0; // default of 0 means don't use
        internal const bool MultipleActiveResultSets = false;
        internal const bool MultiSubnetFailover = false;
        internal const int MaxPoolSize = 100;
        internal const int MinPoolSize = 0;
        internal const int PacketSize = 8000;
        internal const string Password = _emptyString;
        internal const bool PersistSecurityInfo = false;
        internal const bool Pooling = true;
        internal const bool TrustServerCertificate = false;
        internal const string TypeSystemVersion = "Latest";
        internal const string UserID = _emptyString;
        internal const bool UserInstance = false;
        internal const bool Replication = false;
        internal const string WorkstationID = _emptyString;
        internal const string TransactionBinding = "Implicit Unbind";
        internal const int ConnectRetryCount = 1;
        internal const int ConnectRetryInterval = 10;
        internal const PoolBlockingPeriod PoolBlockingPeriod = Microsoft.Data.SqlClient.PoolBlockingPeriod.Auto;
        internal static readonly SqlAuthenticationMethod Authentication = SqlAuthenticationMethod.NotSpecified;
        internal const SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Disabled;
        internal const string EnclaveAttestationUrl = _emptyString;
        internal const SqlConnectionAttestationProtocol AttestationProtocol = SqlConnectionAttestationProtocol.NotSpecified;
        internal const SqlConnectionIPAddressPreference IPAddressPreference = SqlConnectionIPAddressPreference.IPv4First;
    }

    internal static partial class DbConnectionStringKeywords
    {
        // all
        // internal const string NamedConnection = "Named Connection";

        // SqlClient
        internal const string ApplicationIntent = "Application Intent";
        internal const string ApplicationName = "Application Name";
        internal const string AttachDBFilename = "AttachDbFilename";
        internal const string CommandTimeout = "Command Timeout";
        internal const string ConnectTimeout = "Connect Timeout";
        internal const string ConnectionReset = "Connection Reset";
        internal const string ContextConnection = "Context Connection";
        internal const string CurrentLanguage = "Current Language";
        internal const string Encrypt = "Encrypt";
        internal const string FailoverPartner = "Failover Partner";
        internal const string InitialCatalog = "Initial Catalog";
        internal const string MultipleActiveResultSets = "Multiple Active Result Sets";
        internal const string MultiSubnetFailover = "Multi Subnet Failover";
        internal const string NetworkLibrary = "Network Library";
        internal const string PacketSize = "Packet Size";
        internal const string Replication = "Replication";
        internal const string TransactionBinding = "Transaction Binding";
        internal const string TrustServerCertificate = "Trust Server Certificate";
        internal const string TypeSystemVersion = "Type System Version";
        internal const string UserInstance = "User Instance";
        internal const string WorkstationID = "Workstation ID";
        internal const string ConnectRetryCount = "Connect Retry Count";
        internal const string ConnectRetryInterval = "Connect Retry Interval";
        internal const string Authentication = "Authentication";
        internal const string ColumnEncryptionSetting = "Column Encryption Setting";
        internal const string EnclaveAttestationUrl = "Enclave Attestation Url";
        internal const string AttestationProtocol = "Attestation Protocol";
        internal const string IPAddressPreference = "IP Address Preference";

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
        internal const string PoolBlockingPeriod = "Pool Blocking Period";
    }

    internal static class DbConnectionStringSynonyms
    {
        //internal const string ApplicationName        = APP;
        internal const string APP = "app";

        // internal const string IPAddressPreference = IPADDRESSPREFERENCE;
        internal const string IPADDRESSPREFERENCE = "IPAddressPreference";

        //internal const string ApplicationIntent    = APPLICATIONINTENT;
        internal const string APPLICATIONINTENT = "ApplicationIntent";

        //internal const string AttachDBFilename       = EXTENDEDPROPERTIES+","+INITIALFILENAME;
        internal const string EXTENDEDPROPERTIES = "extended properties";
        internal const string INITIALFILENAME = "initial file name";

        //internal const string ConnectTimeout         = CONNECTIONTIMEOUT+","+TIMEOUT;
        internal const string CONNECTIONTIMEOUT = "connection timeout";
        internal const string TIMEOUT = "timeout";

        //internal const string ConnectRetryCount = CONNECTRETRYCOUNT;
        internal const string CONNECTRETRYCOUNT = "ConnectRetryCount";

        //internal const string ConnectRetryInterval = CONNECTRETRYINTERVAL;
        internal const string CONNECTRETRYINTERVAL = "ConnectRetryInterval";

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

        //internal const string MultipleActiveResultSets    = MULTIPLEACTIVERESULTSETS;
        internal const string MULTIPLEACTIVERESULTSETS = "MultipleActiveResultSets";

        //internal const string MultiSubnetFailover = MULTISUBNETFAILOVER;
        internal const string MULTISUBNETFAILOVER = "MultiSubnetFailover";

        //internal const string NetworkLibrary         = NET+","+NETWORK;
        internal const string NET = "net";
        internal const string NETWORK = "network";

        //internal const string PoolBlockingPeriod = POOLBLOCKINGPERIOD;
        internal const string POOLBLOCKINGPERIOD = "PoolBlockingPeriod";

        //internal const string Password               = Pwd;
        internal const string Pwd = "pwd";

        //internal const string PersistSecurityInfo    = PERSISTSECURITYINFO;
        internal const string PERSISTSECURITYINFO = "persistsecurityinfo";

        //internal const string TrustServerCertificate = TRUSTSERVERCERTIFICATE;
        internal const string TRUSTSERVERCERTIFICATE = "TrustServerCertificate";

        //internal const string UserID                 = UID+","+User;
        internal const string UID = "uid";
        internal const string User = "user";

        //internal const string WorkstationID          = WSID;
        internal const string WSID = "wsid";
    }
}
