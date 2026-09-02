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
    /// Copies the properties of the specified <see cref="TdsXmlTypeInfo"/> instance
    /// to the current instance.
    /// </summary>
    /// <param name="original">
    /// The <see cref="TdsXmlTypeInfo"/> instance from which to copy properties.
    /// </param>
    public void CopyFrom(TdsXmlTypeInfo original)
    {
        if (original != null)
        {
            Database = original.Database;
            OwningSchema = original.OwningSchema;
            Name = original.Name;
        }
    }
}
