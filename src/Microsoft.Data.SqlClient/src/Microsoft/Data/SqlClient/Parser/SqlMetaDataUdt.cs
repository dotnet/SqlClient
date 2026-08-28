// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Data.SqlClient.Parser;

internal sealed class SqlMetaDataUdt
{
    #if NET
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    #endif
    internal Type Type;

    internal string DatabaseName;

    internal string SchemaName;

    internal string TypeName;

    internal string AssemblyQualifiedName;

    public void CopyFrom(SqlMetaDataUdt original)
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
