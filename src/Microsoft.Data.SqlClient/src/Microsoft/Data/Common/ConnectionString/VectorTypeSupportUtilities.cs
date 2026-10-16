// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.Common.ConnectionString
{
    internal static class VectorTypeSupportUtilities
    {
        internal static bool TryConvertToVectorTypeSupport(string value, out SqlVectorTypeSupport result)
        {
            Debug.Assert(Enum.GetNames(typeof(SqlVectorTypeSupport)).Length == 3, "SqlVectorTypeSupport enum has changed, update needed");
            Debug.Assert(value != null, "TryConvertToVectorTypeSupport(null,...)");

            if (StringComparer.OrdinalIgnoreCase.Equals(value, nameof(SqlVectorTypeSupport.Off)))
            {
                result = SqlVectorTypeSupport.Off;
                return true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(value, nameof(SqlVectorTypeSupport.V1)))
            {
                result = SqlVectorTypeSupport.V1;
                return true;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(value, nameof(SqlVectorTypeSupport.V2)))
            {
                result = SqlVectorTypeSupport.V2;
                return true;
            }
            else
            {
                result = DbConnectionStringDefaults.VectorTypeSupport;
                return false;
            }
        }

        internal static bool IsValidVectorTypeSupportValue(SqlVectorTypeSupport value)
        {
            Debug.Assert(Enum.GetNames(typeof(SqlVectorTypeSupport)).Length == 3, "SqlVectorTypeSupport enum has changed, update needed");
            return value == SqlVectorTypeSupport.Off || value == SqlVectorTypeSupport.V1 || value == SqlVectorTypeSupport.V2;
        }

        internal static string VectorTypeSupportToString(SqlVectorTypeSupport value)
        {
            Debug.Assert(IsValidVectorTypeSupportValue(value));

            return value switch
            {
                SqlVectorTypeSupport.Off => nameof(SqlVectorTypeSupport.Off),
                SqlVectorTypeSupport.V2 => nameof(SqlVectorTypeSupport.V2),
                _ => nameof(SqlVectorTypeSupport.V1),
            };
        }

        /// <summary>
        /// Converts the given value to a <see cref="SqlVectorTypeSupport"/>, following the same
        /// rules as the other enumerated connection string values: a string is matched against
        /// the enum names using an ordinal, case insensitive comparison; a value of the enum
        /// type is used as is; an integral value is converted; and anything else is rejected.
        /// </summary>
        internal static SqlVectorTypeSupport ConvertToVectorTypeSupport(string keyword, object value)
        {
            Debug.Assert(value != null, "ConvertToVectorTypeSupport(null)");

            if (value is string sValue)
            {
                if (TryConvertToVectorTypeSupport(sValue, out SqlVectorTypeSupport result))
                {
                    return result;
                }

                // Try again without any leading or trailing whitespace.
                sValue = sValue.Trim();
                if (TryConvertToVectorTypeSupport(sValue, out result))
                {
                    return result;
                }

                throw ADP.InvalidConnectionOptionValue(keyword);
            }
            else
            {
                SqlVectorTypeSupport eValue;

                if (value is SqlVectorTypeSupport support)
                {
                    // Quick path for the most common case.
                    eValue = support;
                }
                else if (value.GetType().IsEnum)
                {
                    // Block the use of an unrelated enum type, which would otherwise be
                    // converted through its underlying integral value.
                    throw ADP.ConvertFailed(value.GetType(), typeof(SqlVectorTypeSupport), null);
                }
                else
                {
                    try
                    {
                        eValue = (SqlVectorTypeSupport)Enum.ToObject(typeof(SqlVectorTypeSupport), value);
                    }
                    catch (ArgumentException e)
                    {
                        throw ADP.ConvertFailed(value.GetType(), typeof(SqlVectorTypeSupport), e);
                    }
                }

                if (IsValidVectorTypeSupportValue(eValue))
                {
                    return eValue;
                }

                throw ADP.InvalidEnumerationValue(typeof(SqlVectorTypeSupport), (int)eValue);
            }
        }

        /// <summary>
        /// The vector feature extension version which corresponds to the given setting.
        /// </summary>
        internal static byte ToFeatureExtensionVersion(SqlVectorTypeSupport value)
        {
            Debug.Assert(IsValidVectorTypeSupportValue(value));

            return value switch
            {
                SqlVectorTypeSupport.Off => TdsEnums.VECTOR_VERSION_NOT_SUPPORTED,
                SqlVectorTypeSupport.V2 => TdsEnums.VECTOR_VERSION_FLOAT16,
                _ => TdsEnums.VECTOR_VERSION_FLOAT32,
            };
        }
    }
}
