// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Driver-owned feature flags reported in the USERAGENT Driver Properties field.
/// </summary>
[Flags]
internal enum SqlClientDriverProperties : ulong
{
    None = 0,
    ConnectionPoolV2 = 0x1
}

internal static class SqlClientDriverPropertiesResolver
{
    internal static SqlClientDriverProperties Current =>
        Resolve(LocalAppContextSwitches.UseConnectionPoolV2);

    internal static SqlClientDriverProperties Resolve(bool useConnectionPoolV2) =>
        useConnectionPoolV2
            ? SqlClientDriverProperties.ConnectionPoolV2
            : SqlClientDriverProperties.None;
}
