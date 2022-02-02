// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.SqlServer.Server
{
    /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFacetAttribute.xml' path='Type[@Name="SqlFacetAttribute"]/Docs/*' />
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.ReturnValue | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class SqlFacetAttribute : Attribute
    {
        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFacetAttribute.xml' path='Type[@Name="SqlFacetAttribute"]/Members/Member[@MemberName=".ctor"]/Docs/*' />
        public SqlFacetAttribute() { }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFacetAttribute.xml' path='Type[@Name="SqlFacetAttribute"]/Members/Member[@MemberName="IsFixedLength"]/Docs/*' />
        public bool IsFixedLength
        {
            get;
            set;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFacetAttribute.xml' path='Type[@Name="SqlFacetAttribute"]/Members/Member[@MemberName="MaxSize"]/Docs/*' />
        public int MaxSize
        {
            get;
            set;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFacetAttribute.xml' path='Type[@Name="SqlFacetAttribute"]/Members/Member[@MemberName="Precision"]/Docs/*' />
        public int Precision
        {
            get;
            set;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFacetAttribute.xml' path='Type[@Name="SqlFacetAttribute"]/Members/Member[@MemberName="Scale"]/Docs/*' />
        public int Scale
        {
            get;
            set;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFacetAttribute.xml' path='Type[@Name="SqlFacetAttribute"]/Members/Member[@MemberName="IsNullable"]/Docs/*' />
        public bool IsNullable
        {
            get;
            set;
        }
    }
}
