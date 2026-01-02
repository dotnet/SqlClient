// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text.Json;

using Microsoft.Data.SqlClient.TestUtilities;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// This class reads configuration information from environment variables and
/// the config.json file for use by our tests.
///
/// Environment variables take precedence over config.json settings.  Note that
/// variable names are case-sensitive on non-Windows platforms.
///
/// The following variables are supported:
///
///   ADO_POOL:
///     When defined, indicates that tests are running in an ADO-CI pool.
///
///   SYSTEM_ACCESSTOKEN:
///     The Azure Pipelines $(System.AccessToken) to use for workload identity
///     federation.
///
///   TEST_DEBUG_EMIT:
///     When defined, enables debug output of configuration values.
///
///   TEST_MDS_CONFIG:
///     The path to the config file to use instead of the default.  If not
///     supplied, the config file is assumed to be located next to the test
///     assembly and is named config.json.
/// </summary>
internal static class Config
{
    #region Config Properties

    internal static bool AdoPool { get; } = false;
    internal static bool DebugEmit { get; } = false;
    internal static bool IntegratedSecuritySupported { get; } = false;
    internal static bool ManagedIdentitySupported { get; } = false;
    internal static string PasswordConnectionString { get; } = string.Empty;
    internal static string ServicePrincipalId { get; } = string.Empty;
    internal static string ServicePrincipalSecret { get; } = string.Empty;
    internal static string SystemAccessToken { get; } = string.Empty;
    internal static bool SystemAssignedManagedIdentitySupported { get; } = false;
    internal static string TcpConnectionString { get; } = string.Empty;
    internal static string TenantId { get; } = string.Empty;
    internal static bool UseManagedSniOnWindows { get; } = false;
    internal static string UserManagedIdentityClientId { get; } = string.Empty;
    internal static string WorkloadIdentityFederationServiceConnectionId { get; } = string.Empty;

    #endregion

    #region Conditional Fact/Theory Helpers

    internal static bool HasIntegratedSecurityConnectionString() =>
        !TcpConnectionString.Empty() && IntegratedSecuritySupported;
    internal static bool HasPasswordConnectionString() => !PasswordConnectionString.Empty();
    internal static bool HasServicePrincipal() => !ServicePrincipalId.Empty() && !ServicePrincipalSecret.Empty();
    internal static bool HasSystemAccessToken() => !SystemAccessToken.Empty();
    internal static bool HasTcpConnectionString() => !TcpConnectionString.Empty();
    internal static bool HasTenantId() => !TenantId.Empty();
    internal static bool HasUserManagedIdentityClientId() => !UserManagedIdentityClientId.Empty();
    internal static bool HasWorkloadIdentityFederationServiceConnectionId() => !WorkloadIdentityFederationServiceConnectionId.Empty();

    internal static bool IsAzureSqlServer() =>
        Utils.IsAzureSqlServer(new SqlConnectionStringBuilder(TcpConnectionString).DataSource);

    internal static bool OnAdoPool() => AdoPool;
    internal static bool OnLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    internal static bool OnMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    internal static bool OnWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    internal static bool OnUnix() => OnLinux() || OnMacOS();

    internal static bool SupportsIntegratedSecurity() => IntegratedSecuritySupported;
    internal static bool SupportsManagedIdentity() => ManagedIdentitySupported;
    internal static bool SupportsSystemAssignedManagedIdentity() => SystemAssignedManagedIdentitySupported;

    #endregion

    #region Static Construction

    /// <summary>
    /// Static construction reads configuration settings from the config file
    /// and environment variables.
    /// </summary>
    static Config()
    {
        // Read from the config.json file.  If the TEST_MDS_CONFIG environment
        // variable is set, use it.  Otherwise, assume the config file is in the
        // working directory and named config.json.
        string configPath = GetEnvVar("TEST_MDS_CONFIG");
        if (configPath.Empty())
        {
            configPath = "config.json";
        }

        try
        {
            using JsonDocument doc =
                JsonDocument.Parse(
                    File.ReadAllText(configPath),
                    new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });

            JsonElement root = doc.RootElement;
            // See the sample config file for information about these settings:
            //
            // src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/config.default.json
            //
            // The sample file is copied to the build output directory as
            // config.json by the TestUtilities project file.
            //
            IntegratedSecuritySupported = GetBool(root, "SupportsIntegratedSecurity");
            ManagedIdentitySupported = GetBool(root, "ManagedIdentitySupported");
            PasswordConnectionString = GetString(root, "AADPasswordConnectionString");
            ServicePrincipalId = GetString(root, "AADServicePrincipalId");
            ServicePrincipalSecret = GetString(root, "AADServicePrincipalSecret");
            SystemAssignedManagedIdentitySupported =
                GetBool(root, "SupportsSystemAssignedManagedIdentity");
            TcpConnectionString = GetString(root, "TCPConnectionString");
            TenantId = GetString(root, "AzureKeyVaultTenantId");
            UseManagedSniOnWindows = GetBool(root, "UseManagedSNIOnWindows");
            UserManagedIdentityClientId = GetString(root, "UserManagedIdentityClientId");
            WorkloadIdentityFederationServiceConnectionId =
                GetString(root, "WorkloadIdentityFederationServiceConnectionId");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Config: Failed to read config file={configPath}: {ex}");
            throw;
        }

