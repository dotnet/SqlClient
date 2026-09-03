// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <summary>
///   Driver-owned feature flags reported in the Driver Properties part of the
///   USERAGENT login feature extension payload.
/// </summary>
/// <remarks>
///   This part is driver-owned, so its meaning is defined entirely by
///   Microsoft.Data.SqlClient and carries no cross-driver contract.  Other
///   drivers use the same part for their own purposes.
/// </remarks>
[Flags]
internal enum SqlClientDriverProperties : ushort
{
    /// <summary>No tracked features are enabled.</summary>
    None = 0x0000,

    /// <summary>
    ///   The connection pool V2 implementation
    ///   (<c>Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2</c>) is
    ///   enabled.
    /// </summary>
    ConnectionPoolV2 = 0x0001
}

/// <summary>
///   Resolves the <see cref="SqlClientDriverProperties"/> flags that describe
///   how this process is configured.
/// </summary>
internal static class SqlClientDriverPropertiesResolver
{
    /// <summary>
    ///   The flags describing the current process.
    /// </summary>
    /// <remarks>
    ///   The flags are sourced from process-wide switches, so this is stable
    ///   for the life of the process.
    /// </remarks>
    internal static SqlClientDriverProperties Current
    {
        get
        {
            SqlClientDriverProperties properties = SqlClientDriverProperties.None;

            if (LocalAppContextSwitches.UseConnectionPoolV2)
            {
                properties |= SqlClientDriverProperties.ConnectionPoolV2;
            }

            return properties;
        }
    }
}
