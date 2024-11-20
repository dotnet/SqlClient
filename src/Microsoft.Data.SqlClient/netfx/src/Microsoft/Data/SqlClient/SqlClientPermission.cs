// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Security;
using System.Security.Permissions;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/SqlClientPermission/*' />
    [Serializable]
    [Obsolete("Code Access Security is not supported or honored by the runtime.")]
    public sealed class SqlClientPermission : DBDataPermission
    {
        private static readonly string SecurityElementXml = $"<IPermission class=\"{typeof(SqlClientPermission).AssemblyQualifiedName.Replace('\"', '\'')}\" version=\"1\" Unrestricted=\"true\" />";

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ctor[@name="default"]/*' />
        [Obsolete("SqlClientPermission() has been deprecated.  Use the SqlClientPermission(PermissionState.None) constructor.  http://go.microsoft.com/fwlink/?linkid=14202", true)] // MDAC 86034
        public SqlClientPermission() : base(PermissionState.Unrestricted)
        {
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ctor[@name="PermissionState"]/*' />
        public SqlClientPermission(PermissionState state) : base(PermissionState.Unrestricted)
        {
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ctor[@name="PermissionStateAndallowBlankPasswordBool"]/*' />
        [Obsolete("SqlClientPermission(PermissionState state, Boolean allowBlankPassword) has been deprecated.  Use the SqlClientPermission(PermissionState.None) constructor.  http://go.microsoft.com/fwlink/?linkid=14202", true)] // MDAC 86034
        public SqlClientPermission(PermissionState state, bool allowBlankPassword) : base(PermissionState.Unrestricted)
        {
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/Add[@name="connectionStringAndrestrictionsStringAndBehavior"]/*' />
        public override void Add(string connectionString, string restrictions, KeyRestrictionBehavior behavior)
        {
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/Copy/*' />
        public override IPermission Copy()
        {
            return new SqlClientPermission(PermissionState.Unrestricted);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/Intersect/*' />
        public override IPermission Intersect(IPermission target)
        {
            return target.Copy();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/IsSubsetOf/*' />
        public override bool IsSubsetOf(IPermission target)
        {
            return true;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/Union/*' />
        public override IPermission Union(IPermission target)
        {
            return Copy();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/FromXml/*' />
        public override void FromXml(SecurityElement securityElement)
        {
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ToXml/*' />
        public override SecurityElement ToXml()
        {
            return SecurityElement.FromString(SecurityElementXml);
        }
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/SqlClientPermissionAttribute/*' />
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    [Serializable]
    [Obsolete("Code Access Security is not supported or honored by the runtime.")]
    public sealed class SqlClientPermissionAttribute : DBDataPermissionAttribute
    {

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/ctor/*' />
        public SqlClientPermissionAttribute(SecurityAction action) : base(action)
        {
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/CreatePermission/*' />
        public override IPermission CreatePermission()
        {
            return new SqlClientPermission(PermissionState.Unrestricted);
        }
    }
}
