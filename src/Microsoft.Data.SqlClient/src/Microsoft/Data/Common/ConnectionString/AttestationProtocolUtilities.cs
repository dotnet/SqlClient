// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.Common.ConnectionString
{
    internal static class AttestationProtocolUtilities
    {
        /// <summary>
        ///  Convert a string value to the corresponding SqlConnectionAttestationProtocol
        /// </summary>
        /// <param name="value"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal static bool TryConvertToAttestationProtocol(string value, out SqlConnectionAttestationProtocol result)
        {
            if (StringComparer.InvariantCultureIgnoreCase.Equals(value, nameof(SqlConnectionAttestationProtocol.HGS)))
            {
                result = SqlConnectionAttestationProtocol.HGS;
                return true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, nameof(SqlConnectionAttestationProtocol.AAS)))
            {
                result = SqlConnectionAttestationProtocol.AAS;
                return true;
            }
            else if (StringComparer.InvariantCultureIgnoreCase.Equals(value, nameof(SqlConnectionAttestationProtocol.None)))
            {
                result = SqlConnectionAttestationProtocol.None;
                return true;
            }
            else
            {
                result = DbConnectionStringDefaults.AttestationProtocol;
                return false;
            }
        }

        internal static bool IsValidAttestationProtocol(SqlConnectionAttestationProtocol value)
        {
            Debug.Assert(Enum.GetNames(typeof(SqlConnectionAttestationProtocol)).Length == 4, "SqlConnectionAttestationProtocol enum has changed, update needed");
            return value == SqlConnectionAttestationProtocol.NotSpecified
                || value == SqlConnectionAttestationProtocol.HGS
                || value == SqlConnectionAttestationProtocol.AAS
                || value == SqlConnectionAttestationProtocol.None;
        }

        internal static string AttestationProtocolToString(SqlConnectionAttestationProtocol value)
        {
            Debug.Assert(IsValidAttestationProtocol(value), "value is not a valid attestation protocol");

            return value switch
            {
                SqlConnectionAttestationProtocol.AAS => nameof(SqlConnectionAttestationProtocol.AAS),
                SqlConnectionAttestationProtocol.HGS => nameof(SqlConnectionAttestationProtocol.HGS),
                SqlConnectionAttestationProtocol.None => nameof(SqlConnectionAttestationProtocol.None),
                _ => null
            };
        }

        internal static SqlConnectionAttestationProtocol ConvertToAttestationProtocol(string keyword, object value)
        {
            if (value == null)
            {
                return DbConnectionStringDefaults.AttestationProtocol;
            }

            if (value is string sValue)
            {
                // try again after remove leading & trailing whitespaces.
                sValue = sValue.Trim();
                if (TryConvertToAttestationProtocol(sValue, out SqlConnectionAttestationProtocol result))
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

                if (value is SqlConnectionAttestationProtocol protocol)
                {
                    eValue = protocol;
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

        internal static SqlConnectionEncryptOption ConvertToSqlConnectionEncryptOption(string keyword, object value)
        {
            if (value is null)
            {
                return DbConnectionStringDefaults.Encrypt;
            }
            else if(value is SqlConnectionEncryptOption eValue)
            {
                return eValue;
            }
            else if (value is string sValue)
            {
                return SqlConnectionEncryptOption.Parse(sValue);
            }
            else if(value is bool bValue)
            {
                return SqlConnectionEncryptOption.Parse(bValue);
            }

            throw ADP.InvalidConnectionOptionValue(keyword);
        }
    }
}
