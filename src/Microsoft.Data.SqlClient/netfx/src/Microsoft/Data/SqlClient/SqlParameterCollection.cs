//------------------------------------------------------------------------------
// <copyright file="SqlParameterCollection.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">blained</owner>
// <owner current="true" primary="false">markash</owner>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Data.Common;
using System.Data;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient {


    [
    Editor("Microsoft.VSDesigner.Data.Design.DBParametersEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
    ListBindable(false)
    ]
    public sealed partial class SqlParameterCollection : DbParameterCollection {
        private bool _isDirty;
        private static Type ItemType = typeof(SqlParameter);
        private System.Data.SqlClient.SqlParameterCollection _sysSqlParameterCollection;

        internal SqlParameterCollection() : base() {
        }

        internal SqlParameterCollection(System.Data.SqlClient.SqlParameterCollection sqlParameterCollection){
            SysSqlParameterCollection = sqlParameterCollection;
        }


        internal System.Data.SqlClient.SqlParameterCollection SysSqlParameterCollection {
            get {
                return _sysSqlParameterCollection;
            }
            set {
                _sysSqlParameterCollection = value;
            }
        }

        internal bool IsDirty {
            get {
                return _isDirty;
            }
            set {
                _isDirty = value;
            }
        }

        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
        ]
        new public SqlParameter this[int index] {
            get {
                if (SysSqlParameterCollection != null)
                {
                    return new SqlParameter(SysSqlParameterCollection[index]);
                }
                else
                {
                    return (SqlParameter)GetParameter(index);
                }
            }
            set {
                SetParameter(index, value);
            }
        }

        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
        ]
        new public SqlParameter this[string parameterName] {
            get {
                if (SysSqlParameterCollection != null)
                {
                    return new SqlParameter(SysSqlParameterCollection[parameterName]);
                }
                else
                {
                    return (SqlParameter)GetParameter(parameterName);
                }
            }
            set {
                if (SysSqlParameterCollection != null)
                {
                    System.Data.SqlClient.SqlParameter holdMDSqlParamVal = new System.Data.SqlClient.SqlParameter(value.ParameterName, value.Value);
                    SysSqlParameterCollection[parameterName] = holdMDSqlParamVal;
                }
                else
                {
                    SetParameter(parameterName, value);
                }
            }
        }

        public SqlParameter Add(SqlParameter value) {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.Add(new System.Data.SqlClient.SqlParameter(value.ParameterName, value.Value));
                return value;
            }
            else
            {
                Add((object)value);
                return value;
            }
        }

        [ EditorBrowsableAttribute(EditorBrowsableState.Never) ] 
        [ ObsoleteAttribute("Add(String parameterName, Object value) has been deprecated.  Use AddWithValue(String parameterName, Object value).  http://go.microsoft.com/fwlink/?linkid=14202", false) ] // 79027
        public SqlParameter Add(string parameterName, object value) {          
            return (SysSqlParameterCollection != null) ? new SqlParameter(SysSqlParameterCollection.Add(parameterName, value)) : Add(new SqlParameter(parameterName, value));
        }

        public SqlParameter AddWithValue(string parameterName, object value) { // 79027
            return (SysSqlParameterCollection != null) ? new SqlParameter(SysSqlParameterCollection.AddWithValue(parameterName, value)) : Add(new SqlParameter(parameterName, value));
        }

        public SqlParameter Add(string parameterName, SqlDbType sqlDbType) {
            return (SysSqlParameterCollection != null) ? new SqlParameter(SysSqlParameterCollection.Add(parameterName, sqlDbType)) : Add(new SqlParameter(parameterName, sqlDbType));
        }

        public SqlParameter Add(string parameterName, SqlDbType sqlDbType, int size) {
            return (SysSqlParameterCollection != null) ? new SqlParameter(SysSqlParameterCollection.Add(parameterName, sqlDbType, size)) : Add(new SqlParameter(parameterName, sqlDbType, size));
        }

        public SqlParameter Add(string parameterName, SqlDbType sqlDbType, int size, string sourceColumn) {            
            return (SysSqlParameterCollection != null) ? new SqlParameter(SysSqlParameterCollection.Add(parameterName, sqlDbType, size, sourceColumn)) : Add(new SqlParameter(parameterName, sqlDbType, size, sourceColumn));
        }

        public void AddRange(SqlParameter[] values) {
            if (SysSqlParameterCollection != null)
            {
                List<System.Data.SqlClient.SqlParameter> hldMdParamValues = new List<System.Data.SqlClient.SqlParameter>();
                foreach (var paramVal in values)
                    hldMdParamValues.Add(new System.Data.SqlClient.SqlParameter(paramVal.ParameterName, paramVal.Value));

                SysSqlParameterCollection.AddRange(hldMdParamValues.ToArray());
            }
            else
            {
                AddRange((Array)values);
            }
        }

        override public bool Contains(string value) { // WebData 97349
            return SysSqlParameterCollection?.Contains(value) ?? (-1 != IndexOf(value));
        }

        public bool Contains(SqlParameter value) {
            return SysSqlParameterCollection?.Contains(new System.Data.SqlClient.SqlParameter(value.ParameterName, value.Value)) ?? (-1 != IndexOf(value));
        }

        public void CopyTo(SqlParameter[] array, int index) {
            if (SysSqlParameterCollection != null)
            {
                System.Data.SqlClient.SqlParameter[] sysParamArray = new System.Data.SqlClient.SqlParameter[SysSqlParameterCollection.Count];
                SysSqlParameterCollection.CopyTo(sysParamArray, index);

                //Convert to an array of M.D.SqlClient.SqlParameter
                List<SqlParameter> sysList = new List<SqlParameter>();
                foreach (System.Data.SqlClient.SqlParameter item in sysParamArray)
                {
                    sysList.Add(new SqlParameter(item.ParameterName, item.Value));
                }

                sysList.ToArray().CopyTo(array, index);
            }
            else
            {
                CopyTo((Array)array, index);
            }
        }
        
        public int IndexOf(SqlParameter value) {
            return SysSqlParameterCollection?.IndexOf(new System.Data.SqlClient.SqlParameter(value.ParameterName, value.Value)) ?? IndexOf((object)value);
        }
    
        public void Insert(int index, SqlParameter value) {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.Insert(index, new System.Data.SqlClient.SqlParameter(value.ParameterName, value.Value));
            }
            else
            {
                Insert(index, (object)value);
            }            
        }

        private void OnChange() {
            IsDirty = true;
        }

        public void Remove(SqlParameter value) {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.Remove(new System.Data.SqlClient.SqlParameter(value.ParameterName, value.Value));
            }
            else
            {
                Remove((object)value);
            }
        }    

    }
}
