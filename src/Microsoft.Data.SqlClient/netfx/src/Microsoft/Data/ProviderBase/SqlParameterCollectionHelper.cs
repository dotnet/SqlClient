//------------------------------------------------------------------------------
// <copyright file="DbParameterCollectionBase.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">stevesta</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Data;
    using Microsoft.Data.Common;
    using Microsoft.Data.ProviderBase;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Data.Common;

    public sealed partial class SqlParameterCollection : DbParameterCollection {
        private List<SqlParameter> _items; // the collection of parameters

        override public int Count {
            get {
                if (SysSqlParameterCollection != null)
                {
                    return SysSqlParameterCollection.Count;
                }
                else
                {
                    // NOTE: we don't construct the list just to get the count.
                    return ((null != _items) ? _items.Count : 0);
                }
            }
        }

        private List<SqlParameter> InnerList {
            get {
                List<SqlParameter> items = _items;

                if (null == items) {
                    items = new List<SqlParameter>();
                    _items = items;
                }
                return items;
            }
        }

        override public bool IsFixedSize {
            get {
                if (SysSqlParameterCollection != null)
                {
                    return SysSqlParameterCollection.IsFixedSize;
                }
                else
                {
                    return ((System.Collections.IList)InnerList).IsFixedSize;
                }
            }
        }

        override public bool IsReadOnly {
            get {
                if (SysSqlParameterCollection != null)
                {
                    return SysSqlParameterCollection.IsReadOnly;
                }
                else
                {
                    return ((System.Collections.IList)InnerList).IsReadOnly;
                }
            }
        }

        override public bool IsSynchronized {
            get {
                if (SysSqlParameterCollection != null)
                {
                    return SysSqlParameterCollection.IsSynchronized;
                }
                else
                {
                    return ((System.Collections.ICollection)InnerList).IsSynchronized;
                }
            }
        }

        override public object SyncRoot {
            get {
                if (SysSqlParameterCollection != null)
                {
                    return SysSqlParameterCollection.SyncRoot;
                }
                else
                {
                    return ((System.Collections.ICollection)InnerList).SyncRoot;
                }
            }
        }

        [
        EditorBrowsableAttribute(EditorBrowsableState.Never)
        ]
        override public int Add(object value) {
            if (SysSqlParameterCollection != null)
            {
                return SysSqlParameterCollection.Add(value);
            }
            else
            {
                OnChange();  // fire event before value is validated
                ValidateType(value);
                Validate(-1, value);
                InnerList.Add((SqlParameter)value);
                return Count - 1;
            }
        }

        override public void AddRange(System.Array values) {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.AddRange(values);
            }
            else
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
        }

        private int CheckName(string parameterName) {
            int index = IndexOf(parameterName);
            if (index < 0) {
                throw ADP.ParametersSourceIndex(parameterName, this, ItemType);
            }
            return index;
        }

        override public void Clear() {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.Clear();

            }
            else
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
        }

        override public bool Contains(object value) {
            if (SysSqlParameterCollection != null)
            {
                return SysSqlParameterCollection.Contains(value);
            }
            else
            {
                return (-1 != IndexOf(value));
            }
        }

        override public void CopyTo(Array array, int index) {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.CopyTo(array, index);
            }
            else
            {
                ((System.Collections.ICollection)InnerList).CopyTo(array, index);
            }
        }

        override public System.Collections.IEnumerator GetEnumerator() {
            return SysSqlParameterCollection?.GetEnumerator() ?? ((System.Collections.ICollection)InnerList).GetEnumerator();
        }

        override protected DbParameter GetParameter(int index) {
            RangeCheck(index);
            return InnerList[index];
        }

        override protected DbParameter GetParameter(string parameterName) {
            int index = IndexOf(parameterName);
            if (index < 0) {
                throw ADP.ParametersSourceIndex(parameterName, this, ItemType);
            }
            return InnerList[index];
        }

        private static int IndexOf(System.Collections.IEnumerable items, string parameterName) {
            if (null != items) {
                int i = 0;
                // first case, kana, width sensitive search
                foreach(SqlParameter parameter in items) {
                    if (0 == ADP.SrcCompare(parameterName, parameter.ParameterName)) {
                        return i;
                    }
                    ++i;
                }
                i = 0;
                // then insensitive search
                foreach(SqlParameter parameter in items) {
                    if (0 == ADP.DstCompare(parameterName, parameter.ParameterName)) {
                        return i;
                    }
                    ++i;
                }
            }
            return -1;
        }

        override public int IndexOf(string parameterName) {
            return SysSqlParameterCollection?.IndexOf(parameterName) ?? IndexOf(InnerList, parameterName);
        }

        override public int IndexOf(object value) {
            if (SysSqlParameterCollection != null)
            {
                return SysSqlParameterCollection.IndexOf(value);
            }
            else
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
        }

        override public void Insert(int index, object value) {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.Insert(index, value);
            }
            else
            {
                OnChange();  // fire event before value is validated
                ValidateType(value);
                Validate(-1, (SqlParameterCollection)value);
                InnerList.Insert(index, (SqlParameter)value);
            }
        }

        private void RangeCheck(int index) {
            if ((index < 0) || (Count <= index)) {
                throw ADP.ParametersMappingIndex(index, this);
            }
        }

        override public void Remove(object value) {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.Remove(value);
            }
            else
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
        }

        override public void RemoveAt(int index) {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.RemoveAt(index);
            }
            else
            {
                OnChange();  // fire event before value is validated
                RangeCheck(index);
                RemoveIndex(index);
            }
        }

        override public void RemoveAt(string parameterName) {
            if (SysSqlParameterCollection != null)
            {
                SysSqlParameterCollection.RemoveAt(parameterName);
            }
            else
            {
                OnChange();  // fire event before value is validated
                int index = CheckName(parameterName);
                RemoveIndex(index);
            }
        }

        private void RemoveIndex(int index) {
            List<SqlParameter> items = InnerList;
            Debug.Assert((null != items) && (0 <= index) && (index < Count), "RemoveIndex, invalid");
            SqlParameter item = items[index];
            items.RemoveAt(index);
            item.ResetParent();
        }

        private void Replace(int index, object newValue) {
            List<SqlParameter> items = InnerList;
            Debug.Assert((null != items) && (0 <= index) && (index < Count), "Replace Index invalid");
            ValidateType(newValue);
            Validate(index, newValue);
            SqlParameter item = items[index];
            items[index] = (SqlParameter)newValue;
            item.ResetParent();
        }

        override protected void SetParameter(int index, DbParameter value) {
            OnChange();  // fire event before value is validated
            RangeCheck(index);
            Replace(index, value);
        }

        override protected void SetParameter(string parameterName, DbParameter value) {
            OnChange();  // fire event before value is validated
            int index = IndexOf(parameterName);
            if (index < 0) {
                throw ADP.ParametersSourceIndex(parameterName, this, ItemType);
            }
            Replace(index, value);
        }

        private void Validate(int index, object value) {
            if (null == value) {
                throw ADP.ParameterNull("value", this, ItemType);
            }
            // Validate assigns the parent - remove clears the parent
            object parent = ((SqlParameter)value).CompareExchangeParent(this, null);
            if (null != parent) {
                if (this != parent) {
                    throw ADP.ParametersIsNotParent(ItemType, this);
                }
                if (index != IndexOf(value)) {
                    throw ADP.ParametersIsParent(ItemType, this);
                }
            }
            // generate a ParameterName
            String name = ((SqlParameter)value).ParameterName;
            if (0 == name.Length) {
                index = 1;
                do {
                    name = ADP.Parameter + index.ToString(CultureInfo.CurrentCulture);
                    index++;
                } while (-1 != IndexOf(name));
                ((SqlParameter)value).ParameterName = name;
            }
        }

        private void ValidateType(object value) {
            if (null == value) {
                throw ADP.ParameterNull("value", this, ItemType);
            }
            else if (!ItemType.IsInstanceOfType(value)) {
                throw ADP.InvalidParameterType(this, ItemType, value);
            }
        }

    };
}

