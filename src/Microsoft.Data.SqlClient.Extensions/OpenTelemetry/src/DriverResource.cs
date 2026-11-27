// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using OpenTelemetry.Resources;

namespace Microsoft.Data.SqlClient.Extensions.OpenTelemetry;

/// <summary>
/// Resource attributes identifying the driver on private-route exports. They live on the
/// resource rather than on spans, so they cost nothing per operation.
/// </summary>
internal static class DriverResource
{
    internal const string DriverName = "mssqldriver.driver.name";
    internal const string DriverVersion = "mssqldriver.driver.version";
    internal const string DriverNameValue = "sqlclient";

    // The extension ships with the driver and shares its file version.
    internal static readonly string Version =
        typeof(DriverResource).Assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "unknown";

    /// <summary>
    /// Creates the default resource (service.name, OTEL_SERVICE_NAME and
    /// OTEL_RESOURCE_ATTRIBUTES) plus the driver identity.
    /// </summary>
    internal static ResourceBuilder Create() =>
        ResourceBuilder.CreateDefault().AddAttributes(new KeyValuePair<string, object>[]
        {
            new(DriverName, DriverNameValue),
            new(DriverVersion, Version)
        });
}
