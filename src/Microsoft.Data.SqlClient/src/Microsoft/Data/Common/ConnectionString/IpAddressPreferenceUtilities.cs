// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.Common.ConnectionString
{
    internal static class IpAddressPreferenceUtilities
    {
        /// <summary>
        /// IP Address Preference.
        /// </summary>
        private readonly static Dictionary<string, SqlConnectionIPAddressPreference> s_preferenceNames = new(StringComparer.InvariantCultureIgnoreCase);

        static IpAddressPreferenceUtilities()
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
                result = DbConnectionStringDefaults.IpAddressPreference;
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
                return DbConnectionStringDefaults.IpAddressPreference;  // IPv4First
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
    }
}
