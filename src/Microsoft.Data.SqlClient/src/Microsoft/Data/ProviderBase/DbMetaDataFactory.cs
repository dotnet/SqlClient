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

        private DataTable ExecuteCommand(DataRow requestedCollectionRow, string[] restrictions, DbConnection connection)
        {
            Debug.Assert(requestedCollectionRow is not null);

            DataTable metaDataCollectionsTable = CollectionDataSet.Tables[DbMetaDataCollectionNames.MetaDataCollections];
            DataColumn populationStringColumn = metaDataCollectionsTable.Columns[PopulationStringKey];
            DataColumn numberOfRestrictionsColumn = metaDataCollectionsTable.Columns[NumberOfRestrictionsKey];
            DataColumn collectionNameColumn = metaDataCollectionsTable.Columns[CollectionNameKey];

            DataTable resultTable = null;
            string sqlCommand = requestedCollectionRow[populationStringColumn, DataRowVersion.Current] as string;
            int numberOfRestrictions = (int)requestedCollectionRow[numberOfRestrictionsColumn, DataRowVersion.Current];
            string collectionName = requestedCollectionRow[collectionNameColumn, DataRowVersion.Current] as string;

            if ((restrictions is not null) && (restrictions.Length > numberOfRestrictions))
            {
                throw ADP.TooManyRestrictions(collectionName);
            }

            SqlConnection castConnection = connection as SqlConnection;
            using SqlCommand command = castConnection.CreateCommand();

            command.CommandText = sqlCommand;
            command.CommandTimeout = Math.Max(command.CommandTimeout, 180);
            command.Transaction = castConnection?.GetOpenTdsConnection()?.CurrentTransaction?.Parent;

            for (int i = 0; i < numberOfRestrictions; i++)
            {
                SqlParameter restrictionParameter = command.CreateParameter();

                if ((restrictions is not null) && (i < restrictions.Length) && (restrictions[i] is not null))
                {
                    restrictionParameter.Value = restrictions[i];
                }
                else
                {
                    // This is where we have to assign null to the value of the parameter.
                    restrictionParameter.Value = DBNull.Value;
                }

                restrictionParameter.ParameterName = GetParameterName(collectionName, i + 1);
                restrictionParameter.Direction = ParameterDirection.Input;
                command.Parameters.Add(restrictionParameter);
            }

            SqlDataReader reader = null;
            try
            {
                try
                {
                    reader = command.ExecuteReader();
                }
                catch (Exception e)
                {
                    if (!ADP.IsCatchableExceptionType(e))
                    {
                        throw;
                    }
                    throw ADP.QueryFailed(collectionName, e);
                }

                // Build a DataTable from the reader
                resultTable = new DataTable(collectionName)
                {
                    Locale = CultureInfo.InvariantCulture
                };

                System.Collections.ObjectModel.ReadOnlyCollection<DbColumn> colSchema = reader.GetColumnSchema();
                foreach (DbColumn col in colSchema)
                {
                    resultTable.Columns.Add(col.ColumnName, col.DataType);
                }
                object[] values = new object[resultTable.Columns.Count];
                while (reader.Read())
                {
                    reader.GetValues(values);
                    resultTable.Rows.Add(values);
                }
            }
            finally
            {
                reader?.Dispose();
            }
            return resultTable;
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

        private string GetParameterName(string neededCollectionName, int neededRestrictionNumber)
        {
            DataTable restrictionsTable = CollectionDataSet.Tables[DbMetaDataCollectionNames.Restrictions];

            Debug.Assert(restrictionsTable is not null);

            DataColumnCollection restrictionColumns = restrictionsTable.Columns;
            DataColumn collectionName = restrictionColumns[CollectionNameKey];
            DataColumn parameterName = restrictionColumns[ParameterNameKey];
            DataColumn restrictionName = restrictionColumns[RestrictionNameKey];
            DataColumn restrictionNumber = restrictionColumns[RestrictionNumberKey];

            Debug.Assert(parameterName is not null);
            Debug.Assert(collectionName is not null);
            Debug.Assert(restrictionName is not null);
            Debug.Assert(restrictionNumber is not null);

            string result = null;

            foreach (DataRow restriction in restrictionsTable.Rows)
            {

                if (((string)restriction[collectionName] == neededCollectionName) &&
                    ((int)restriction[restrictionNumber] == neededRestrictionNumber) &&
                    (SupportedByCurrentVersion(restriction)))
                {

                    result = (string)restriction[parameterName];
                    break;
                }
            }

            if (result is null)
            {
                throw ADP.MissingRestrictionRow();
            }

            return result;
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
