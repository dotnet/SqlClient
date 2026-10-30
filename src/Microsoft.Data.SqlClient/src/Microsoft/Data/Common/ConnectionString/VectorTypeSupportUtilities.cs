// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Parser;

#nullable enable

namespace Microsoft.Data.Common.ConnectionString
{
    internal static class VectorTypeSupportUtilities
    {
        #if DEBUG
        static VectorTypeSupportUtilities()
        {
            Debug.Assert(Enum.GetNames(typeof(SqlVectorTypeSupport)).Length == 3,
                "SqlVectorTypeSupport enum has changed, update needed");
        }
        #endif

        internal static bool TryConvertToVectorTypeSupport(string value, out SqlVectorTypeSupport result)
        {
            // Require a name, not a numeric string or a combination such as "Off, V2".
            if (Enum.TryParse(value, ignoreCase: true, out result)
                && IsValidVectorTypeSupportValue(result)
                && StringComparer.OrdinalIgnoreCase.Equals(value, VectorTypeSupportToString(result)))
            {
                return true;
            }

            result = DbConnectionStringDefaults.VectorTypeSupport;
            return false;
        }

        internal static bool IsValidVectorTypeSupportValue(SqlVectorTypeSupport value) =>
            value is SqlVectorTypeSupport.Off or SqlVectorTypeSupport.V1 or SqlVectorTypeSupport.V2;

        internal static string VectorTypeSupportToString(SqlVectorTypeSupport value) =>
            value switch
            {
                SqlVectorTypeSupport.Off => nameof(SqlVectorTypeSupport.Off),
                SqlVectorTypeSupport.V1 => nameof(SqlVectorTypeSupport.V1),
                SqlVectorTypeSupport.V2 => nameof(SqlVectorTypeSupport.V2),
                _ => throw InvalidVectorTypeSupportValue(value),
            };

        /// <summary>
        /// Converts the given value to a <see cref="SqlVectorTypeSupport"/>, following the same
        /// rules as the other enumerated connection string values: a string is matched against
        /// the enum names using an ordinal, case insensitive comparison; a value of the enum
        /// type is used as is; an integral value is converted; and anything else is rejected.
        /// </summary>
        internal static SqlVectorTypeSupport ConvertToVectorTypeSupport(string keyword, object value)
        {
            if (value is string sValue)
            {
                return TryConvertToVectorTypeSupport(sValue.Trim(), out SqlVectorTypeSupport result)
                    ? result
                    : throw ADP.InvalidConnectionOptionValue(keyword);
            }

            SqlVectorTypeSupport eValue;
            if (value is SqlVectorTypeSupport support)
            {
                eValue = support;
            }
            else if (value.GetType().IsEnum || value is float || value is double)
            {
                // Enum.ToObject accepts foreign enums; on .NET 8 it also reinterprets float bits.
                throw ADP.ConvertFailed(value.GetType(), typeof(SqlVectorTypeSupport), null);
            }
            else
            {
                try
                {
                    // The indexer accepts boxed integers of different widths, not just Int32.
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

        /// <summary>
        /// The vector feature extension version which corresponds to the given setting.
        /// </summary>
        internal static byte ToFeatureExtensionVersion(SqlVectorTypeSupport value) =>
            value switch
            {
                SqlVectorTypeSupport.Off => TdsEnums.VECTOR_VERSION_NOT_SUPPORTED,
                SqlVectorTypeSupport.V1 => TdsEnums.VECTOR_VERSION_FLOAT32,
                SqlVectorTypeSupport.V2 => TdsEnums.VECTOR_VERSION_FLOAT16,
                _ => throw InvalidVectorTypeSupportValue(value),
            };

        private static ArgumentOutOfRangeException InvalidVectorTypeSupportValue(
            SqlVectorTypeSupport value)
        {
            Debug.Fail("Invalid vector type support value.");
            return ADP.InvalidEnumerationValue(typeof(SqlVectorTypeSupport), (int)value);
        }
    }
}
