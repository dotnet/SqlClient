// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// This is used to hold the ServerSpn for a given connection. Most connection types have a single format, although TCP connections may allow
    /// with and without a port. Depending on how the SPN is registered on the server, either one may be the correct name.
    /// </summary>
    /// <see href="https://learn.microsoft.com/sql/database-engine/configure-windows/register-a-service-principal-name-for-kerberos-connections?view=sql-server-ver17#spn-formats"/>
    /// <param name="primary"></param>
    /// <param name="secondary"></param>
    /// <remarks>
    /// <para>SQL Server SPN format follows these patterns:</para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>Default instance, no port (primary):</term>
    ///     <description>MSSQLSvc/fully-qualified-domain-name</description>
    ///   </item>
    ///   <item>
    ///     <term>Default instance, default port (secondary):</term>
    ///     <description>MSSQLSvc/fully-qualified-domain-name:1433</description>
    ///   </item>
    ///   <item>
    ///     <term>Named instance or custom port:</term>
    ///     <description>MSSQLSvc/fully-qualified-domain-name:port_or_instance_name</description>
    ///   </item>
    /// </list>
    /// <para>For TCP connections to named instances, the port number is used in SPN.</para>
    /// <para>For Named Pipe connections to named instances, the instance name is used in SPN.</para>
    /// <para>When hostname resolution fails, the user-provided hostname is used instead of FQDN.</para>
    /// <para>For default instances with TCP protocol, both forms (with and without port) may be returned.</para>
    /// </remarks>
    internal readonly struct ResolvedServerSpn(string primary, string? secondary = null)
    {
        public string Primary => primary;

        public string? Secondary => secondary;
    }
}
