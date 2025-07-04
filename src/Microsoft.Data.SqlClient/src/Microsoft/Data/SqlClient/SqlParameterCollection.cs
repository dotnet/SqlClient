// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Common;

// NOTE: The current Microsoft.VSDesigner editor attributes are implemented for System.Data.SqlClient, and are not publicly available.
// New attributes that are designed to work with Microsoft.Data.SqlClient and are publicly documented should be included in future.
namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SqlParameterCollection/*' />    
    [
    ListBindable(false)
    ]
    public sealed partial class SqlParameterCollection : DbParameterCollection
    {
        private List<SqlParameter> _items;
        private bool _isDirty;

        internal SqlParameterCollection() : base()
        {
        }

        internal SqlParameterCollection(int capacity)
            : this()
        {
            _items = new List<SqlParameter>(Math.Max(capacity, 1));
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ItemIndex/*' />
        public new SqlParameter this[int index]
        {
            get => (SqlParameter)GetParameter(index);
            set => SetParameter(index, value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ItemParameterName/*' />
        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
        ]
        public new SqlParameter this[string parameterName]
        {
            get => (SqlParameter)GetParameter(parameterName);
            set => SetParameter(parameterName, value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddValue1/*' />
        public SqlParameter Add(SqlParameter value)
        {
            Add((object)value);
            return value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddWithValue/*' />
        public SqlParameter AddWithValue(string parameterName, object value) => Add(new SqlParameter(parameterName, value));

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameSqlDbType/*' />
        public SqlParameter Add(string parameterName, SqlDbType sqlDbType) => Add(new SqlParameter(parameterName, sqlDbType));

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameSqlDbTypeSize/*' />
        public SqlParameter Add(string parameterName, SqlDbType sqlDbType, int size) => Add(new SqlParameter(parameterName, sqlDbType, size));

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameSqlDbTypeSizeSourceColumn/*' />
        public SqlParameter Add(string parameterName, SqlDbType sqlDbType, int size, string sourceColumn) => Add(new SqlParameter(parameterName, sqlDbType, size, sourceColumn));

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddRangeValues2/*' />
        public void AddRange(SqlParameter[] values) => AddRange((Array)values);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ContainsValue3/*' />
        public override bool Contains(string value) => IndexOf(value) != -1;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ContainsValue1/*' />
        public bool Contains(SqlParameter value) => IndexOf(value) != -1;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/CopyToArrayIndex2/*' />
        public void CopyTo(SqlParameter[] array, int index) => CopyTo((Array)array, index);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IndexOfValue1/*' />
        public int IndexOf(SqlParameter value) => IndexOf((object)value);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/InsertIndexValue1/*' />
        public void Insert(int index, SqlParameter value) => Insert(index, (object)value);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveValue1/*' />
        public void Remove(SqlParameter value) => Remove((object)value);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/Count/*' />
        public override int Count => _items?.Count ?? 0;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IsFixedSize/*' />
        public override bool IsFixedSize => ((System.Collections.IList)InnerList).IsFixedSize;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IsReadOnly/*' />
        public override bool IsReadOnly => ((System.Collections.IList)InnerList).IsReadOnly;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IsSynchronized/*' />
        public override bool IsSynchronized => ((System.Collections.ICollection)InnerList).IsSynchronized;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SyncRoot/*' />
        public override object SyncRoot => ((System.Collections.ICollection)InnerList).SyncRoot;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddValue2/*' />      
        [EditorBrowsableAttribute(EditorBrowsableState.Never)]
        public override int Add(object value)
        {
            IsDirty = true;
            ValidateType(value);
            Validate(-1, value);
            InnerList.Add((SqlParameter)value);
            return Count - 1;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddRangeValues1/*' />
        public override void AddRange(Array values)
        {
            IsDirty = true;
            if (values == null)
            {
                throw ADP.ArgumentNull(nameof(values));
            }
            foreach (object value in values)
            {
                ValidateType(value);
            }
            foreach (SqlParameter value in values)
            {
                Validate(-1, value);
                InnerList.Add(value);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/Clear/*' />
        public override void Clear()
        {
            IsDirty = true;
            List<SqlParameter> items = InnerList;

            if (items != null)
            {
                foreach (SqlParameter item in items)
                {
                    item.ResetParent();
                }
                items.Clear();
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ContainsValue2/*' />
        public override bool Contains(object value) => IndexOf(value) != -1;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/CopyToArrayIndex1/*' />
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)InnerList).CopyTo(array, index);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/GetEnumerator/*' />
        public override System.Collections.IEnumerator GetEnumerator() => ((System.Collections.ICollection)InnerList).GetEnumerator();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IndexOfParameterName/*' />
        public override int IndexOf(string parameterName)
        {
            List<SqlParameter> items = InnerList;
            if (items != null)
            {
                int i = 0;

                foreach (SqlParameter parameter in items)
                {
                    if (parameterName == parameter.ParameterName)
                    {
                        return i;
                    }
                    ++i;
                }
                i = 0;

                foreach (SqlParameter parameter in items)
                {
                    if (CultureInfo.CurrentCulture.CompareInfo.Compare(parameterName, parameter.ParameterName, ADP.DefaultCompareOptions) == 0)
                    {
                        return i;
                    }
                    ++i;
                }
            }
            return -1;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IndexOfValue2/*' />
        public override int IndexOf(object value)
        {
            if (value != null)
            {
                ValidateType(value);

                List<SqlParameter> items = InnerList;

                if (items != null)
                {
                    int count = items.Count;

                    for (int i = 0; i < count; i++)
                    {
                        if (value == items[i])
                        {
                            return i;
                        }
                    }
                }
            }
            return -1;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/InsertIndexValue2/*' />
        public override void Insert(int index, object value)
        {
            IsDirty = true;
            ValidateType(value);
            Validate(-1, (SqlParameter)value);
            InnerList.Insert(index, (SqlParameter)value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveValue2/*' />
        public override void Remove(object value)
        {
            IsDirty = true;
            ValidateType(value);
            int index = IndexOf(value);
            if (index != -1)
            {
                RemoveIndex(index);
            }
            else if (((SqlParameter)value).CompareExchangeParent(null, this) != this)
            {
                throw ADP.CollectionRemoveInvalidObject(typeof(SqlParameter), this);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveAtIndex/*' />
        public override void RemoveAt(int index)
        {
            IsDirty = true;
            RangeCheck(index);
            RemoveIndex(index);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveAtParameterName/*' />
        public override void RemoveAt(string parameterName)
        {
            IsDirty = true;
            int index = CheckName(parameterName);
            RemoveIndex(index);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SetParameterIndexValue/*' />
        protected override void SetParameter(int index, DbParameter value)
        {
            IsDirty = true;
            RangeCheck(index);
            Replace(index, value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SetParameterParameterNameValue/*' />
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            IsDirty = true;
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw ADP.ParametersSourceIndex(parameterName, this, typeof(SqlParameter));
            }
            Replace(index, value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/GetParameterIndex/*' />
        protected override DbParameter GetParameter(int index)
        {
            RangeCheck(index);
            return InnerList[index];
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/GetParameterParameterName/*' />
        protected override DbParameter GetParameter(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw ADP.ParametersSourceIndex(parameterName, this, typeof(SqlParameter));
            }
            return InnerList[index];
        }

        private List<SqlParameter> InnerList
        {
            get
            {
                List<SqlParameter> items = _items;

                if (items == null)
                {
                    items = new List<SqlParameter>();
                    _items = items;
                }
                return items;
            }
        }

        internal bool IsDirty
        {
            get => _isDirty;
            set => _isDirty = value;
        }

        private void RangeCheck(int index)
        {
            if ((index < 0) || (Count <= index))
            {
                throw ADP.ParametersMappingIndex(index, this);
            }
        }

        private int CheckName(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw ADP.ParametersSourceIndex(parameterName, this, typeof(SqlParameter));
            }
            return index;
        }

        private void RemoveIndex(int index)
        {
            List<SqlParameter> items = InnerList;
            Debug.Assert((items != null) && (0 <= index) && (index < Count), "RemoveIndex, invalid");
            SqlParameter item = items[index];
            items.RemoveAt(index);
            item.ResetParent();
        }

        private void Replace(int index, object newValue)
        {
            List<SqlParameter> items = InnerList;
            Debug.Assert((items != null) && (0 <= index) && (index < Count), "Replace Index invalid");
            ValidateType(newValue);
            Validate(index, newValue);
            SqlParameter item = items[index];
            items[index] = (SqlParameter)newValue;
            item.ResetParent();
        }


        private void Validate(int index, object value)
        {
            if (value == null)
            {
                throw ADP.ParameterNull(nameof(value), this, typeof(SqlParameter));
            }

            object parent = ((SqlParameter)value).CompareExchangeParent(this, null);
            if (parent != null)
            {
                if (this != parent)
                {
                    throw ADP.ParametersIsNotParent(typeof(SqlParameter), this);
                }
                if (index != IndexOf(value))
                {
                    throw ADP.ParametersIsParent(typeof(SqlParameter), this);
                }
            }

            string name = ((SqlParameter)value).ParameterName;
            if (name.Length == 0)
            {
                index = 1;
                do
                {
                    name = ADP.Parameter + index.ToString(CultureInfo.CurrentCulture);
                    index++;
                } while (-1 != IndexOf(name));
                ((SqlParameter)value).ParameterName = name;
            }
        }

        private void ValidateType(object value)
        {
            if (value is null)
            {
                throw ADP.ParameterNull(nameof(value), this, typeof(SqlParameter));
            }
            else if (!typeof(SqlParameter).IsInstanceOfType(value))
            {
                throw ADP.InvalidParameterType(this, typeof(SqlParameter), value);
            }
        }
    }
}
