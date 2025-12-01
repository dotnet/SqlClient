// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Microsoft.Data.ProviderBase
{
    internal class DbMetaDataFactory
    {
        // well known column names
        private const string CollectionNameKey = "CollectionName";
        private const string PopulationMechanismKey = "PopulationMechanism";
        private const string PopulationStringKey = "PopulationString";
        private const string MaximumVersionKey = "MaximumVersion";
        private const string MinimumVersionKey = "MinimumVersion";
        private const string RestrictionDefaultKey = "RestrictionDefault";
        private const string RestrictionNumberKey = "RestrictionNumber";
        private const string NumberOfRestrictionsKey = "NumberOfRestrictions";
        private const string RestrictionNameKey = "RestrictionName";
        private const string ParameterNameKey = "ParameterName";

        protected DataSet CollectionDataSet { get; set; }

        protected string ServerVersion { get; set; }

        protected virtual DataTable CloneAndFilterCollection(string collectionName, ReadOnlySpan<string> hiddenColumnNames)
        {
            throw ADP.MethodNotImplemented();
        }

        protected virtual DataTable ExecuteCommand(DataRow requestedCollectionRow, string[] restrictions, DbConnection connection)
        {
            throw ADP.MethodNotImplemented();
        }

        private DataRow FindMetaDataCollectionRow(string collectionName)
        {
            bool versionFailure = false;
            bool haveExactMatch = false;
            bool haveMultipleInexactMatches = false;

            DataTable metaDataCollectionsTable = CollectionDataSet.Tables[DbMetaDataCollectionNames.MetaDataCollections];
            Debug.Assert(metaDataCollectionsTable is not null);

            DataColumn collectionNameColumn = metaDataCollectionsTable.Columns[DbMetaDataColumnNames.CollectionName];
            Debug.Assert(collectionNameColumn is not null && collectionNameColumn.DataType == typeof(string));

            DataRow requestedCollectionRow = null;
            string exactCollectionName = null;

            // find the requested collection
            foreach (DataRow row in metaDataCollectionsTable.Rows)
            {
                string candidateCollectionName = row[collectionNameColumn, DataRowVersion.Current] as string;

                if (string.IsNullOrEmpty(candidateCollectionName))
                {
                    throw ADP.InvalidXmlInvalidValue(DbMetaDataCollectionNames.MetaDataCollections, DbMetaDataColumnNames.CollectionName);
                }

                if (string.Equals(candidateCollectionName, collectionName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!SupportedByCurrentVersion(row))
                    {
                        versionFailure = true;
                    }
                    else if (collectionName == candidateCollectionName)
                    {
                        if (haveExactMatch)
                        {
                            throw ADP.CollectionNameIsNotUnique(collectionName);
                        }
                        requestedCollectionRow = row;
                        exactCollectionName = candidateCollectionName;
                        haveExactMatch = true;
                    }
                    else if (!haveExactMatch)
                    {
                        // have an inexact match - ok only if it is the only one
                        if (exactCollectionName is not null)
                        {
                            // can't fail here because we may still find an exact match
                            haveMultipleInexactMatches = true;
                        }
                        requestedCollectionRow = row;
                        exactCollectionName = candidateCollectionName;
                    }
                }
            }

            if (requestedCollectionRow is null)
            {
                if (!versionFailure)
                {
                    throw ADP.UndefinedCollection(collectionName);
                }
                else
                {
                    throw ADP.UnsupportedVersion(collectionName);
                }
            }

            if (!haveExactMatch && haveMultipleInexactMatches)
            {
                throw ADP.AmbiguousCollectionName(collectionName);
            }

            return requestedCollectionRow;

        }

        public virtual DataTable GetSchema(DbConnection connection, string collectionName, string[] restrictions)
        {
            const string DataTableKey = "DataTable";
            const string SqlCommandKey = "SQLCommand";
            const string PrepareCollectionKey = "PrepareCollection";

            Debug.Assert(CollectionDataSet is not null);

            DataTable metaDataCollectionsTable = CollectionDataSet.Tables[DbMetaDataCollectionNames.MetaDataCollections];
            DataColumn populationMechanismColumn = metaDataCollectionsTable.Columns[PopulationMechanismKey];
            DataColumn collectionNameColumn = metaDataCollectionsTable.Columns[DbMetaDataColumnNames.CollectionName];
            DataRow requestedCollectionRow = FindMetaDataCollectionRow(collectionName);
            string exactCollectionName = requestedCollectionRow[collectionNameColumn, DataRowVersion.Current] as string;

            if (!ADP.IsEmptyArray(restrictions))
            {

                for (int i = 0; i < restrictions.Length; i++)
                {
                    if ((restrictions[i] is not null) && (restrictions[i].Length > 4096))
                    {
                        // use a non-specific error because no new beta 2 error messages are allowed
                        // TODO: will add a more descriptive error in RTM
                        throw ADP.NotSupported();
                    }
                }
            }

            string populationMechanism = requestedCollectionRow[populationMechanismColumn, DataRowVersion.Current] as string;

            DataTable requestedSchema;
            switch (populationMechanism)
            {
                case DataTableKey:
                    ReadOnlySpan<string> hiddenColumns = exactCollectionName == DbMetaDataCollectionNames.MetaDataCollections
                        ? [ PopulationMechanismKey, PopulationStringKey ]
                        : [];

                    // none of the datatable collections support restrictions
                    if (!ADP.IsEmptyArray(restrictions))
                    {
                        throw ADP.TooManyRestrictions(exactCollectionName);
                    }

                    requestedSchema = CloneAndFilterCollection(exactCollectionName, hiddenColumns);
                    break;

                case SqlCommandKey:
                    requestedSchema = ExecuteCommand(requestedCollectionRow, restrictions, connection);
                    break;

                case PrepareCollectionKey:
                    requestedSchema = PrepareCollection(exactCollectionName, restrictions, connection);
                    break;

                default:
                    throw ADP.UndefinedPopulationMechanism(populationMechanism);
            }

            return requestedSchema;
        }

        protected virtual DataTable PrepareCollection(string collectionName, string[] restrictions, DbConnection connection)
        {
            throw ADP.NotSupported();
        }

        protected bool SupportedByCurrentVersion(DataRow requestedCollectionRow)
        {
            DataColumnCollection tableColumns = requestedCollectionRow.Table.Columns;
            DataColumn versionColumn;
            object version;

            // check the minimum version first
            versionColumn = tableColumns[MinimumVersionKey];
            if (versionColumn is not null)
            {
                version = requestedCollectionRow[versionColumn];

                if (version is string minVersion
                    && string.Compare(ServerVersion, minVersion, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            // if the minimum version was ok what about the maximum version
            versionColumn = tableColumns[MaximumVersionKey];
            if (versionColumn is not null)
            {
                version = requestedCollectionRow[versionColumn];

                if (version is string maxVersion
                    && string.Compare(ServerVersion, maxVersion, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
