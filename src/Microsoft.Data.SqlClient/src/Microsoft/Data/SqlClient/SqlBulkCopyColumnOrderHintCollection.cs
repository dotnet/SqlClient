// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/SqlBulkCopyColumnOrderHintCollection/*'/>
    public sealed class SqlBulkCopyColumnOrderHintCollection : CollectionBase
    {
        internal bool ReadOnly { get; set; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Item/*'/>
        public SqlBulkCopyColumnOrderHint this[int index] => (SqlBulkCopyColumnOrderHint)List[index];

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Add[@name="bulkCopyColumnOrderHintParameter"]/*'/>
        public SqlBulkCopyColumnOrderHint Add(SqlBulkCopyColumnOrderHint bulkCopyColumnOrderHint)
        {
            AssertWriteAccess();
            if (string.IsNullOrEmpty(bulkCopyColumnOrderHint.Column) ||
                bulkCopyColumnOrderHint.SortOrder == SortOrder.Unspecified)
            {
                throw SQL.BulkLoadInvalidOrderHint();
            }
            InnerList.Add(bulkCopyColumnOrderHint);
            return bulkCopyColumnOrderHint;
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
            InnerList.Insert(index, value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Remove/*'/>
        public void Remove(SqlBulkCopyColumnOrderHint value)
        {
            AssertWriteAccess();
            InnerList.Remove(value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/RemoveAt/*'/>
        public new void RemoveAt(int index)
        {
            AssertWriteAccess();
            base.RemoveAt(index);
        }

    }
}

