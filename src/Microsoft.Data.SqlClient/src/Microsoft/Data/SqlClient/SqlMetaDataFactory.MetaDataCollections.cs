// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient;

internal sealed partial class SqlMetaDataFactory
{
    /// <summary>
    /// Returns a list of all schema collections supported by the current data provider
    /// </summary>
    /// <remarks>
    /// Returns both common schema collections <see href="https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/common-schema-collections"/>
    /// and SQL Server schema collections <see href="https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-schema-collections"/>
    /// </remarks>
    private class MetaDataCollection : MetaDataCollectionBase
    {
        internal MetaDataCollection()
            : base(DbMetaDataCollectionNames.MetaDataCollections, 0)
        {
        }

        public override ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable accumulator = null)
        {
            if (!ADP.IsEmptyArray(context.RestrictionValues))
            {
                throw ADP.TooManyRestrictions(CollectionName);
            }

            DataTable table = accumulator ?? new(CollectionName)
            {
                Columns =
                {
                    new DataColumn(DbMetaDataColumnNames.CollectionName, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.NumberOfRestrictions, typeof(int)),
                    new DataColumn(DbMetaDataColumnNames.NumberOfIdentifierParts, typeof(int))
                }
            };

            foreach (MetaDataCollectionBase mdc in s_metaDataCollection)
            {
                if (mdc.SupportedByCurrentVersion(context) && mdc.CollectionName[0] != '_')
                {
                    DataRow row = table.NewRow();
                    table.Rows.Add([mdc.CollectionName, mdc.NumberOfRestrictions, mdc.NumberOfIdentifierParts]);
                }
            }

            return new ValueTask<DataTable>(table);
        }

        internal async ValueTask<DataTable> GetMetadata(string collectionName, MetaDataContext context)
        {
            Debug.Assert(context != null);
            if (string.IsNullOrEmpty(collectionName) || collectionName[0] == '_')
            {
                throw ADP.UndefinedCollection(collectionName);
            }

            MetaDataCollectionBase collection = FindMetaDataCollection(collectionName, context);
            if (collection == null)
            {
                throw ADP.UnsupportedVersion(collectionName);
                //throw ADP.UndefinedCollection(collectionName);
                //throw ADP.UnableToBuildCollection(collectionName);
            }

            DataTable table = await collection.GetMetadata(context);
            return table;
        }
    }
}
