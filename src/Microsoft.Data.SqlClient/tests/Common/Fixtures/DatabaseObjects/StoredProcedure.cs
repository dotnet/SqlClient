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

    protected override void CreateObject(string definition)
    {
        using SqlCommand createCommand = new($"CREATE PROCEDURE {Name} {definition}", Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        using SqlCommand dropCommand = new($"IF (OBJECT_ID('{Name}') IS NOT NULL) DROP PROCEDURE {Name}", Connection);

        dropCommand.ExecuteNonQuery();
    }
}
