// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/PoolBlockingPeriod/*'/>
#if NETFRAMEWORK
    [System.Serializable]
#endif    
    public enum PoolBlockingPeriod
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/Auto/*'/>
        Auto = 0,         // Blocking period OFF for Azure SQL servers, but ON for all other SQL servers.

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/AlwaysBlock/*'/>
        AlwaysBlock = 1,  // Blocking period ON for all SQL servers including Azure SQL servers. 

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/NeverBlock/*'/>
        NeverBlock = 2,   // Blocking period OFF for all SQL servers including Azure SQL servers.
    }
}
