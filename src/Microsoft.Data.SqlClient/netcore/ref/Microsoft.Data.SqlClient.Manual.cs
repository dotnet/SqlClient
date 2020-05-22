// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlCommand : System.Data.Common.DbCommand
    {
        // SqlCommand expects IDisposable methods to be implemented via System.ComponentModel.Component, which it no longer inherits from
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Dispose/*'/>
        override protected void Dispose(bool disposing) { }
    }
    public sealed partial class SqlConnection : System.Data.Common.DbConnection
    {
        // SqlConnection expects IDisposable methods to be implemented via System.ComponentModel.Component, which it no longer inherits from
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Dispose/*'/>
        override protected void Dispose(bool disposing) { }
    }
    [System.ComponentModel.TypeConverter(typeof(SqlParameterConverter))]
    public sealed partial class SqlParameter
    {
        internal class SqlParameterConverter { }
    }
}
