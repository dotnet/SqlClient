// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A transient database user, created at the start of its scope and dropped when disposed.
/// </summary>
/// <remarks>
/// This class assumes that the associated server login already exists.
/// </remarks>
public sealed class DatabaseUser : DatabaseObject<string>
{
    public string DatabaseName => State;

    /// <summary>
    /// Initializes a new instance of the DatabaseUser class using the specified SQL connection
    /// and associated server login.
    /// </summary>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="database">The name of the database where the user will be created.</param>
    /// <param name="login">The server login which the database user will be associated with.</param>
    public DatabaseUser(SqlConnection connection, string database, ServerLogin login)
        : base(connection, login.Name, $"FOR LOGIN {login.Name}", state: database, shouldCreate: true, shouldDrop: true)
    {
    }

    protected override void CreateObject(string definition)
    {
        using SqlCommand createCommand = new($"CREATE USER {Name} {definition}", Connection);

        ExecuteCommandInDatabase(createCommand);
    }

    protected override void DropObject()
    {
        using SqlCommand dropCommand = new($"IF USER_ID('{UnescapedName}') IS NOT NULL DROP USER {Name}", Connection);

        ExecuteCommandInDatabase(dropCommand);
    }

    private void ExecuteCommandInDatabase(SqlCommand command)
    {
        // In some cases, we want to manage a database user in a different database to the one that the connection
        // currently has open. In such a case, we need to change the database context for the command execution
        // and then restore it afterwards.
        string? originalDatabase = DatabaseName == command.Connection.Database ? null : command.Connection.Database;

        try
        {
            if (originalDatabase is not null)
            {
                command.Connection.ChangeDatabase(DatabaseName);
            }

            command.ExecuteNonQuery();
        }
        finally
        {
            if (originalDatabase is not null)
            {
                command.Connection.ChangeDatabase(originalDatabase);
            }
        }
    }
}
