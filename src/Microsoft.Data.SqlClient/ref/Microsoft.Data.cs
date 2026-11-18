// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data;

/// <include file='../../../doc/snippets/Microsoft.Data/OperationAbortedException.xml' path='docs/members[@name="OperationAbortedException"]/OperationAbortedException/*' />
[System.SerializableAttribute]
[System.Runtime.CompilerServices.TypeForwardedFromAttribute("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
public sealed class OperationAbortedException : System.SystemException
{
    private OperationAbortedException(string message, System.Exception innerException) { }

    #if NET
    [System.ObsoleteAttribute]
    #endif
    private OperationAbortedException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
}

/// <include file='../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlDbTypeExtensions/*' />
public static class SqlDbTypeExtensions
{
    /// <include file='../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlJson[@name="default"]/*' />
    public const System.Data.SqlDbType Json = (System.Data.SqlDbType)35;
    /// <include file='../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlVector[@name="default"]/*' />
    public const System.Data.SqlDbType Vector = (System.Data.SqlDbType)36;
}

