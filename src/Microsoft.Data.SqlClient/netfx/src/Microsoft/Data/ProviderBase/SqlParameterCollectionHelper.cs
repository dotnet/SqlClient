// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.Data.Common;

    public sealed partial class SqlParameterCollection : DbParameterCollection
    {
        private List<SqlParameter> _items; // the collection of parameters

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/Count/*' />
        override public int Count
        {
            get
            {
                // NOTE: we don't construct the list just to get the count.
                return ((null != _items) ? _items.Count : 0);
            }
        }

        private List<SqlParameter> InnerList
        {
            get
            {
                List<SqlParameter> items = _items;

                if (null == items)
                {
                    items = new List<SqlParameter>();
                    _items = items;
                }
                return items;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IsFixedSize/*' />
        override public bool IsFixedSize
        {
            get
            {
                return ((System.Collections.IList)InnerList).IsFixedSize;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IsReadOnly/*' />
        override public bool IsReadOnly
        {
            get
            {
                return ((System.Collections.IList)InnerList).IsReadOnly;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IsSynchronized/*' />
        override public bool IsSynchronized
        {
            get
            {
                return ((System.Collections.ICollection)InnerList).IsSynchronized;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SyncRoot/*' />
        override public object SyncRoot
        {
            get
            {
                return ((System.Collections.ICollection)InnerList).SyncRoot;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddValue2/*' />
        [
        EditorBrowsableAttribute(EditorBrowsableState.Never)
        ]
        override public int Add(object value)
        {
            OnChange();  // fire event before value is validated
            ValidateType(value);
            Validate(-1, value);
            InnerList.Add((SqlParameter)value);
            return Count - 1;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddRangeValues1/*' />
        override public void AddRange(System.Array values)
        {
            OnChange();  // fire event before value is validated
            if (null == values)
            {
                throw ADP.ArgumentNull("values");
            }
            foreach (object value in values)
            {
                ValidateType(value);
            }
            foreach (SqlParameter value in values)
            {
                Validate(-1, value);
                InnerList.Add((SqlParameter)value);
            }
        }

        private int CheckName(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw ADP.ParametersSourceIndex(parameterName, this, ItemType);
            }
            return index;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/Clear/*' />
        override public void Clear()
        {
            OnChange();  // fire event before value is validated
            List<SqlParameter> items = InnerList;

            if (null != items)
            {
                foreach (SqlParameter item in items)
                {
                    item.ResetParent();
                }
                items.Clear();
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ContainsValue2/*' />
        override public bool Contains(object value)
        {
            return (-1 != IndexOf(value));
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/CopyToArrayIndex1/*' />
        override public void CopyTo(Array array, int index)
        {
            ((System.Collections.ICollection)InnerList).CopyTo(array, index);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/GetEnumerator/*' />
        override public System.Collections.IEnumerator GetEnumerator()
        {
            return ((System.Collections.ICollection)InnerList).GetEnumerator();
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/GetParameterIndex/*' />
        override protected DbParameter GetParameter(int index)
        {
            RangeCheck(index);
            return InnerList[index];
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/GetParameterParameterName/*' />
        override protected DbParameter GetParameter(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw ADP.ParametersSourceIndex(parameterName, this, ItemType);
            }
            return InnerList[index];
        }

        private static int IndexOf(System.Collections.IEnumerable items, string parameterName)
        {
            if (null != items)
            {
                int i = 0;
                // first case, kana, width sensitive search
                foreach (SqlParameter parameter in items)
                {
                    if (0 == ADP.SrcCompare(parameterName, parameter.ParameterName))
                    {
                        return i;
                    }
                    ++i;
                }
                i = 0;
                // then insensitive search
                foreach (SqlParameter parameter in items)
                {
                    if (0 == ADP.DstCompare(parameterName, parameter.ParameterName))
                    {
                        return i;
                    }
                    ++i;
                }
            }
            return -1;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IndexOfParameterName/*' />
        override public int IndexOf(string parameterName)
        {
            return IndexOf(InnerList, parameterName);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IndexOfValue2/*' />
        override public int IndexOf(object value)
        {
            if (null != value)
            {
                ValidateType(value);

                List<SqlParameter> items = InnerList;

                if (null != items)
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/InsertIndexValue2/*' />
        override public void Insert(int index, object value)
        {
            OnChange();  // fire event before value is validated
            ValidateType(value);
            Validate(-1, (SqlParameter)value);
            InnerList.Insert(index, (SqlParameter)value);
        }

        private void RangeCheck(int index)
        {
            if ((index < 0) || (Count <= index))
            {
                throw ADP.ParametersMappingIndex(index, this);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveValue2/*' />
        override public void Remove(object value)
        {
            OnChange();  // fire event before value is validated
            ValidateType(value);
            int index = IndexOf(value);
            if (-1 != index)
            {
                RemoveIndex(index);
            }
            else if (this != ((SqlParameter)value).CompareExchangeParent(null, this))
            {
                throw ADP.CollectionRemoveInvalidObject(ItemType, this);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveAtIndex/*' />
        override public void RemoveAt(int index)
        {
            OnChange();  // fire event before value is validated
            RangeCheck(index);
            RemoveIndex(index);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveAtParameterName/*' />
        override public void RemoveAt(string parameterName)
        {
            OnChange();  // fire event before value is validated
            int index = CheckName(parameterName);
            RemoveIndex(index);
        }

        private void RemoveIndex(int index)
        {
            List<SqlParameter> items = InnerList;
            Debug.Assert((null != items) && (0 <= index) && (index < Count), "RemoveIndex, invalid");
            SqlParameter item = items[index];
            items.RemoveAt(index);
            item.ResetParent();
        }

        private void Replace(int index, object newValue)
        {
            List<SqlParameter> items = InnerList;
            Debug.Assert((null != items) && (0 <= index) && (index < Count), "Replace Index invalid");
            ValidateType(newValue);
            Validate(index, newValue);
            SqlParameter item = items[index];
            items[index] = (SqlParameter)newValue;
            item.ResetParent();
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SetParameterIndexValue/*' />
        override protected void SetParameter(int index, DbParameter value)
        {
            OnChange();  // fire event before value is validated
            RangeCheck(index);
            Replace(index, value);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SetParameterParameterNameValue/*' />
        override protected void SetParameter(string parameterName, DbParameter value)
        {
            OnChange();  // fire event before value is validated
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw ADP.ParametersSourceIndex(parameterName, this, ItemType);
            }
            Replace(index, value);
        }

        private void Validate(int index, object value)
        {
            if (null == value)
            {
                throw ADP.ParameterNull("value", this, ItemType);
            }
            // Validate assigns the parent - remove clears the parent
            object parent = ((SqlParameter)value).CompareExchangeParent(this, null);
            if (null != parent)
            {
                if (this != parent)
                {
                    throw ADP.ParametersIsNotParent(ItemType, this);
                }
                if (index != IndexOf(value))
                {
                    throw ADP.ParametersIsParent(ItemType, this);
                }
            }
            // generate a ParameterName
            String name = ((SqlParameter)value).ParameterName;
            if (0 == name.Length)
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
            if (null == value)
            {
                throw ADP.ParameterNull("value", this, ItemType);
            }
            else if (!ItemType.IsInstanceOfType(value))
            {
                throw ADP.InvalidParameterType(this, ItemType, value);
            }
        }

    };
}

