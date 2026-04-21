// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A transient user-defined type, created at the start of its scope and dropped when disposed.
/// </summary>
public sealed class UserDefinedType : DatabaseObject
{
    /// <summary>
    /// Initializes a new instance of the UserDefinedType class using the specified SQL connection,
    /// name and definition.
    /// </summary>
    /// <remarks>
    /// If a user-defined type with the specified name already exists, it will be dropped automatically
    /// before creation.
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="prefix">The type name.</param>
    /// <param name="definition">The SQL definition of the type.</param>
    public UserDefinedType(SqlConnection connection, string prefix, string definition)
        : base(connection, "[dbo]." + GenerateLongName(prefix), definition, shouldCreate: true, shouldDrop: true)
    {
    }

    protected override void CreateObject(string definition)
    {
        using SqlCommand createCommand = new($"CREATE TYPE {Name} AS {definition}", Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        // Use TYPE_ID instead of OBJECT_ID because OBJECT_ID does not resolve
        // user-defined types, which would silently skip the drop and leak objects.
        using SqlCommand dropCommand = new($"IF TYPE_ID('{Name}') IS NOT NULL DROP TYPE {Name}", Connection);

        dropCommand.ExecuteNonQuery();
    }
}
