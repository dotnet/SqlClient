// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Represents UDT metadata inside a TYPE_INFO TDS object used by metadata and return value tokens.
/// </summary>
internal sealed class TdsUdtTypeInfo
{
    /// <summary>
    /// Represents the fully qualified type name of the UDT, including the name of the assembly
    /// in which the type is defined.
    /// </summary>
    internal string AssemblyQualifiedName;

    /// <summary>
    /// Represents the name of the database associated with the UDT.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal string DatabaseName;

    /// <summary>
    /// Represents the schema name of the UDT within the TYPE_INFO TDS object.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal string SchemaName;

    /// <summary>
    /// Represents the name of the UDT as specified in the TYPE_INFO TDS object.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal string TypeName;

    /// <summary>
    /// Represents the data type the UDT, providing metadata necessary for runtime type resolution
    /// and compatibility checks.
    /// </summary>
    #if NET
    [field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    #endif
    internal Type Type { get; set; }

    /// <summary>
    /// Copies the information from the provided <see cref="TdsUdtTypeInfo"/> instance to the current instance.
    /// </summary>
    /// <param name="original">
    /// The <see cref="TdsUdtTypeInfo"/> instance from which to copy the information.
    /// </param>
    public void CopyFrom(TdsUdtTypeInfo original)
    {
        if (original != null)
        {
            Type = original.Type;
            DatabaseName = original.DatabaseName;
            SchemaName = original.SchemaName;
            TypeName = original.TypeName;
            AssemblyQualifiedName = original.AssemblyQualifiedName;
        }
    }
}
