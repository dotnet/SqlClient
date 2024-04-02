// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Data;
using System.Data.Common;

namespace Microsoft.Data.Sql
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/SqlDataSourceEnumerator/*' />
    public sealed partial class SqlDataSourceEnumerator : DbDataSourceEnumerator
    {
        private static readonly Lazy<SqlDataSourceEnumerator> s_singletonInstance = new(() => new SqlDataSourceEnumerator());

        private SqlDataSourceEnumerator() : base(){}

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/Instance/*' />
        public static SqlDataSourceEnumerator Instance => s_singletonInstance.Value;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/GetDataSources/*' />
        override public DataTable GetDataSources() => GetDataSourcesInternal();

        private partial DataTable GetDataSourcesInternal();
    }
}
