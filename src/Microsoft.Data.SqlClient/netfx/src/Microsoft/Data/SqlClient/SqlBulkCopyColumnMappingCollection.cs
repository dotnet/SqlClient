// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using System.Collections;
using System.Diagnostics;

// todo: rename the file
// Caution! ndp\fx\src\data\netmodule\sources needs to follow this name change

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Defines the mapping between a column in a <see cref="Microsoft.Data.SqlClient.SqlBulkCopy" /> instance's data source and a column in the instance's destination table.
    /// </summary>
    /// <remarks>
    /// Column mappings define the mapping between data source and the target table. 
    /// If mappings are not defined - that is, the <see cref="Microsoft.Data.SqlClient.SqlBulkCopy.ColumnMappings"/> collection is empty - the columns are mapped implicitly based on ordinal position. For this to work, source and target schemas must match. If they do not, an <see cref="System.InvalidOperationException"/> will be thrown.
    /// If the <see cref="Microsoft.Data.SqlClient.SqlBulkCopy.ColumnMappings"/> collection is not empty, not every column present in the data source has to be specified. Those not mapped by the collection are ignored.  
    /// You can refer to source and target columns by either name or ordinal. You can also mix by-name and by-ordinal column references in the same mappings collection.
    /// </remarks>
    /// <example>
    /// The following example bulk copies data from a source table in the **AdventureWorks** sample database to a destination table in the same database. Although the number of columns in the destination matches the number of columns in the source, and each destination column is in the same ordinal position as its corresponding source column, the column names do not match. <see cref="Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping"/> objects are used to create a column map for the bulk copy.
    /// [!IMPORTANT]
    /// <![CDATA[
    /// This sample will not run unless you have created the work tables as described in [Bulk Copy Example Setup](~/docs/framework/data/adonet/sql/bulk-copy-example-setup.md). This code is provided to demonstrate the syntax for using **SqlBulkCopy** only. If the source and destination tables are in the same SQL Server instance, it is easier and faster to use a Transact-SQL `INSERT … SELECT` statement to copy the data.  
    /// [!code-csharp[DataWorks SqlBulkCopy.ColumnMapping#1](~/samples/snippets/csharp/VS_Snippets_ADO.NET/DataWorks SqlBulkCopy.ColumnMapping/CS/source.cs#1)]
    /// ]]>
    /// </example>
    public sealed class SqlBulkCopyColumnMappingCollection : CollectionBase  {

        private enum MappingSchema {
            Undefined = 0,
            NamesNames = 1,
            NemesOrdinals = 2,
            OrdinalsNames = 3,
            OrdinalsOrdinals = 4,
        }

        private bool _readOnly;
        private MappingSchema _mappingSchema = MappingSchema.Undefined;

        internal SqlBulkCopyColumnMappingCollection() {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SqlBulkCopyColumnMapping this [int index] {
            get {
                return (SqlBulkCopyColumnMapping)this.List[index];
            }
        }

        internal bool ReadOnly {
            get {
                return _readOnly;
            }
            set {
                _readOnly = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bulkCopyColumnMapping"></param>
        /// <returns></returns>
        public SqlBulkCopyColumnMapping Add(SqlBulkCopyColumnMapping bulkCopyColumnMapping) {
            AssertWriteAccess();
            Debug.Assert(ADP.IsEmpty(bulkCopyColumnMapping.SourceColumn) || bulkCopyColumnMapping._internalSourceColumnOrdinal == -1, "BulkLoadAmbigousSourceColumn");
            if (((ADP.IsEmpty(bulkCopyColumnMapping.SourceColumn)) && (bulkCopyColumnMapping.SourceOrdinal == -1))
                || ((ADP.IsEmpty(bulkCopyColumnMapping.DestinationColumn))&&(bulkCopyColumnMapping.DestinationOrdinal == -1))) {
                throw SQL.BulkLoadNonMatchingColumnMapping();
            }
            InnerList.Add(bulkCopyColumnMapping);
            return bulkCopyColumnMapping;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceColumn"></param>
        /// <param name="destinationColumn"></param>
        /// <returns></returns>
        public SqlBulkCopyColumnMapping Add(string sourceColumn, string destinationColumn) {
            AssertWriteAccess();
            SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping (sourceColumn, destinationColumn);
            return Add(column);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceColumnIndex"></param>
        /// <param name="destinationColumn"></param>
        /// <returns></returns>
        public SqlBulkCopyColumnMapping Add(int sourceColumnIndex, string destinationColumn) {
            AssertWriteAccess();
            SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping (sourceColumnIndex, destinationColumn);
            return Add(column);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceColumn"></param>
        /// <param name="destinationColumnIndex"></param>
        /// <returns></returns>
        public SqlBulkCopyColumnMapping Add(string sourceColumn, int destinationColumnIndex) {
            AssertWriteAccess();
            SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping (sourceColumn, destinationColumnIndex);
            return Add(column);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceColumnIndex"></param>
        /// <param name="destinationColumnIndex"></param>
        /// <returns></returns>
        public SqlBulkCopyColumnMapping Add(int sourceColumnIndex, int destinationColumnIndex) {
            AssertWriteAccess();
            SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping (sourceColumnIndex, destinationColumnIndex);
            return Add(column);
        }

        private void AssertWriteAccess () {
            if (ReadOnly) {
                throw SQL.BulkLoadMappingInaccessible();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        new public void Clear() {
            AssertWriteAccess();
            base.Clear();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Contains(SqlBulkCopyColumnMapping value) {
            return (-1 != InnerList.IndexOf(value));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        public void CopyTo(SqlBulkCopyColumnMapping[] array, int index) {
            InnerList.CopyTo(array, index);
        }

        internal void CreateDefaultMapping (int columnCount) {
            for (int i=0; i<columnCount; i++) {
                InnerList.Add(new SqlBulkCopyColumnMapping (i,i));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int IndexOf(SqlBulkCopyColumnMapping value) {
            return InnerList.IndexOf(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public void Insert(int index, SqlBulkCopyColumnMapping value) {
            AssertWriteAccess();
            InnerList.Insert(index, value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public void Remove(SqlBulkCopyColumnMapping value) {
            AssertWriteAccess();
            InnerList.Remove(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        new public void RemoveAt(int index) {
            AssertWriteAccess();
            base.RemoveAt(index);
        }

        internal void ValidateCollection () {
            MappingSchema mappingSchema;
            foreach (SqlBulkCopyColumnMapping a in this) {
                if (a.SourceOrdinal != -1) {
                    if(a.DestinationOrdinal != -1) {
                        mappingSchema = MappingSchema.OrdinalsOrdinals;
                    }
                    else {
                        mappingSchema = MappingSchema.OrdinalsNames;
                    }
                }
                else {
                    if(a.DestinationOrdinal != -1) {
                        mappingSchema = MappingSchema.NemesOrdinals;
                    }
                    else {
                        mappingSchema = MappingSchema.NamesNames;
                    }
                }

                if (_mappingSchema == MappingSchema.Undefined) {
                    _mappingSchema = mappingSchema;
                }
                else {
                    if (_mappingSchema != mappingSchema) {
                          throw SQL.BulkLoadMappingsNamesOrOrdinalsOnly();
                    }
                }
            }
        }
    }
}