        // Apply environment variable overrides.
        //
        // Note that environment variables are case-sensitive on non-Windows
        // platforms.
        AdoPool = GetEnvFlag("ADO_POOL");
        DebugEmit = GetEnvFlag("TEST_DEBUG_EMIT");
        SystemAccessToken = GetEnvVar("SYSTEM_ACCESSTOKEN");

        // Emit debug information if requested.
        if (DebugEmit)
        {
            Console.WriteLine("Config:");
            Console.WriteLine(
                $"  AdoPool:                                {AdoPool}");
            Console.WriteLine(
                $"  DebugEmit:                              {DebugEmit}");
            Console.WriteLine(
                $"  IntegratedSecuritySupported:            {IntegratedSecuritySupported}");
            Console.WriteLine(
                $"  ManagedIdentitySupported:               {ManagedIdentitySupported}");
            Console.WriteLine(
                $"  PasswordConnectionString:               {PasswordConnectionString}");
            Console.WriteLine(
                $"  ServicePrincipalId:                     {ServicePrincipalId}");
            Console.WriteLine(
                $"  ServicePrincipalSecret:                 {ServicePrincipalSecret.Length}");
            Console.WriteLine(
                $"  SystemAccessToken:                      {SystemAccessToken}");
            Console.WriteLine(
                $"  SystemAssignedManagedIdentitySupported: {SystemAssignedManagedIdentitySupported}");
            Console.WriteLine(
                $"  TcpConnectionString:                    {TcpConnectionString}");
            Console.WriteLine(
                $"  TenantId:                               {TenantId}");
            Console.WriteLine(
                $"  UseManagedSniOnWindows:                 {UseManagedSniOnWindows}");
            Console.WriteLine(
                $"  UserManagedIdentityClientId:            {UserManagedIdentityClientId}");
            Console.WriteLine(
                "  WorkloadIdentityFederationServiceConnectionId: " +
                WorkloadIdentityFederationServiceConnectionId);
        }

        // Apply the SNI flag, if necessary.  This must occur before any MDS
        // APIs are used.
        if (UseManagedSniOnWindows)
        {
            AppContext.SetSwitch(
                "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows",
                true);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Get a string property from a JSON element.
    /// </summary>
    /// <param name="element">The JSON element to read from.</param>
    /// <param name="name">The name of the property to read.</param>
    /// <returns>The string value of the property, or an empty string if not found or invalid.</returns>
    private static string GetString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var property))
        {
            try
            {
                var value = property.GetString();
                if (value is not null)
                {
                    return value;
                }
            }
            catch (InvalidOperationException)
            {
                // Ignore invalid values.
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Get a boolean property from a JSON element.
    /// </summary>
    /// <param name="element">The JSON element to read from.</param>
    /// <param name="name">The name of the property to read.</param>
    /// <returns>The boolean value of the property, or false if not found or invalid.</returns>
    private static bool GetBool(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var property))
        {
            try
            {
                return property.GetBoolean();
            }
            catch (InvalidOperationException)
            {
                // Ignore invalid values.
            }
        }

        return false;
    }

    /// <summary>
    /// Get a boolean flag from an environment variable.  The variable's value
    /// is not examined; only its presence is checked.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <returns>True if the environment variable is set; false otherwise.</returns>
    private static bool GetEnvFlag(string name)
    {
        return Environment.GetEnvironmentVariable(name) is not null;
    }

    /// <summary>
    /// Get a string value from an environment variable.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <returns>The value of the environment variable, or an empty string if not set.</returns>
    private static string GetEnvVar(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value;
    }

    #endregion
}
