// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/SqlBulkCopyColumnOrderHintCollection/*'/>
    public sealed class SqlBulkCopyColumnOrderHintCollection : CollectionBase
    {
        internal bool ReadOnly { get; set; }

        private readonly Dictionary<string, SqlBulkCopyColumnOrderHint> _nameToOrderHint = new Dictionary<string, SqlBulkCopyColumnOrderHint>();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Item/*'/>
        public SqlBulkCopyColumnOrderHint this[int index] => (SqlBulkCopyColumnOrderHint)List[index];

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Add[@name="columnOrderHintParameter"]/*'/>
        public SqlBulkCopyColumnOrderHint Add(SqlBulkCopyColumnOrderHint columnOrderHint)
        {
            AssertWriteAccess();
            if (columnOrderHint == null)
            {
                throw new ArgumentNullException();
            }
            if (string.IsNullOrEmpty(columnOrderHint.Column) ||
                columnOrderHint.SortOrder == SortOrder.Unspecified)
            {
                throw SQL.BulkLoadInvalidOrderHint();
            }
            RegisterColumnName(columnOrderHint, columnOrderHint.Column);
            InnerList.Add(columnOrderHint);
            return columnOrderHint;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Add[@name="columnStringAndsortOrderSortOrder"]/*'/>
        public SqlBulkCopyColumnOrderHint Add(string column, SortOrder sortOrder)
        {
            AssertWriteAccess();
            return Add(new SqlBulkCopyColumnOrderHint(column, sortOrder));
        }

        private void AssertWriteAccess()
        {
            if (ReadOnly)
            {
                throw SQL.BulkLoadOrderHintInaccessible();
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Clear/*'/>
        public new void Clear()
        {
            AssertWriteAccess();
            foreach (SqlBulkCopyColumnOrderHint orderHint in InnerList)
            {
                UnregisterColumnName(orderHint, orderHint.Column);
            }
            base.Clear();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Contains/*'/>
        public bool Contains(SqlBulkCopyColumnOrderHint value) => InnerList.Contains(value);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/CopyTo/*'/>
        public void CopyTo(SqlBulkCopyColumnOrderHint[] array, int index) => InnerList.CopyTo(array, index);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/IndexOf/*'/>
        public int IndexOf(SqlBulkCopyColumnOrderHint value) => InnerList.IndexOf(value);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Insert/*'/>
        public void Insert(int index, SqlBulkCopyColumnOrderHint value)
        {
            AssertWriteAccess();
            // Try inserting into an invalid index to throw an exception
            if (index < 0 || index > InnerList.Count)
            {
                InnerList.Insert(index, value);
            }
            if (value == null)
            {
                throw new ArgumentNullException();
            }
            RegisterColumnName(value, value.Column);
            InnerList.Insert(index, value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Remove/*'/>
        public void Remove(SqlBulkCopyColumnOrderHint value)
        {
            AssertWriteAccess();
            if (value == null)
            {
                throw new ArgumentNullException();
            }
            UnregisterColumnName(value, value.Column);
            InnerList.Remove(value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/RemoveAt/*'/>
        public new void RemoveAt(int index)
        {
            AssertWriteAccess();
            var orderHint = (SqlBulkCopyColumnOrderHint)InnerList[index];
            UnregisterColumnName(orderHint, orderHint.Column);
            base.RemoveAt(index);
        }

        private void ColumnNameChanging(object sender, string newName)
        {
            if (sender is SqlBulkCopyColumnOrderHint orderHint)
            {
                if (_nameToOrderHint.ContainsKey(newName))
                {
                    throw SQL.BulkLoadOrderHintDuplicateColumn(newName);
                }
                UnregisterColumnName(orderHint, orderHint.Column);
                RegisterColumnName(orderHint, newName);
            }
        }

        private void RegisterColumnName(SqlBulkCopyColumnOrderHint orderHint, string columnName)
        {
            if (_nameToOrderHint.ContainsKey(columnName))
            {
                throw SQL.BulkLoadOrderHintDuplicateColumn(orderHint.Column);
            }
            _nameToOrderHint.Add(columnName, orderHint);
            orderHint.NameChanging += ColumnNameChanging;
        }

        private void UnregisterColumnName(SqlBulkCopyColumnOrderHint orderHint, string columnName)
        {
            if (Contains(orderHint))
            {
                _nameToOrderHint.Remove(columnName);
                orderHint.NameChanging -= ColumnNameChanging;
            }
        }
    }
}

