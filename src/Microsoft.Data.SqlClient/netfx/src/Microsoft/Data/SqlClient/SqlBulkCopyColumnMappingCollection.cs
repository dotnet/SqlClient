//------------------------------------------------------------------------------
// <copyright file="SqlBulkCopyMappingCollection.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">mithomas</owner>
// <owner current="true" primary="false">blained</owner>
//------------------------------------------------------------------------------

// todo: rename the file
// Caution! ndp\fx\src\data\netmodule\sources needs to follow this name change

namespace Microsoft.Data.SqlClient
{
    using Microsoft.Data.Common;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;

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
        private System.Data.SqlClient.SqlBulkCopyColumnMappingCollection _sysSqlBulkCopyColumnMappingCollection;

        internal System.Data.SqlClient.SqlBulkCopyColumnMappingCollection SysSqlBulkCopyColumnMappingCollection
        {
            get
            {
                return _sysSqlBulkCopyColumnMappingCollection;
            }
            set
            {
                _sysSqlBulkCopyColumnMappingCollection = value;
            }
        }

        internal SqlBulkCopyColumnMappingCollection() {
        }

        public SqlBulkCopyColumnMappingCollection(System.Data.SqlClient.SqlBulkCopyColumnMappingCollection sqlBulkCopyColumnMappingCollection)
        {
            SysSqlBulkCopyColumnMappingCollection = sqlBulkCopyColumnMappingCollection;
        }

        public SqlBulkCopyColumnMapping this [int index] {
            get {
                return (SysSqlBulkCopyColumnMappingCollection != null) ? new SqlBulkCopyColumnMapping(SysSqlBulkCopyColumnMappingCollection[index]) : (SqlBulkCopyColumnMapping)this.List[index];
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


        public SqlBulkCopyColumnMapping Add(SqlBulkCopyColumnMapping bulkCopyColumnMapping) {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                return new SqlBulkCopyColumnMapping(SysSqlBulkCopyColumnMappingCollection.Add(new System.Data.SqlClient.SqlBulkCopyColumnMapping(bulkCopyColumnMapping.SourceColumn, bulkCopyColumnMapping.DestinationColumn)));
            }
            else
            {
                AssertWriteAccess();
                Debug.Assert(ADP.IsEmpty(bulkCopyColumnMapping.SourceColumn) || bulkCopyColumnMapping._internalSourceColumnOrdinal == -1, "BulkLoadAmbigousSourceColumn");
                if (((ADP.IsEmpty(bulkCopyColumnMapping.SourceColumn)) && (bulkCopyColumnMapping.SourceOrdinal == -1))
                    || ((ADP.IsEmpty(bulkCopyColumnMapping.DestinationColumn)) && (bulkCopyColumnMapping.DestinationOrdinal == -1)))
                {
                    throw SQL.BulkLoadNonMatchingColumnMapping();
                }
                InnerList.Add(bulkCopyColumnMapping);
                return bulkCopyColumnMapping;
            }
        }

        public SqlBulkCopyColumnMapping Add(string sourceColumn, string destinationColumn) {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                return new SqlBulkCopyColumnMapping(SysSqlBulkCopyColumnMappingCollection.Add(sourceColumn, destinationColumn));
            }
            else
            {
                AssertWriteAccess();
                SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping(sourceColumn, destinationColumn);
                return Add(column);
            }
        }

