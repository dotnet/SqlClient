// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A transient stored procedure, created at the start of its scope and dropped when disposed.
/// </summary>
public sealed class StoredProcedure : DatabaseObject
{
    /// <summary>
    /// Initializes a new instance of the StoredProcedure class using the specified SQL connection,
    /// name and definition.
    /// </summary>
    /// <remarks>
    /// If a stored procedure with the specified name already exists, it will be dropped automatically
    /// before creation.
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="prefix">The stored procedure name. Can begin with '#' or '##' to indicate a temporary procedure.</param>
    /// <param name="definition">The SQL definition of the stored procedure.</param>
    public StoredProcedure(SqlConnection connection, string prefix, string definition)
        : base(connection, GenerateLongName(prefix), definition, shouldCreate: true, shouldDrop: true)
    {
    }

    private StoredProcedure(SqlConnection connection, string name, string definition, bool shouldCreate)
        : base(connection, name, definition, shouldCreate, shouldDrop: true)
    {
    }

    /// <summary>
    /// Creates a stored procedure using the caller-supplied name verbatim, instead of generating one.
    /// </summary>
    /// <remarks>
    /// Prefer the prefix-based constructor: generated names embed a GUID and so cannot collide
    /// between concurrent test runs against a shared database. This overload exists for the
    /// minority of tests that must control the name exactly, for example because the same
    /// procedure has to be created and addressed over several different connections.
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="name">The procedure name, already quoted/escaped by the caller if it needs to be.</param>
    /// <param name="definition">The SQL definition of the stored procedure.</param>
    public static StoredProcedure WithName(SqlConnection connection, string name, string definition)
        => new(connection, name, definition, shouldCreate: true);

    protected override void CreateObject(string definition)
    {
        using SqlCommand createCommand = new($"CREATE PROCEDURE {Name} {definition}", Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        // NOTE: The name is passed to OBJECT_ID() as a parameter rather than being interpolated
        //   into a string literal, because it embeds Environment.UserName/MachineName (see
        //   DatabaseObject.GenerateLongName) and an apostrophe in either would break the batch.
        //   The identifier in DROP PROCEDURE is already bracket-quoted by GenerateLongName.
        using SqlCommand dropCommand = new($"IF (OBJECT_ID(@name) IS NOT NULL) DROP PROCEDURE {Name}", Connection);

        dropCommand.Parameters.AddWithValue("@name", Name);

        dropCommand.ExecuteNonQuery();
    }
}
