// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlVectorTypeSupport.xml' path='docs/members[@name="SqlVectorTypeSupport"]/SqlVectorTypeSupport/*'/>
#if NETFRAMEWORK
    [System.Serializable]
#endif
    public enum SqlVectorTypeSupport
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlVectorTypeSupport.xml' path='docs/members[@name="SqlVectorTypeSupport"]/Off/*'/>
        Off = 0,  // Vector columns are returned as varchar(max) containing a JSON array.

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlVectorTypeSupport.xml' path='docs/members[@name="SqlVectorTypeSupport"]/V1/*'/>
        V1 = 1,   // Vectors with a float32 base type are exchanged in their binary form.

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlVectorTypeSupport.xml' path='docs/members[@name="SqlVectorTypeSupport"]/V2/*'/>
        V2 = 2,   // Adds float16 to the base types exchanged in their binary form.
    }
}
