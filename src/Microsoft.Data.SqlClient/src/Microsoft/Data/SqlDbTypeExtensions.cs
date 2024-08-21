// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;

namespace Microsoft.Data
{
    /// <summary>
    /// Extensions for SqlDbType enum to enable its usage.
    /// </summary>
    public static class SqlDbTypeExtensions
    {
        /// <summary>
        /// Represents the JSON Data type in SQL Server.
        /// </summary>
#if NET9_0_OR_GREATER
        public const SqlDbType Json = SqlDbType.Json;
#else
        public const SqlDbType Json = (SqlDbType)35;
#endif
    }
}
