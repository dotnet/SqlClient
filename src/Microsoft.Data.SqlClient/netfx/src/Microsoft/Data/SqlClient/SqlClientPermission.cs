// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security;
using System.Security.Permissions;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Common;
using System.Reflection;
using System.Collections;
using DBDataPermission = System.Data.Common.DBDataPermission;
using System.Globalization;

namespace Microsoft.Data.SqlClient {

    [Serializable] 
    public sealed class SqlClientPermission : System.Data.Common.DBDataPermission
    {

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
            // Explicitly copyFrom to clone the key value pairs
            CopyFrom(permission);
        }

        internal SqlClientPermission(SqlClientPermissionAttribute permissionAttribute) : base(permissionAttribute) { // for CreatePermission
        }

        internal SqlClientPermission(SqlConnectionString constr) : base(PermissionState.None) { // for Open
            if (null != constr)
            {
                AllowBlankPassword = constr.HasBlankPassword; // MDAC 84563
                AddPermissionEntry(new DBConnectionString(constr));
            }
            if ((null == constr) || constr.IsEmpty) {
                base.Add(ADP.StrEmpty, ADP.StrEmpty, KeyRestrictionBehavior.AllowOnly);
            }
        }

        public override void Add(string connectionString, string restrictions, KeyRestrictionBehavior behavior) {
            DBConnectionString constr = new DBConnectionString(connectionString, restrictions, behavior, SqlConnectionString.GetParseSynonyms(), false);
            AddPermissionEntry(constr);
        }

        override public IPermission Copy () {
            return new SqlClientPermission(this);
        }

        // Modifications

        private NameValuePermission _keyvaluetree = NameValuePermission.Default;
        private /*DBConnectionString[]*/ArrayList _keyvalues; // = null;

        internal void AddPermissionEntry(DBConnectionString entry)
        {
            if (null == _keyvaluetree)
            {
                _keyvaluetree = new NameValuePermission();
            }
            if (null == _keyvalues)
            {
                _keyvalues = new ArrayList();
            }
            NameValuePermission.AddEntry(_keyvaluetree, _keyvalues, entry);
            _IsUnrestricted = false; // MDAC 84639
        }

        private bool _IsUnrestricted
        {
            set
            {
                // Use Reflection to access the base class _isUnrestricted. There is no other way to alter this externally.
                FieldInfo fieldInfo = GetType().BaseType.GetField("_isUnrestricted", BindingFlags.Instance
                | BindingFlags.NonPublic);
                fieldInfo.SetValue(this, value);
            }

            get
            {
                return base.IsUnrestricted();
            }
        }

        // Modified CopyFrom to make sure that we copy the Name Value Pair
        private void CopyFrom(SqlClientPermission permission)
        {
            if (!_IsUnrestricted)
            {
                if (null != permission._keyvalues)
                {
                    _keyvalues = (ArrayList)permission._keyvalues.Clone();

                    if (null != permission._keyvaluetree)
                    {
                        _keyvaluetree = permission._keyvaluetree.CopyNameValue();
                    }
                }
            }
        }

        override public IPermission Intersect(IPermission target)
        { // used during Deny actions
            if (null == target)
            {
                return null;
            }
            if (target.GetType() != this.GetType())
            {
                throw ADP.PermissionTypeMismatch();
            }
            if (this.IsUnrestricted())
            { // MDAC 84803, NDPWhidbey 29121
                return target.Copy();
            }

            DBDataPermission operand = (DBDataPermission)target;
            if (operand.IsUnrestricted())
            { // NDPWhidbey 29121
                return this.Copy();
            }

            SqlClientPermission newPermission = (SqlClientPermission)operand.Copy();
            newPermission.AllowBlankPassword &= AllowBlankPassword;

            if ((null != _keyvalues) && (null != newPermission._keyvalues))
            {
                newPermission._keyvalues.Clear();

                newPermission._keyvaluetree.Intersect(newPermission._keyvalues, _keyvaluetree);
            }
            else
            {
                // either target.Add or this.Add have not been called
                // return a non-null object so IsSubset calls will fail
                newPermission._keyvalues = null;
                newPermission._keyvaluetree = null;
            }

            if (newPermission.IsEmpty())
            { // no intersection, MDAC 86773
                newPermission = null;
            }
            return newPermission;
        }