        public SqlBulkCopyColumnMapping Add(int sourceColumnIndex, string destinationColumn) {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                return new SqlBulkCopyColumnMapping(SysSqlBulkCopyColumnMappingCollection.Add(sourceColumnIndex, destinationColumn));
            }
            else
            {
                AssertWriteAccess();
                SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping(sourceColumnIndex, destinationColumn);
                return Add(column);
            }
        }

        public SqlBulkCopyColumnMapping Add(string sourceColumn, int destinationColumnIndex) {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                return new SqlBulkCopyColumnMapping(SysSqlBulkCopyColumnMappingCollection.Add(sourceColumn, destinationColumnIndex));
            }
            else
            {
                AssertWriteAccess();
                SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping(sourceColumn, destinationColumnIndex);
                return Add(column);
            }
        }
        public SqlBulkCopyColumnMapping Add(int sourceColumnIndex, int destinationColumnIndex) {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                return new SqlBulkCopyColumnMapping(SysSqlBulkCopyColumnMappingCollection.Add(sourceColumnIndex, destinationColumnIndex));
            }
            else
            {
                AssertWriteAccess();
                SqlBulkCopyColumnMapping column = new SqlBulkCopyColumnMapping(sourceColumnIndex, destinationColumnIndex);
                return Add(column);
            }
        }

        private void AssertWriteAccess () {
            if (ReadOnly) {
                throw SQL.BulkLoadMappingInaccessible();
            }
        }

        new public void Clear() {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                SysSqlBulkCopyColumnMappingCollection.Clear();
            }
            else
            {
                AssertWriteAccess();
                base.Clear();
            }
        }

        public bool Contains(SqlBulkCopyColumnMapping value) {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                return SysSqlBulkCopyColumnMappingCollection.Contains(new System.Data.SqlClient.SqlBulkCopyColumnMapping(value.SourceColumn, value.DestinationColumn));
            }
            else
            {
                return (-1 != InnerList.IndexOf(value));
            }
        }

        public void CopyTo(SqlBulkCopyColumnMapping[] array, int index) {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                // Create an array of S.D.SqlClient.SqlBulkCopyMapping
                System.Data.SqlClient.SqlBulkCopyColumnMapping[] sysArray = new System.Data.SqlClient.SqlBulkCopyColumnMapping[SysSqlBulkCopyColumnMappingCollection.Count];
                SysSqlBulkCopyColumnMappingCollection.CopyTo(sysArray, index);

                //Convert to an array of M.D.SqlClient.SqlBulkCopyColumnMapping
                List<SqlBulkCopyColumnMapping> sysList = new List<SqlBulkCopyColumnMapping>();
                foreach (System.Data.SqlClient.SqlBulkCopyColumnMapping item in sysArray)
                {
                    sysList.Add(new SqlBulkCopyColumnMapping(item.SourceColumn, item.DestinationColumn));
                }
                sysList.ToArray().CopyTo(array, index);
            }
            else
            {
                InnerList.CopyTo(array, index);
            }
        }

        internal void CreateDefaultMapping (int columnCount) {
            for (int i=0; i<columnCount; i++) {
                InnerList.Add(new SqlBulkCopyColumnMapping (i,i));
            }
        }

        public int IndexOf(SqlBulkCopyColumnMapping value)
        {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                return SysSqlBulkCopyColumnMappingCollection.IndexOf(new System.Data.SqlClient.SqlBulkCopyColumnMapping(value.SourceColumn, value.DestinationColumn));
            }
            else
            {
                return InnerList.IndexOf(value);
            }
        }

        public void Insert(int index, SqlBulkCopyColumnMapping value)
        {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                SysSqlBulkCopyColumnMappingCollection.Insert(index, new System.Data.SqlClient.SqlBulkCopyColumnMapping(value.SourceColumn, value.DestinationColumn));
            }
            else
            {
                AssertWriteAccess();
                InnerList.Insert(index, value);
            }
        }

        public void Remove(SqlBulkCopyColumnMapping value)
        {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                SysSqlBulkCopyColumnMappingCollection.Remove(new System.Data.SqlClient.SqlBulkCopyColumnMapping(value.SourceColumn, value.DestinationColumn));
            }
            else
            {
                AssertWriteAccess();
                InnerList.Remove(value);
            }
        }

        new public void RemoveAt(int index)
        {
            if (SysSqlBulkCopyColumnMappingCollection != null)
            {
                SysSqlBulkCopyColumnMappingCollection.RemoveAt(index);
            }
            else
            {
                AssertWriteAccess();
                base.RemoveAt(index);
            }
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

