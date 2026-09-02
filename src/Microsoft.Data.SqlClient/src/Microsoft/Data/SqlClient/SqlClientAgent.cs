// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;
using System.Threading;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Identifies known middleware agents that use Microsoft.Data.SqlClient.
/// </summary>
public enum SqlClientAgent
{
    /// <summary>The Microsoft Entity Framework Core SQL Server provider.</summary>
    EntityFramework = 1,

    /// <summary>Microsoft Semantic Kernel.</summary>
    SemanticKernel = 2,

    /// <summary>Microsoft SQL Server Management Studio.</summary>
    ManagementStudio = 3,

    /// <summary>Microsoft SQL Server Management Objects.</summary>
    SqlManagementObjects = 4,

    /// <summary>Microsoft SQL Server Data-Tier Application Framework.</summary>
    DataTierApplicationFramework = 5,

    /// <summary>Microsoft SQL Tools Service.</summary>
    SqlToolsService = 6,

    /// <summary>Microsoft ASP.NET Core distributed SQL Server cache.</summary>
    AspNetCoreDistributedSqlServerCache = 7,

    /// <summary>Microsoft Entity Framework 6 SQL Server provider.</summary>
    EntityFramework6 = 8,

    /// <summary>Microsoft Azure Functions SQL extension.</summary>
    AzureFunctionsSqlExtension = 9,

    /// <summary>Microsoft Orleans ADO.NET providers.</summary>
    OrleansAdoNet = 10,

    /// <summary>Microsoft Durable Task SQL Server provider.</summary>
    DurableTaskSqlServer = 11
}

/// <summary>
///   <para>
///     Holds the process-wide agent registration reported in the USERAGENT
///     login feature extension.
///   </para>
///   <para>
///     An agent may be registered at most once per process, either from the
///     application configuration file or programmatically via
///     <see cref="SqlConnection.RegisterSqlClientAgent"/>.  The configuration
///     file wins, because it is read during static construction, before any
///     application code can call
///     <see cref="SqlConnection.RegisterSqlClientAgent"/>.
///   </para>
/// </summary>
internal static class SqlClientAgentRegistration
{
    // The registered agent identifier, or 0 when no agent is registered.
    //
    // Written at most once, either by static construction from the application
    // configuration file, or by the first Register() call.
    private static int s_agentId = LoadFromAppConfig();

    /// <summary>
    ///   The registered agent, or null when no agent has been registered.
    /// </summary>
    internal static SqlClientAgent? Agent
    {
        get
        {
            int id = Volatile.Read(ref s_agentId);
            return id == 0 ? null : (SqlClientAgent)id;
        }
    }

    /// <summary>
    ///   Register the given agent for the lifetime of the process.
    /// </summary>
    /// <param name="id">The agent to register.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="id"/> is not a declared agent identifier.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   An agent has already been registered.
    /// </exception>
    internal static void Register(SqlClientAgent id)
    {
        Validate(id);
        if (Interlocked.CompareExchange(ref s_agentId, (int)id, 0) != 0)
        {
            throw SQL.SqlClientAgentAlreadyRegistered();
        }
    }

    /// <summary>
    ///   <para>
    ///     Convert a configured agent identifier to a
    ///     <see cref="SqlClientAgent"/>.
    ///   </para>
    ///   <para>
    ///     Both the name of a known agent (case-insensitive) and its numeric
    ///     identifier are accepted.  A numeric identifier that is not yet
    ///     defined in <see cref="SqlClientAgent"/> is accepted, so an agent
    ///     assigned an identifier after this driver shipped can still be
    ///     configured.
    ///   </para>
    /// </summary>
    /// <param name="value">The agent identifier to convert.</param>
    /// <returns>The agent the identifier names.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="value"/> is not a valid agent identifier.
    /// </exception>
    internal static SqlClientAgent Parse(string value)
    {
        // Enum.TryParse accepts comma-separated lists and combines them, so
        // "EntityFramework,SemanticKernel" would silently yield an unrelated
        // agent.  Only a single name or number is valid here.
        if (value is null
            || value.IndexOf(',') >= 0
            || !Enum.TryParse(value, ignoreCase: true, out SqlClientAgent id)
            || !IsInRange(id))
        {
            throw SQL.InvalidSqlClientAgent(value ?? string.Empty, nameof(value));
        }

        return id;
    }