        private bool IsEmpty()
        { // MDAC 84804
            ArrayList keyvalues = _keyvalues;
            bool flag = (!IsUnrestricted() && !AllowBlankPassword && ((null == keyvalues) || (0 == keyvalues.Count)));
            return flag;
        }

        override public bool IsSubsetOf(IPermission target)
        {
            if (null == target)
            {
                return IsEmpty();
            }
            if (target.GetType() != this.GetType())
            {
                throw ADP.PermissionTypeMismatch();
            }

            SqlClientPermission superset = (target as SqlClientPermission);

            bool subset = superset.IsUnrestricted();
            if (!subset)
            {
                if (!IsUnrestricted() &&
                    (!AllowBlankPassword || superset.AllowBlankPassword) &&
                    ((null == _keyvalues) || (null != superset._keyvaluetree)))
                {

                    subset = true;
                    if (null != _keyvalues)
                    {
                        foreach (DBConnectionString kventry in _keyvalues)
                        {
                            if (!superset._keyvaluetree.CheckValueForKeyPermit(kventry))
                            {
                                subset = false;
                                break;
                            }
                        }
                    }
                }
            }
            return subset;
        }

        override public IPermission Union(IPermission target)
        {
            if (null == target)
            {
                return this.Copy();
            }
            if (target.GetType() != this.GetType())
            {
                throw ADP.PermissionTypeMismatch();
            }
            if (IsUnrestricted())
            { // MDAC 84803
                return this.Copy();
            }

            SqlClientPermission newPermission = (SqlClientPermission)target.Copy();
            if (!newPermission.IsUnrestricted())
            {
                newPermission.AllowBlankPassword |= AllowBlankPassword;

                if (null != _keyvalues)
                {
                    foreach (DBConnectionString entry in _keyvalues)
                    {
                        newPermission.AddPermissionEntry(entry);
                    }
                }
            }
            return (newPermission.IsEmpty() ? null : newPermission);
        }

        private string DecodeXmlValue(string value)
        {
            if ((null != value) && (0 < value.Length))
            {
                value = value.Replace("&quot;", "\"");
                value = value.Replace("&apos;", "\'");
                value = value.Replace("&lt;", "<");
                value = value.Replace("&gt;", ">");
                value = value.Replace("&amp;", "&");
            }
            return value;
        }

        private string EncodeXmlValue(string value)
        {
            if ((null != value) && (0 < value.Length))
            {
                value = value.Replace('\0', ' '); // assumption that '\0' will only be at end of string
                value = value.Trim();
                value = value.Replace("&", "&amp;");
                value = value.Replace(">", "&gt;");
                value = value.Replace("<", "&lt;");
                value = value.Replace("\'", "&apos;");
                value = value.Replace("\"", "&quot;");
            }
            return value;
        }

