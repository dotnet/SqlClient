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

    private Table(SqlConnection connection, string name, string definition, bool shouldCreate)
        : base(connection, name, definition, shouldCreate, shouldDrop: true)
    {
    }

    /// <summary>
    /// Creates a table using the caller-supplied name verbatim, instead of generating one.
    /// </summary>
    /// <remarks>
    /// Prefer the prefix-based constructor: generated names embed a GUID and so cannot collide
    /// between concurrent test runs against a shared database. This overload exists for the
    /// minority of tests that must control the name exactly - either because the name itself is
    /// under test (for example, one containing special characters), or because the same table has
    /// to be addressed through several different connections.
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="name">The table name, already quoted/escaped by the caller if it needs to be.</param>
    /// <param name="definition">The SQL definition describing the structure of the table, including columns and data types.</param>
    public static Table WithName(SqlConnection connection, string name, string definition)
        => new(connection, name, definition, shouldCreate: true);

    /// <summary>
    /// Adopts an already-existing table so that it is dropped when the returned instance is
    /// disposed. No table is created.
    /// </summary>
    /// <remarks>
    /// Useful when a table is created by other means (for example, by a helper that also populates
    /// it, or over a different connection) but still needs deterministic cleanup.
    /// </remarks>
    /// <param name="connection">The SQL connection used to drop the table.</param>
    /// <param name="name">The table name, already quoted/escaped by the caller if it needs to be.</param>
    public static Table AdoptExisting(SqlConnection connection, string name)
        => new(connection, name, definition: string.Empty, shouldCreate: false);

    protected override void CreateObject(string definition)
    {
        using SqlCommand createCommand = new($"CREATE TABLE {Name} {definition}", Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        // NOTE: The name is passed to OBJECT_ID() as a parameter rather than being interpolated
        //   into a string literal, because it embeds Environment.UserName/MachineName (see
        //   DatabaseObject.GenerateLongName) and an apostrophe in either would break the batch.
        //   The identifier in DROP TABLE is already bracket-quoted by GenerateLongName.
        using SqlCommand dropCommand = new($"IF (OBJECT_ID(@name) IS NOT NULL) DROP TABLE {Name}", Connection);

        dropCommand.Parameters.AddWithValue("@name", Name);

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
