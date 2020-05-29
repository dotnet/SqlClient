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
        private readonly HashSet<string> _columnNames = new HashSet<string>();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Item/*'/>
        public SqlBulkCopyColumnOrderHint this[int index] => (SqlBulkCopyColumnOrderHint)List[index];

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Add[@name="columnOrderHintParameter"]/*'/>
        public SqlBulkCopyColumnOrderHint Add(SqlBulkCopyColumnOrderHint columnOrderHint)
        {
            if (columnOrderHint == null)
            {
                throw new ArgumentNullException(nameof(columnOrderHint));
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
            return Add(new SqlBulkCopyColumnOrderHint(column, sortOrder));
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Clear/*'/>
        public new void Clear()
        {
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
        public void Insert(int index, SqlBulkCopyColumnOrderHint columnOrderHint)
        {
            // Try inserting into an invalid index to throw an exception
            if (index < 0 || index > InnerList.Count)
            {
                InnerList.Insert(index, columnOrderHint);
            }
            if (columnOrderHint == null)
            {
                throw new ArgumentNullException(nameof(columnOrderHint));
            }
            RegisterColumnName(columnOrderHint, columnOrderHint.Column);
            InnerList.Insert(index, columnOrderHint);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Remove/*'/>
        public void Remove(SqlBulkCopyColumnOrderHint columnOrderHint)
        {
            if (columnOrderHint == null)
            {
                throw new ArgumentNullException(nameof(columnOrderHint));
            }
            UnregisterColumnName(columnOrderHint, columnOrderHint.Column);
            InnerList.Remove(columnOrderHint);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/RemoveAt/*'/>
        public new void RemoveAt(int index)
        {
            var orderHint = (SqlBulkCopyColumnOrderHint)InnerList[index];
            UnregisterColumnName(orderHint, orderHint.Column);
            base.RemoveAt(index);
        }

        private void ColumnNameChanging(object sender, string newName)
        {
            if (sender is SqlBulkCopyColumnOrderHint orderHint)
            {
                if (_columnNames.Contains(newName))
                {
                    throw SQL.BulkLoadOrderHintDuplicateColumn(newName);
                }
                UnregisterColumnName(orderHint, orderHint.Column);
                RegisterColumnName(orderHint, newName);
            }
        }

        private void RegisterColumnName(SqlBulkCopyColumnOrderHint orderHint, string columnName)
        {
            if (_columnNames.Contains(columnName))
            {
                throw SQL.BulkLoadOrderHintDuplicateColumn(orderHint.Column);
            }
            _columnNames.Add(columnName);
            orderHint.NameChanging += ColumnNameChanging;
        }

        private void UnregisterColumnName(SqlBulkCopyColumnOrderHint orderHint, string columnName)
        {
            if (Contains(orderHint))
            {
                _columnNames.Remove(columnName);
                orderHint.NameChanging -= ColumnNameChanging;
            }
        }
    }
}
