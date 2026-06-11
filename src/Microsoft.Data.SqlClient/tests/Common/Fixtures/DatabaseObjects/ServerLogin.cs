// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A transient server login, created at the start of its scope and dropped when disposed.
/// </summary>
public sealed class ServerLogin : DatabaseObject<string>
{
    public string Password => State;

    /// <summary>
    /// Initializes a new instance of the ServerLogin class using the specified SQL connection, login name prefix, and default database.
    /// The login will be created with a randomly generated password that meets SQL Server's password complexity requirements.
    /// </summary>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="namePrefix">The prefix for the login name.</param>
    /// <param name="defaultDatabase">The default database for the login. If null, not set.</param>
    public ServerLogin(SqlConnection connection, string namePrefix, string? defaultDatabase = null)
        : this(connection, GenerateLongName(namePrefix), GeneratePassword(), defaultDatabase)
    {
    }

    private ServerLogin(SqlConnection connection, string namePrefix, string password, string? defaultDatabase)
        : base(connection, namePrefix, GenerateDefinition(password, defaultDatabase), password, shouldCreate: true, shouldDrop: true)
    {
    }

    private static string GenerateDefinition(string password, string? defaultDatabase) =>
        $"WITH PASSWORD='{password}'" +
            (string.IsNullOrEmpty(defaultDatabase) ? string.Empty : $", DEFAULT_DATABASE=[{defaultDatabase}]");

    /// <summary>
    /// Generates a password which meets the SQL Server password complexity requirements, which are:
    /// <list type="number">
    /// <item>Minimum length of 8 characters</item>
    /// <item>Must contain characters from three of the following four categories:</item>
    /// <list type="number">
    /// <item>Uppercase letters (A-Z)</item>
    /// <item>Lowercase letters (a-z)</item>
    /// <item>Digits (0-9)</item>
    /// <item>Non-alphanumeric characters (e.g. !, $, #, %)</item>
    /// </list>
    /// </list>
    /// </summary>
    /// <returns>A compliant password.</returns>
    private static string GeneratePassword()
    {
        const int PasswordLength = 16;
        const char UpperCaseStart = 'A';
        const char LowerCaseStart = 'a';
        const char DigitsStart = '0';

        // First 5 characters are uppercase letters, next 5 are lowercase letters, and the last 6 are digits
        Span<char> passwordDigits = stackalloc char[PasswordLength];
        Random rnd = new();

        for(int i = 0; i < 5; i++)
        {
            passwordDigits[i] = (char)(UpperCaseStart + rnd.Next(26));
        }
        for (int i = 5; i < 10; i++)
        {
            passwordDigits[i] = (char)(LowerCaseStart + rnd.Next(26));
        }
        for (int i = 10; i < PasswordLength; i++)
        {
            passwordDigits[i] = (char)(DigitsStart + rnd.Next(10));
        }

        return passwordDigits.ToString();
    }

    protected override void CreateObject(string definition)
    {
        using SqlCommand createCommand = new($"CREATE LOGIN {Name} {definition}", Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        using SqlCommand dropCommand = new($"IF SUSER_ID('{UnescapedName}') IS NOT NULL DROP LOGIN {Name}", Connection);

        dropCommand.ExecuteNonQuery();
    }
}
