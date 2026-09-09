// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A transient schema, created at the start of its scope and dropped when disposed.
/// </summary>
/// <remarks>
/// A schema can only be dropped once it is empty, so any object created inside it must be
/// declared after the schema and therefore disposed before it.
/// </remarks>
public sealed class Schema : DatabaseObject
{
    /// <summary>
    /// Initializes a new instance of the Schema class using the specified SQL connection and name prefix.
    /// </summary>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="prefix">The prefix for the schema name.</param>
    public Schema(SqlConnection connection, string prefix)
        : base(connection, GenerateLongName(prefix), definition: string.Empty)
    {
    }

    private Schema(SqlConnection connection, string name, NameIsVerbatim _)
        : base(connection, name, definition: string.Empty)
    {
    }

    /// <summary>
    /// Creates a schema using the caller-supplied name verbatim, instead of generating one.
    /// </summary>
    /// <remarks>
    /// Prefer the prefix-based constructor. This overload exists for tests in which the name
    /// itself is under test, for example one containing special characters.
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="name">The schema name, already quoted/escaped by the caller if it needs to be.</param>
    public static Schema WithName(SqlConnection connection, string name)
        => new(connection, name, NameIsVerbatim.Yes);

    protected override void CreateObject(string definition)
    {
        // NOTE: CREATE SCHEMA must be the first statement in its batch, so it cannot be guarded
        //   by an IF the way the other objects are. The base class drops before creating, which
        //   covers the (vanishingly unlikely) case of a name collision.
        using SqlCommand createCommand = new($"CREATE SCHEMA {Name}", Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        // NOTE: The name is passed to SCHEMA_ID() as a parameter rather than being interpolated
        //   into a string literal, because it may embed Environment.UserName/MachineName (see
        //   DatabaseObject.GenerateLongName) and an apostrophe in either would break the batch.
        //   The identifier in DROP SCHEMA is already bracket-quoted.
        using SqlCommand dropCommand = new($"IF (SCHEMA_ID(@name) IS NOT NULL) DROP SCHEMA {Name}", Connection);

        dropCommand.Parameters.AddWithValue("@name", UnescapedName);

        dropCommand.ExecuteNonQuery();
    }
}
