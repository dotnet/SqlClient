// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SqlParameterCollection/*' />
    [
    Editor("Microsoft.VSDesigner.Data.Design.DBParametersEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
    ListBindable(false)
    ]
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ItemIndex/*' />
        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
        ]
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ItemParameterName/*' />
        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
        ]
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddValue1/*' />
        public SqlParameter Add(SqlParameter value)
        {
            Add((object)value);
            return value;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameValue/*' />
        [EditorBrowsableAttribute(EditorBrowsableState.Never)]
        [ObsoleteAttribute("Add(String parameterName, Object value) has been deprecated.  Use AddWithValue(String parameterName, Object value).  http://go.microsoft.com/fwlink/?linkid=14202", false)] // 79027
        public SqlParameter Add(string parameterName, object value)
        {
            return Add(new SqlParameter(parameterName, value));
        }
        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddWithValue/*' />
        public SqlParameter AddWithValue(string parameterName, object value)
        { // 79027
            return Add(new SqlParameter(parameterName, value));
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameSqlDbType/*' />
        public SqlParameter Add(string parameterName, SqlDbType sqlDbType)
        {
            return Add(new SqlParameter(parameterName, sqlDbType));
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameSqlDbTypeSize/*' />
        public SqlParameter Add(string parameterName, SqlDbType sqlDbType, int size)
        {
            return Add(new SqlParameter(parameterName, sqlDbType, size));
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameSqlDbTypeSizeSourceColumn/*' />
        public SqlParameter Add(string parameterName, SqlDbType sqlDbType, int size, string sourceColumn)
        {
            return Add(new SqlParameter(parameterName, sqlDbType, size, sourceColumn));
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddRangeValues2/*' />
        public void AddRange(SqlParameter[] values)
        {
            AddRange((Array)values);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ContainsValue3/*' />
        override public bool Contains(string value)
        { // WebData 97349
            return (-1 != IndexOf(value));
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ContainsValue1/*' />
        public bool Contains(SqlParameter value)
        {
            return (-1 != IndexOf(value));
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/CopyToArrayIndex2/*' />
        public void CopyTo(SqlParameter[] array, int index)
        {
            CopyTo((Array)array, index);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IndexOfValue1/*' />
        public int IndexOf(SqlParameter value)
        {
            return IndexOf((object)value);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/InsertIndexValue1/*' />
        public void Insert(int index, SqlParameter value)
        {
            Insert(index, (object)value);
        }

        private void OnChange()
        {
            IsDirty = true;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveValue1/*' />
        public void Remove(SqlParameter value)
        {
            Remove((object)value);
        }

    }
}
