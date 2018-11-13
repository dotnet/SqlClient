//------------------------------------------------------------------------------
// <copyright file="SqlClientPermission.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">markash</owner>
// <owner current="true" primary="false">blained</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient {
    using System;
    using System.Security;
    using System.Security.Permissions;
    using System.Data;
    using System.Data.Common;
    using Microsoft.Data.Common;
    using DBDataPermission = System.Data.Common.DBDataPermission;
    using System.Reflection;

    [Serializable] 
    public sealed class SqlClientPermission :  DBDataPermission {

        [ Obsolete("SqlClientPermission() has been deprecated.  Use the SqlClientPermission(PermissionState.None) constructor.  http://go.microsoft.com/fwlink/?linkid=14202", true) ] // MDAC 86034
        public SqlClientPermission() : this(PermissionState.None) {
        }

        public SqlClientPermission(PermissionState state) : base(state) {
        }

        [ Obsolete("SqlClientPermission(PermissionState state, Boolean allowBlankPassword) has been deprecated.  Use the SqlClientPermission(PermissionState.None) constructor.  http://go.microsoft.com/fwlink/?linkid=14202", true) ] // MDAC 86034
        public SqlClientPermission(PermissionState state, bool allowBlankPassword) : this(state) {
            AllowBlankPassword = allowBlankPassword;
        }

        private SqlClientPermission(SqlClientPermission permission) : base(permission) { // for Copy
        }

        internal SqlClientPermission(SqlClientPermissionAttribute permissionAttribute) : base(permissionAttribute) { // for CreatePermission
        }

        internal SqlClientPermission(SqlConnectionString constr) : base(PermissionState.None) { // for Open
            if ((null == constr) || constr.IsEmpty) {
                base.Add(ADP.StrEmpty, ADP.StrEmpty, KeyRestrictionBehavior.AllowOnly);
            }
            else
            {
                AllowBlankPassword = constr.HasBlankPassword; // MDAC 84563
                AddPermissionEntry(constr);
            }
        }

        public override void Add(string connectionString, string restrictions, KeyRestrictionBehavior behavior) {
            DBConnectionString constr = new DBConnectionString(connectionString, restrictions, behavior, SqlConnectionString.GetParseSynonyms(), false);
            AddPermissionEntry(constr);
        }

        private void AddPermissionEntry(object constr)
        {
            Type[] arguments = null;
            if (constr is DBConnectionString)
                arguments = new Type[] { typeof(DBConnectionString) };
            else if(constr is SqlConnectionString)
                arguments = new Type[] { typeof(SqlConnectionString) };
            MethodInfo addPermissionEntryMethodInfo = typeof(DBDataPermission).GetMethod("AddPermissionEntry", arguments);
            addPermissionEntryMethodInfo.Invoke(null, new object[] { constr });
        }

        override public IPermission Copy () {
            return new SqlClientPermission(this);
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false )]
    [Serializable] 
    public sealed class SqlClientPermissionAttribute : DBDataPermissionAttribute {

        public SqlClientPermissionAttribute(SecurityAction action) : base(action) {
        }

        override public IPermission CreatePermission() {
            return new SqlClientPermission(this);
        }
    }
}
