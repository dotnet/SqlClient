// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Data.Common;
using System.Security;
using System.Security.Permissions;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/SqlClientPermissionAttribute/*' />
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    [Serializable]
    public sealed class SqlClientPermissionAttribute : DBDataPermissionAttribute
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/ctor/*' />
        public SqlClientPermissionAttribute(SecurityAction action) : base(action)
        {
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/CreatePermission/*' />
        public override IPermission CreatePermission()
        {
            return new SqlClientPermission(this);
        }
    }
}

#endif
