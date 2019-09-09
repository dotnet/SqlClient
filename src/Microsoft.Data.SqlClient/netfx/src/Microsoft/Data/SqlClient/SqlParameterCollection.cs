// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data.Common;
using System.Data;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// 
    /// </summary>
    [Editor("Microsoft.VSDesigner.Data.Design.DBParametersEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
    ListBindable(false)]
    public sealed partial class SqlParameterCollection : DbParameterCollection
    {
        private bool _isDirty;
        private static Type ItemType = typeof(SqlParameter);

        internal SqlParameterCollection() : base()
        {
        }

        internal bool IsDirty
        {
            get
            {
                return _isDirty;
            }
            set
            {
                _isDirty = value;
            }
        }

        [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        new public SqlParameter this[int index]
        {
            get
            {
                return (SqlParameter)GetParameter(index);
            }
            set
            {
                SetParameter(index, value);
            }
        }

        [Browsable(false),DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        new public SqlParameter this[string parameterName]
        {
            get
            {
                return (SqlParameter)GetParameter(parameterName);
            }
            set
            {
                SetParameter(parameterName, value);
            }
        }

        public SqlParameter Add(SqlParameter value)
        {
            Add((object)value);
            return value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [EditorBrowsableAttribute(EditorBrowsableState.Never)]
        [ObsoleteAttribute("Add(String parameterName, Object value) has been deprecated.  Use AddWithValue(String parameterName, Object value).  http://go.microsoft.com/fwlink/?linkid=14202", false)] // 79027
        public SqlParameter Add(string parameterName, object value)
        {
            return Add(new SqlParameter(parameterName, value));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public SqlParameter AddWithValue(string parameterName, object value)
        { // 79027
            return Add(new SqlParameter(parameterName, value));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="sqlDbType"></param>
        /// <returns></returns>
        public SqlParameter Add(string parameterName, SqlDbType sqlDbType)
        {
            return Add(new SqlParameter(parameterName, sqlDbType));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="sqlDbType"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public SqlParameter Add(string parameterName, SqlDbType sqlDbType, int size)
        {
            return Add(new SqlParameter(parameterName, sqlDbType, size));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="sqlDbType"></param>
        /// <param name="size"></param>
        /// <param name="sourceColumn"></param>
        /// <returns></returns>
        public SqlParameter Add(string parameterName, SqlDbType sqlDbType, int size, string sourceColumn)
        {
            return Add(new SqlParameter(parameterName, sqlDbType, size, sourceColumn));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="values"></param>
        public void AddRange(SqlParameter[] values)
        {
            AddRange((Array)values);
        }

        override public bool Contains(string value)
        { // WebData 97349
            return (-1 != IndexOf(value));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Contains(SqlParameter value)
        {
            return (-1 != IndexOf(value));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        public void CopyTo(SqlParameter[] array, int index)
        {
            CopyTo((Array)array, index);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int IndexOf(SqlParameter value)
        {
            return IndexOf((object)value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public void Insert(int index, SqlParameter value)
        {
            Insert(index, (object)value);
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnChange()
        {
            IsDirty = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public void Remove(SqlParameter value)
        {
            Remove((object)value);
        }

    }
}
