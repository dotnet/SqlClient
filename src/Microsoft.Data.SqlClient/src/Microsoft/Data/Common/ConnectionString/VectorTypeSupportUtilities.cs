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
            // Match names only. Enum.TryParse also accepts numeric strings and comma-separated
            // combinations such as "Off, V2", which are not connection string keyword values.
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

        internal static bool IsValidVectorTypeSupportValue(SqlVectorTypeSupport value) =>
            value is SqlVectorTypeSupport.Off or SqlVectorTypeSupport.V1 or SqlVectorTypeSupport.V2;

        internal static string VectorTypeSupportToString(SqlVectorTypeSupport value)
        {
            switch (value)
            {
                case SqlVectorTypeSupport.Off:
                    return nameof(SqlVectorTypeSupport.Off);
                case SqlVectorTypeSupport.V1:
                    return nameof(SqlVectorTypeSupport.V1);
                case SqlVectorTypeSupport.V2:
                    return nameof(SqlVectorTypeSupport.V2);
                default:
                    Debug.Fail("Invalid vector type support value.");
                    throw ADP.InvalidEnumerationValue(typeof(SqlVectorTypeSupport), (int)value);
            }
        }

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
        internal static byte ToFeatureExtensionVersion(SqlVectorTypeSupport value)
        {
            switch (value)
            {
                case SqlVectorTypeSupport.Off:
                    return TdsEnums.VECTOR_VERSION_NOT_SUPPORTED;
                case SqlVectorTypeSupport.V1:
                    return TdsEnums.VECTOR_VERSION_FLOAT32;
                case SqlVectorTypeSupport.V2:
                    return TdsEnums.VECTOR_VERSION_FLOAT16;
                default:
                    Debug.Fail("Invalid vector type support value.");
                    throw ADP.InvalidEnumerationValue(typeof(SqlVectorTypeSupport), (int)value);
            }
        }
    }
}
