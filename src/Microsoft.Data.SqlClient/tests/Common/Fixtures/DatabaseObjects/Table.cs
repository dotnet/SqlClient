// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A transient table, created at the start of its scope and dropped when disposed.
/// </summary>
public sealed class Table : DatabaseObject
{
    /// <summary>
    /// Initializes a new instance of the Table class using the specified SQL connection, table name prefix, and table
    /// definition.
    /// </summary>
    /// <remarks>
    /// If a table with the specified name already exists, it will be dropped automatically before
    /// creation.
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="prefix">The prefix for the table name. Can begin with '#' or '##' to indicate a temporary table.</param>
    /// <param name="definition">The SQL definition describing the structure of the table, including columns and data types.</param>
    public Table(SqlConnection connection, string prefix, string definition)
        : base(connection, GenerateLongName(prefix), definition, shouldCreate: true, shouldDrop: true)
    {
    }

    protected override void CreateObject(string definition)
    {
        using SqlCommand createCommand = new($"CREATE TABLE {Name} {definition}", Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        using SqlCommand dropCommand = new($"IF (OBJECT_ID('{Name}') IS NOT NULL) DROP TABLE {Name}", Connection);

        dropCommand.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes all data from the table.
    /// </summary>
    public void DeleteData()
    {
        using SqlCommand deleteCommand = new($"DELETE FROM {Name}", Connection);

        deleteCommand.ExecuteNonQuery();
    }

    /// <summary>
    /// Truncates the table.
    /// </summary>
    public void Truncate()
    {
        using SqlCommand truncateCommand = new($"TRUNCATE TABLE {Name}", Connection);

        truncateCommand.ExecuteNonQuery();
    }
}
