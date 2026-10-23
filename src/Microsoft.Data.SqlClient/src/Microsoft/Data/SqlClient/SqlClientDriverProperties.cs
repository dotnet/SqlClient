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
    ConnectionPoolV2 = 0x1,

    /// <summary>The configured async compatibility value, before the ProcessSni override.</summary>
    UseCompatibilityAsyncBehaviour = 0x2,

    /// <summary>The configured ProcessSni compatibility value.</summary>
    UseCompatibilityProcessSni = 0x4
}

internal static class SqlClientDriverPropertiesResolver
{
    internal static SqlClientDriverProperties Current =>
        Resolve(
            LocalAppContextSwitches.UseConnectionPoolV2,
            LocalAppContextSwitches.ConfiguredUseCompatibilityAsyncBehaviour,
            LocalAppContextSwitches.UseCompatibilityProcessSni);

    internal static SqlClientDriverProperties Resolve(
        bool useConnectionPoolV2,
        bool useCompatibilityAsyncBehaviour,
        bool useCompatibilityProcessSni)
    {
        SqlClientDriverProperties properties = SqlClientDriverProperties.None;
        if (useConnectionPoolV2)
        {
            properties |= SqlClientDriverProperties.ConnectionPoolV2;
        }
        if (useCompatibilityAsyncBehaviour)
        {
            properties |= SqlClientDriverProperties.UseCompatibilityAsyncBehaviour;
        }
        if (useCompatibilityProcessSni)
        {
            properties |= SqlClientDriverProperties.UseCompatibilityProcessSni;
        }
        return properties;
    }
}
