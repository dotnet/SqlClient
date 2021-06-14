// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/SqlFacetAttribute/*' />
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.ReturnValue | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class SqlFacetAttribute : Attribute
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/ctor/*'/>
        public SqlFacetAttribute() { }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/IsFixedLength/*' />
        public bool IsFixedLength
        {
            get;
            set;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/MaxSize/*' />
        public int MaxSize
        {
            get;
            set;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/Precision/*' />
        public int Precision
        {
            get;
            set;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/Scale/*' />
        public int Scale
        {
            get;
            set;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/IsNullable/*' />
        public bool IsNullable
        {
            get;
            set;
        }
    }
}
