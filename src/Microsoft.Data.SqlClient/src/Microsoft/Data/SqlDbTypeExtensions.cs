// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;

namespace Microsoft.Data
{
    /// <include file='../../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlDbTypeExtensions/*' />
    public static class SqlDbTypeExtensions
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlJson[@name="default"]' />
#if NET9_0_OR_GREATER
        public const SqlDbType Json = SqlDbType.Json;
#else
        public const SqlDbType Json = (SqlDbType)35;
#endif
        /// <include file='../../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlVector[@name="default"]' />
#if NET10_0
        public const SqlDbType Vector = SqlDbType.Vector;
#else
        public const SqlDbType Vector = (SqlDbType)36;
#endif
    }
}
