// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Diagnostics;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/SqlBulkCopyColumnMappingCollection/*'/>
    public sealed class SqlBulkCopyColumnMappingCollection : CollectionBase
    {

        private enum MappingSchema
        {
            Undefined = 0,
            NamesNames = 1,
            NemesOrdinals = 2,
            OrdinalsNames = 3,
            OrdinalsOrdinals = 4,
        }

        private bool _readOnly;
        private MappingSchema _mappingSchema = MappingSchema.Undefined;

        internal SqlBulkCopyColumnMappingCollection()
        {
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Item/*'/>
        public SqlBulkCopyColumnMapping this[int index]
        {
            get
            {
                return (SqlBulkCopyColumnMapping)this.List[index];
            }
        }

        internal bool ReadOnly
        {
            get
            {
                return _readOnly;
            }
            set
            {
                _readOnly = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="bulkCopyColumnMappingParameter"]/*'/>
        public SqlBulkCopyColumnMapping Add(SqlBulkCopyColumnMapping bulkCopyColumnMapping)
        {
            AssertWriteAccess();
            Debug.Assert(ADP.IsEmpty(bulkCopyColumnMapping.SourceColumn) || bulkCopyColumnMapping._internalSourceColumnOrdinal == -1, "BulkLoadAmbiguousSourceColumn");
            if (((ADP.IsEmpty(bulkCopyColumnMapping.SourceColumn)) && (bulkCopyColumnMapping.SourceOrdinal == -1))
                || ((ADP.IsEmpty(bulkCopyColumnMapping.DestinationColumn)) && (bulkCopyColumnMapping.DestinationOrdinal == -1)))
            {
                throw SQL.BulkLoadNonMatchingColumnMapping();
            }
            InnerList.Add(bulkCopyColumnMapping);
            return bulkCopyColumnMapping;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnStringAnddestinationColumnString"]/*'/>
        public SqlBulkCopyColumnMapping Add(string sourceColumn, string destinationColumn)
        {
            AssertWriteAccess();
            SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping(sourceColumn, destinationColumn);
            return Add(column);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnIndexIntegerAnddestinationColumnString"]/*'/>
        public SqlBulkCopyColumnMapping Add(int sourceColumnIndex, string destinationColumn)
        {
            AssertWriteAccess();
            SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping(sourceColumnIndex, destinationColumn);
            return Add(column);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnStringAnddestinationColumnIndexInteger"]/*'/>
        public SqlBulkCopyColumnMapping Add(string sourceColumn, int destinationColumnIndex)
        {
            AssertWriteAccess();
            SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping(sourceColumn, destinationColumnIndex);
            return Add(column);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnIndexIntegerAnddestinationColumnIndexInteger"]/*'/>
        public SqlBulkCopyColumnMapping Add(int sourceColumnIndex, int destinationColumnIndex)
        {
            AssertWriteAccess();
            SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping(sourceColumnIndex, destinationColumnIndex);
            return Add(column);
        }

        private void AssertWriteAccess()
        {
            if (ReadOnly)
            {
                throw SQL.BulkLoadMappingInaccessible();
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Clear/*'/>
        new public void Clear()
        {
            AssertWriteAccess();
            base.Clear();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Contains/*'/>
        public bool Contains(SqlBulkCopyColumnMapping value)
        {
            return (-1 != InnerList.IndexOf(value));
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/CopyTo/*'/>
        public void CopyTo(SqlBulkCopyColumnMapping[] array, int index)
        {
            InnerList.CopyTo(array, index);
        }

        internal void CreateDefaultMapping(int columnCount)
        {
            for (int i = 0; i < columnCount; i++)
            {
                InnerList.Add(new SqlBulkCopyColumnMapping(i, i));
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/IndexOf/*'/>
        public int IndexOf(SqlBulkCopyColumnMapping value)
        {
            return InnerList.IndexOf(value);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Insert/*'/>
        public void Insert(int index, SqlBulkCopyColumnMapping value)
        {
            AssertWriteAccess();
            InnerList.Insert(index, value);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Remove/*'/>
        public void Remove(SqlBulkCopyColumnMapping value)
        {
            AssertWriteAccess();
            InnerList.Remove(value);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/RemoveAt/*'/>
        new public void RemoveAt(int index)
        {
            AssertWriteAccess();
            base.RemoveAt(index);
        }

        internal void ValidateCollection()
        {
            MappingSchema mappingSchema;
            foreach (SqlBulkCopyColumnMapping a in this)
            {
                if (a.SourceOrdinal != -1)
                {
                    if (a.DestinationOrdinal != -1)
                    {
                        mappingSchema = MappingSchema.OrdinalsOrdinals;
                    }
                    else
                    {
                        mappingSchema = MappingSchema.OrdinalsNames;
                    }
                }
                else
                {
                    if (a.DestinationOrdinal != -1)
                    {
                        mappingSchema = MappingSchema.NemesOrdinals;
                    }
                    else
                    {
                        mappingSchema = MappingSchema.NamesNames;
                    }
                }

                if (_mappingSchema == MappingSchema.Undefined)
                {
                    _mappingSchema = mappingSchema;
                }
                else
                {
                    if (_mappingSchema != mappingSchema)
                    {
                        throw SQL.BulkLoadMappingsNamesOrOrdinalsOnly();
                    }
                }
            }
        }
    }
}

