// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser.Tokens;

internal sealed class TdsXmlTypeInfo
{
    /// <summary>
    /// Represents the name of the database associated with the XML schema collection.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal string Database;

    /// <summary>
    /// Represents the name of the XML schema collection associated with the type information.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal string Name;

    /// <summary>
    /// Represents the schema that owns the XML schema collection associated with the database.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal string OwningSchema;

    /// <summary>
    /// Creates a new instance of <see cref="TdsXmlTypeInfo"/> and initializes it with the
    /// properties of the current instance.
    /// </summary>
    internal TdsXmlTypeInfo Clone()
    {
        return new TdsXmlTypeInfo
        {
            Database = Database,
            Name = Name,
            OwningSchema = OwningSchema
        };
    }
}