    /// <summary>
    ///   Throw if the given agent is not a declared agent identifier.
    /// </summary>
    /// <remarks>
    ///   The public registration API is a closed enum, so an undeclared value
    ///   is always a caller mistake.  Undeclared numeric identifiers remain
    ///   valid in <see cref="Parse"/>, where they let an agent assigned an
    ///   identifier after this driver shipped still be configured.
    /// </remarks>
    /// <param name="id">The agent to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="id"/> is not a declared agent identifier.
    /// </exception>
    private static void Validate(SqlClientAgent id)
    {
        if (!IsInRange(id) || !Enum.IsDefined(typeof(SqlClientAgent), id))
        {
            throw SQL.InvalidSqlClientAgent(id.ToString(), nameof(id));
        }
    }

    /// <summary>
    ///   Whether the given agent falls within the identifier space reported on
    ///   the wire.
    /// </summary>
    /// <remarks>
    ///   Identifiers are 16-bit and positive.  Zero is reserved to mean "no
    ///   agent registered".
    /// </remarks>
    /// <param name="id">The agent to test.</param>
    /// <returns>True if the agent is in range, false otherwise.</returns>
    private static bool IsInRange(SqlClientAgent id) =>
        (int)id > 0 && (int)id <= ushort.MaxValue;

    /// <summary>
    ///   <para>
    ///     Read the agent registered in the application configuration file.
    ///   </para>
    ///   <para>All known exceptions are consumed.</para>
    /// </summary>
    /// <returns>
    ///   The configured agent identifier, or 0 when no valid agent is
    ///   configured.
    /// </returns>
    private static int LoadFromAppConfig() =>
        LoadAgent(() => ConfigurationManager.GetSection(SqlClientAgentConfigurationSection.Name));

    /// <summary>
    ///   <para>
    ///     Read the agent from the section returned by the given loader.
    ///   </para>
    ///   <para>All known exceptions are consumed.</para>
    /// </summary>
    /// <param name="getSection">
    ///   Returns the configuration section, or null when it is absent.
    /// </param>
    /// <returns>
    ///   The configured agent identifier, or 0 when no valid agent is
    ///   configured.
    /// </returns>
    internal static int LoadAgent(Func<object?> getSection)
    {
        // This runs during static initialization on the login path, so any
        // escaping exception would surface as a TypeInitializationException on
        // every connection attempt.  Telemetry must never break connections.
        try
        {
            object? section = getSection();
            if (section is null)
            {
                return 0;
            }

            if (section is SqlClientAgentConfigurationSection configurationSection)
            {
                return (int)Parse(configurationSection.Id);
            }

            SqlClientEventSource.Log.TryTraceEvent(
                "SqlClientAgentRegistration: The SqlClientAgent configuration section has an unexpected type; the agent was not registered.");
        }
        catch (Exception e) when (ADP.IsCatchableExceptionType(e))
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlClientAgentRegistration: Unable to load the SqlClientAgent configuration; the agent was not registered: {0}",
                e);
        }

        return 0;
    }
}

/// <summary>
///   <para>
///     The <c>SqlClientAgent</c> application configuration file section, used
///     to register an agent without changing application code.
///   </para>
///   <para>
///     <code>
///       &lt;configSections&gt;
///         &lt;section name="SqlClientAgent"
///                  type="Microsoft.Data.SqlClient.SqlClientAgentConfigurationSection, Microsoft.Data.SqlClient" /&gt;
///       &lt;/configSections&gt;
///       &lt;SqlClientAgent id="EntityFramework" /&gt;
///     </code>
///   </para>
/// </summary>
internal sealed class SqlClientAgentConfigurationSection : ConfigurationSection
{
    /// <summary>
    ///   The name of this configuration section.
    /// </summary>
    internal const string Name = "SqlClientAgent";

    /// <summary>
    ///   The name or numeric identifier of the agent to register.
    /// </summary>
    [ConfigurationProperty("id", IsRequired = true)]
    public string Id
    {
        get => this["id"] as string ?? string.Empty;
        set => this["id"] = value;
    }
}
