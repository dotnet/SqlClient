// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHint.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHint"]/SqlBulkCopyColumnOrderHint/*'/>
    public sealed class SqlBulkCopyColumnOrderHint
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHint.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHint"]/ctor[@name="columnStringAndsortOrderSortOrder"]/*'/>
        public SqlBulkCopyColumnOrderHint(string column, SortOrder sortOrder)
        {
            Column = column;
            SortOrder = sortOrder;
        }

        private string _columnName;
        private SortOrder _sortOrder;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHint.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHint"]/Column/*'/>
        public string Column
        {
            get
            {
                if (_columnName != null)
                {
                    return _columnName;
                }
                return string.Empty;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _columnName = value;
                }
                else
                {
                    throw SQL.BulkLoadNullEmptyColumnName(nameof(Column));
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHint.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHint"]/SortOrder/*'/>
        public SortOrder SortOrder
        {
            get => _sortOrder;
            set
            {
                if (value != SortOrder.Unspecified)
                {
                    _sortOrder = value;
                }
                else
                {
                    throw SQL.BulkLoadUnspecifiedSortOrder();
                }
            }
        }

    }
}
