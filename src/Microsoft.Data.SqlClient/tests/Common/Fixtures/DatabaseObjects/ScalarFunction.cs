// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A transient scalar user-defined function, created at the start of its scope and dropped when
/// disposed.
/// </summary>
public sealed class ScalarFunction : DatabaseObject
{
    /// <summary>
    /// Initializes a new instance of the ScalarFunction class using the specified SQL connection,
    /// name prefix and definition.
    /// </summary>
    /// <remarks>
    /// If a function with the specified name already exists, it will be dropped automatically
    /// before creation.
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="prefix">The prefix for the function name.</param>
    /// <param name="definition">The SQL definition of the function, following the function name.</param>
    public ScalarFunction(SqlConnection connection, string prefix, string definition)
        : base(connection, GenerateLongName(prefix), definition)
    {
    }

    private ScalarFunction(SqlConnection connection, string name, string definition, NameIsVerbatim _)
        : base(connection, name, definition)
    {
    }

    /// <summary>
    /// Creates a function using the caller-supplied name verbatim, instead of generating one.
    /// </summary>
    /// <remarks>
    /// Prefer the prefix-based constructor: generated names embed a GUID and so cannot collide
    /// between concurrent test runs against a shared database. This overload exists for the
    /// minority of tests that must control the name exactly, for example because the same function
    /// has to be created and addressed over several different connections.
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="name">The function name, already quoted/escaped by the caller if it needs to be.</param>
    /// <param name="definition">The SQL definition of the function, following the function name.</param>
    public static ScalarFunction WithName(SqlConnection connection, string name, string definition)
        => new(connection, name, definition, NameIsVerbatim.Yes);

    protected override void CreateObject(string definition)
    {
        using SqlCommand createCommand = new($"CREATE FUNCTION {Name} {definition}", Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        // NOTE: The name is passed to OBJECT_ID() as a parameter rather than being interpolated
        //   into a string literal, because it may embed Environment.UserName/MachineName (see
        //   DatabaseObject.GenerateLongName) and an apostrophe in either would break the batch.
        //   The identifier in DROP FUNCTION is already bracket-quoted.
        using SqlCommand dropCommand = new($"IF (OBJECT_ID(@name) IS NOT NULL) DROP FUNCTION {Name}", Connection);

        dropCommand.Parameters.AddWithValue("@name", Name);

        dropCommand.ExecuteNonQuery();
    }
}