        // <IPermission class="...Permission" version="1" AllowBlankPassword=false>
        //     <add ConnectionString="provider=x;data source=y;" KeyRestrictions="address=;server=" KeyRestrictionBehavior=PreventUsage/>
        // </IPermission>
        override public void FromXml(SecurityElement securityElement)
        {
            // code derived from CodeAccessPermission.ValidateElement
            if (null == securityElement)
            {
                throw ADP.ArgumentNull("securityElement");
            }
            string tag = securityElement.Tag;
            if (!tag.Equals(XmlStr._Permission) && !tag.Equals(XmlStr._IPermission))
            {
                throw ADP.NotAPermissionElement();
            }
            String version = securityElement.Attribute(XmlStr._Version);
            if ((null != version) && !version.Equals(XmlStr._VersionNumber))
            {
                throw ADP.InvalidXMLBadVersion();
            }

            string unrestrictedValue = securityElement.Attribute(XmlStr._Unrestricted);
            _IsUnrestricted = (null != unrestrictedValue) && Boolean.Parse(unrestrictedValue);

            Clear(); // MDAC 83105
            if (!_IsUnrestricted)
            {
                string allowNull = securityElement.Attribute(XmlStr._AllowBlankPassword);
                AllowBlankPassword = (null != allowNull) && Boolean.Parse(allowNull);

                ArrayList children = securityElement.Children;
                if (null != children)
                {
                    foreach (SecurityElement keyElement in children)
                    {
                        tag = keyElement.Tag;
                        if ((XmlStr._add == tag) || ((null != tag) && (XmlStr._add == tag.ToLower(CultureInfo.InvariantCulture))))
                        {
                            string constr = keyElement.Attribute(XmlStr._ConnectionString);
                            string restrt = keyElement.Attribute(XmlStr._KeyRestrictions);
                            string behavr = keyElement.Attribute(XmlStr._KeyRestrictionBehavior);

                            KeyRestrictionBehavior behavior = KeyRestrictionBehavior.AllowOnly;
                            if (null != behavr)
                            {
                                behavior = (KeyRestrictionBehavior)Enum.Parse(typeof(KeyRestrictionBehavior), behavr, true);
                            }
                            constr = DecodeXmlValue(constr);
                            restrt = DecodeXmlValue(restrt);
                            Add(constr, restrt, behavior);
                        }
                    }
                }
            }
            else
            {
                AllowBlankPassword = false;
            }
        }

        // <IPermission class="...Permission" version="1" AllowBlankPassword=false>
        //     <add ConnectionString="provider=x;data source=y;"/>
        //     <add ConnectionString="provider=x;data source=y;" KeyRestrictions="user id=;password=;" KeyRestrictionBehavior=AllowOnly/>
        //     <add ConnectionString="provider=x;data source=y;" KeyRestrictions="address=;server=" KeyRestrictionBehavior=PreventUsage/>
        // </IPermission>
        override public SecurityElement ToXml()
        {
            Type type = this.GetType();
            SecurityElement root = new SecurityElement(XmlStr._IPermission);
            root.AddAttribute(XmlStr._class, type.AssemblyQualifiedName.Replace('\"', '\''));
            root.AddAttribute(XmlStr._Version, XmlStr._VersionNumber);

            if (IsUnrestricted())
            {
                root.AddAttribute(XmlStr._Unrestricted, XmlStr._true);
            }
            else
            {
                root.AddAttribute(XmlStr._AllowBlankPassword, AllowBlankPassword.ToString(CultureInfo.InvariantCulture));

                if (null != _keyvalues)
                {
                    foreach (DBConnectionString value in _keyvalues)
                    {
                        SecurityElement valueElement = new SecurityElement(XmlStr._add);
                        string tmp;

                        tmp = value.ConnectionString; // WebData 97375
                        tmp = EncodeXmlValue(tmp);
                        if (!ADP.IsEmpty(tmp))
                        {
                            valueElement.AddAttribute(XmlStr._ConnectionString, tmp);
                        }
                        tmp = value.Restrictions;
                        tmp = EncodeXmlValue(tmp);
                        if (null == tmp) { tmp = ADP.StrEmpty; }
                        valueElement.AddAttribute(XmlStr._KeyRestrictions, tmp);

                        tmp = value.Behavior.ToString();
                        valueElement.AddAttribute(XmlStr._KeyRestrictionBehavior, tmp);

                        root.AddChild(valueElement);
                    }
                }
            }
            return root;
        }

        private static class XmlStr
        {
            internal const string _class = "class";
            internal const string _IPermission = "IPermission";
            internal const string _Permission = "Permission";
            internal const string _Unrestricted = "Unrestricted";
            internal const string _AllowBlankPassword = "AllowBlankPassword";
            internal const string _true = "true";
            internal const string _Version = "version";
            internal const string _VersionNumber = "1";

            internal const string _add = "add";

            internal const string _ConnectionString = "ConnectionString";
            internal const string _KeyRestrictions = "KeyRestrictions";
            internal const string _KeyRestrictionBehavior = "KeyRestrictionBehavior";
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
