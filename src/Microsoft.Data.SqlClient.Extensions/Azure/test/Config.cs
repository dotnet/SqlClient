// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

// This class reads configuration information from environment variables for use
// by our tests.
//
// The following variables are supported:
//
//   TEST_AZURE_SQL_SERVER - The server to connect to.
//   TEST_AZURE_SQL_DB - The database to connect to.
//   TEST_AZURE_SQL_USER - The username to connect with.
//   TEST_AZURE_SQL_PASSWORD - The password to connect with.
//   TEST_AZURE_MANAGED_IDENTITY - The managed identity to use for authentication.
//
internal static class Config
{
    # region Config Properties

    internal static bool DebugEmit { get; }
    internal static string Server { get; }
    internal static string Database { get; }
    internal static string Username { get; }
    internal static string Password { get; }
    internal static string ManagedIdentity { get; }
    internal static string SystemAccessToken { get; }

    #endregion

    #region Conditional Fact/Theory Helpers

    internal static bool HasServer() => Server.NotEmpty();
    internal static bool HasDatabase() => Database.NotEmpty();
    internal static bool HasUsernamePassword() => Username.NotEmpty() && Password.NotEmpty();
    internal static bool HasManagedIdentity() => ManagedIdentity.NotEmpty();
    internal static bool HasSystemAccessToken() => SystemAccessToken.NotEmpty();
    
    #endregion

    #region Static Construction

    static Config()
    {
        // Note that environment variables are case-sensitive on non-Windows
        // platforms.
        DebugEmit = Environment.GetEnvironmentVariable("TEST_DEBUG_EMIT") is not null;
        Server = GetEnvVar("TEST_AZURE_SQL_SERVER");
        Database = GetEnvVar("TEST_AZURE_SQL_DB");
        Username = GetEnvVar("TEST_AZURE_SQL_USER");
        Password = GetEnvVar("TEST_AZURE_SQL_PASSWORD");
        ManagedIdentity = GetEnvVar("TEST_AZURE_MANAGED_IDENTITY");
        SystemAccessToken = GetEnvVar("SYSTEM_ACCESSTOKEN");

        if (DebugEmit)
        {
            var emit = (string name, string value) =>
            {
                Console.WriteLine($"  {name} ({value.Length}): {value}");
            };

            Console.WriteLine("Config:");
            emit("Server", Server);
            emit("Database", Database);
            emit("Username", Username);
            emit("Password", Password);
            emit("ManagedIdentity", ManagedIdentity);
            emit("SystemAccessToken", SystemAccessToken);
        }
    }

    #endregion

    #region Private Methods

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
