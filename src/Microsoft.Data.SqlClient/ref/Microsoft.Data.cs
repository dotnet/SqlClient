// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../../../doc/snippets/Microsoft.Data/OperationAbortedException.xml' path='docs/members[@name="OperationAbortedException"]/OperationAbortedException/*' />
#if NETFRAMEWORK
[System.Serializable]
#endif
public sealed partial class OperationAbortedException : System.SystemException
{
    internal OperationAbortedException() { }

    #if NETFRAMEWORK
    private OperationAbortedException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    #endif
}

/// <include file='../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlDbTypeExtensions/*' />
public static class SqlDbTypeExtensions
{
    /// <include file='../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlJson[@name="default"]' />
    public const System.Data.SqlDbType Json = (System.Data.SqlDbType)35;
    /// <include file='../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlVector[@name="default"]' />
    public const System.Data.SqlDbType Vector = (System.Data.SqlDbType)36;
}


