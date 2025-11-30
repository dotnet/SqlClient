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

        // population mechanisms
        private const string DataTableKey = "DataTable";
        private const string SqlCommandKey = "SQLCommand";
        private const string PrepareCollectionKey = "PrepareCollection";

        public DbMetaDataFactory(Stream xmlStream, string serverVersion)
        {
            ADP.CheckArgumentNull(xmlStream, nameof(xmlStream));
            ADP.CheckArgumentNull(serverVersion, nameof(serverVersion));

            ServerVersion = serverVersion;

            CollectionDataSet = LoadDataSetFromXml(xmlStream);
        }

        protected DataSet CollectionDataSet { get; }

        protected string ServerVersion { get; }

        protected DataTable CloneAndFilterCollection(string collectionName, string[] hiddenColumnNames)
        {
            DataTable destinationTable;
            DataColumn[] filteredSourceColumns;
            DataColumnCollection destinationColumns;
            DataRow newRow;

            DataTable sourceTable = CollectionDataSet.Tables[collectionName];

            if ((sourceTable == null) || (collectionName != sourceTable.TableName))
            {
                throw ADP.DataTableDoesNotExist(collectionName);
            }

            destinationTable = new DataTable(collectionName)
            {
                Locale = CultureInfo.InvariantCulture
            };
            destinationColumns = destinationTable.Columns;

            filteredSourceColumns = FilterColumns(sourceTable, hiddenColumnNames, destinationColumns);

            foreach (DataRow row in sourceTable.Rows)
            {
                if (SupportedByCurrentVersion(row))
                {
                    newRow = destinationTable.NewRow();
                    for (int i = 0; i < destinationColumns.Count; i++)
                    {
                        newRow[destinationColumns[i]] = row[filteredSourceColumns[i], DataRowVersion.Current];
                    }
                    destinationTable.Rows.Add(newRow);
                    newRow.AcceptChanges();
                }
            }

            return destinationTable;
        }

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                CollectionDataSet.Dispose();
            }
        }

        private DataTable ExecuteCommand(DataRow requestedCollectionRow, string[] restrictions, DbConnection connection)
        {
            DataTable metaDataCollectionsTable = CollectionDataSet.Tables[DbMetaDataCollectionNames.MetaDataCollections];
            DataColumn populationStringColumn = metaDataCollectionsTable.Columns[PopulationStringKey];
            DataColumn numberOfRestrictionsColumn = metaDataCollectionsTable.Columns[NumberOfRestrictionsKey];
            DataColumn collectionNameColumn = metaDataCollectionsTable.Columns[CollectionNameKey];

            DataTable resultTable = null;

            Debug.Assert(requestedCollectionRow != null);
            string sqlCommand = requestedCollectionRow[populationStringColumn, DataRowVersion.Current] as string;
            int numberOfRestrictions = (int)requestedCollectionRow[numberOfRestrictionsColumn, DataRowVersion.Current];
            string collectionName = requestedCollectionRow[collectionNameColumn, DataRowVersion.Current] as string;

            if ((restrictions != null) && (restrictions.Length > numberOfRestrictions))
            {
                throw ADP.TooManyRestrictions(collectionName);
            }

            DbCommand command = connection.CreateCommand();
            SqlConnection castConnection = connection as SqlConnection;

            command.CommandText = sqlCommand;
            command.CommandTimeout = Math.Max(command.CommandTimeout, 180);
            command.Transaction = castConnection?.GetOpenTdsConnection()?.CurrentTransaction?.Parent;

            for (int i = 0; i < numberOfRestrictions; i++)
            {

                DbParameter restrictionParameter = command.CreateParameter();

                if ((restrictions != null) && (restrictions.Length > i) && (restrictions[i] != null))
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

            DbDataReader reader = null;
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

                DataTable schemaTable = reader.GetSchemaTable();
                foreach (DataRow row in schemaTable.Rows)
                {
                    resultTable.Columns.Add(row["ColumnName"] as string, (Type)row["DataType"] as Type);
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

        private DataColumn[] FilterColumns(DataTable sourceTable, string[] hiddenColumnNames, DataColumnCollection destinationColumns)
        {
            int columnCount = 0;
            foreach (DataColumn sourceColumn in sourceTable.Columns)
            {
                if (IncludeThisColumn(sourceColumn, hiddenColumnNames))
                {
                    columnCount++;
                }
            }

            if (columnCount == 0)
            {
                throw ADP.NoColumns();
            }

            int currentColumn = 0;
            DataColumn[] filteredSourceColumns = new DataColumn[columnCount];

            foreach (DataColumn sourceColumn in sourceTable.Columns)
            {
                if (IncludeThisColumn(sourceColumn, hiddenColumnNames))
                {
                    DataColumn newDestinationColumn = new(sourceColumn.ColumnName, sourceColumn.DataType);
                    destinationColumns.Add(newDestinationColumn);
                    filteredSourceColumns[currentColumn] = sourceColumn;
                    currentColumn++;
                }
            }
            return filteredSourceColumns;
        }

        internal DataRow FindMetaDataCollectionRow(string collectionName)
        {
            bool versionFailure;
            bool haveExactMatch;
            bool haveMultipleInexactMatches;
            string candidateCollectionName;

            DataTable metaDataCollectionsTable = CollectionDataSet.Tables[DbMetaDataCollectionNames.MetaDataCollections];
            if (metaDataCollectionsTable == null)
            {
                throw ADP.InvalidXml();
            }

            DataColumn collectionNameColumn = metaDataCollectionsTable.Columns[DbMetaDataColumnNames.CollectionName];

            if (collectionNameColumn == null || (typeof(string) != collectionNameColumn.DataType))
            {
                throw ADP.InvalidXmlMissingColumn(DbMetaDataCollectionNames.MetaDataCollections, DbMetaDataColumnNames.CollectionName);
            }

            DataRow requestedCollectionRow = null;
            string exactCollectionName = null;

            // find the requested collection
            versionFailure = false;
            haveExactMatch = false;
            haveMultipleInexactMatches = false;

            foreach (DataRow row in metaDataCollectionsTable.Rows)
            {

                candidateCollectionName = row[collectionNameColumn, DataRowVersion.Current] as string;
                if (string.IsNullOrEmpty(candidateCollectionName))
                {
                    throw ADP.InvalidXmlInvalidValue(DbMetaDataCollectionNames.MetaDataCollections, DbMetaDataColumnNames.CollectionName);
                }

                if (ADP.CompareInsensitiveInvariant(candidateCollectionName, collectionName))
                {
                    if (!SupportedByCurrentVersion(row))
                    {
                        versionFailure = true;
                    }
                    else
                    {
                        if (collectionName == candidateCollectionName)
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
                            if (exactCollectionName != null)
                            {
                                // can't fail here becasue we may still find an exact match
                                haveMultipleInexactMatches = true;
                            }
                            requestedCollectionRow = row;
                            exactCollectionName = candidateCollectionName;
                        }
                    }
                }
            }

            if (requestedCollectionRow == null)
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

        private void FixUpDataSourceInformationRow(DataRow dataSourceInfoRow)
        {
            Debug.Assert(dataSourceInfoRow.Table.Columns.Contains(DbMetaDataColumnNames.DataSourceProductVersion));
            Debug.Assert(dataSourceInfoRow.Table.Columns.Contains(DbMetaDataColumnNames.DataSourceProductVersionNormalized));

            dataSourceInfoRow[DbMetaDataColumnNames.DataSourceProductVersion] = ServerVersion;
            dataSourceInfoRow[DbMetaDataColumnNames.DataSourceProductVersionNormalized] = ServerVersion;
        }


        private string GetParameterName(string neededCollectionName, int neededRestrictionNumber)
        {
            DataColumn collectionName = null;
            DataColumn parameterName = null;
            DataColumn restrictionName = null;
            DataColumn restrictionNumber = null;

            string result = null;

            DataTable restrictionsTable = CollectionDataSet.Tables[DbMetaDataCollectionNames.Restrictions];
            if (restrictionsTable != null)
            {
                DataColumnCollection restrictionColumns = restrictionsTable.Columns;
                if (restrictionColumns != null)
                {
                    collectionName = restrictionColumns[CollectionNameKey];
                    parameterName = restrictionColumns[ParameterNameKey];
                    restrictionName = restrictionColumns[RestrictionNameKey];
                    restrictionNumber = restrictionColumns[RestrictionNumberKey];
                }
            }

            if ((parameterName == null) || (collectionName == null) || (restrictionName == null) || (restrictionNumber == null))
            {
                throw ADP.MissingRestrictionColumn();
            }

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

            if (result == null)
            {
                throw ADP.MissingRestrictionRow();
            }

            return result;
        }

        public virtual DataTable GetSchema(DbConnection connection, string collectionName, string[] restrictions)
        {
            Debug.Assert(CollectionDataSet != null);

            DataTable metaDataCollectionsTable = CollectionDataSet.Tables[DbMetaDataCollectionNames.MetaDataCollections];
            DataColumn populationMechanismColumn = metaDataCollectionsTable.Columns[PopulationMechanismKey];
            DataColumn collectionNameColumn = metaDataCollectionsTable.Columns[DbMetaDataColumnNames.CollectionName];

            string[] hiddenColumns;

            DataRow requestedCollectionRow = FindMetaDataCollectionRow(collectionName);
            string exactCollectionName = requestedCollectionRow[collectionNameColumn, DataRowVersion.Current] as string;

            if (!ADP.IsEmptyArray(restrictions))
            {

                for (int i = 0; i < restrictions.Length; i++)
                {
                    if ((restrictions[i] != null) && (restrictions[i].Length > 4096))
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
                    if (exactCollectionName == DbMetaDataCollectionNames.MetaDataCollections)
                    {
                        hiddenColumns = new string[2];
                        hiddenColumns[0] = PopulationMechanismKey;
                        hiddenColumns[1] = PopulationStringKey;
                    }
                    else
                    {
                        hiddenColumns = null;
                    }
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

        private bool IncludeThisColumn(DataColumn sourceColumn, string[] hiddenColumnNames)
        {

            bool result = true;
            string sourceColumnName = sourceColumn.ColumnName;

            switch (sourceColumnName)
            {

                case MinimumVersionKey:
                case MaximumVersionKey:
                    result = false;
                    break;

                default:
                    if (hiddenColumnNames == null)
                    {
                        break;
                    }
                    for (int i = 0; i < hiddenColumnNames.Length; i++)
                    {
                        if (hiddenColumnNames[i] == sourceColumnName)
                        {
                            result = false;
                            break;
                        }
                    }
                    break;
            }

            return result;
        }

        private DataSet LoadDataSetFromXml(Stream XmlStream)
        {
            DataSet metaDataCollectionsDataSet = new DataSet("NewDataSet")
            {
                Locale = CultureInfo.InvariantCulture
            };
            XmlReaderSettings settings = new()
            {
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true
            };
            using XmlReader reader = XmlReader.Create(XmlStream, settings);

            // Build up the schema DataSet manually, then load data from XmlStream. The schema of the DataSet is defined at:
            // * https://learn.microsoft.com/en-us/sql/connect/ado-net/common-schema-collections
            // * https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-schema-collections
            // Building the schema manually is necessary because DataSet.ReadXml uses XML serialization. This is slow, and it
            // increases the binary size of AOT assemblies by approximately 4MB.

            bool readContainer = reader.Read();

            // Skip past the XML declaration and the outer container element. This XML document is hardcoded;
            // these checks will need to be adjusted if its structure changes.
            Debug.Assert(readContainer);
            Debug.Assert(reader.NodeType == XmlNodeType.XmlDeclaration);

            readContainer = reader.Read();
            Debug.Assert(readContainer);
            Debug.Assert(reader.NodeType == XmlNodeType.Element);
            Debug.Assert(reader.Name == "MetaData");
            Debug.Assert(reader.Depth == 0);

            // Iterate over each "table element" of the outer container element.
            // LoadDataTable will read the child elements of each table element.
            while (reader.Read()
                && reader.Depth == 1)
            {
                DataTable dataTable = null;
                Action<DataRow> rowFixup = null;

                Debug.Assert(reader.NodeType == XmlNodeType.Element);

                switch (reader.Name)
                {
                    case "MetaDataCollectionsTable":
                        dataTable = CreateMetaDataCollectionsDataTable();
                        break;
                    case "RestrictionsTable":
                        dataTable = CreateRestrictionsDataTable();
                        break;
                    case "DataSourceInformationTable":
                        dataTable = CreateDataSourceInformationDataTable();
                        rowFixup = FixUpDataSourceInformationRow;
                        break;
                    case "DataTypesTable":
                        dataTable = CreateDataTypesDataTable();
                        break;
                    case "ReservedWordsTable":
                        dataTable = CreateReservedWordsDataTable();
                        break;
                    default:
                        Debug.Fail($"Unexpected table element name: {reader.Name}");
                        break;
                }

                if (dataTable != null)
                {
                    LoadDataTable(reader, dataTable, rowFixup);

                    metaDataCollectionsDataSet.Tables.Add(dataTable);
                }
            }

            return metaDataCollectionsDataSet;
        }

        private static void LoadDataTable(XmlReader reader, DataTable table, Action<DataRow> rowFixup)
        {
            int parentDepth = reader.Depth;

            table.BeginLoadData();

            // One outer loop per element, each loop reading every property of the row
            while (reader.Read()
                && reader.Depth == parentDepth + 1)
            {
                Debug.Assert(reader.NodeType == XmlNodeType.Element);
                Debug.Assert(reader.Name == table.TableName);

                int childDepth = reader.Depth;
                DataRow row = table.NewRow();

                // Read every child property. Hardcoded structure - start with the element name, advance to the text, then to the EndElement
                while (reader.Read()
                    && reader.Depth == childDepth + 1)
                {
                    DataColumn column;
                    bool successfulRead;

                    Debug.Assert(reader.NodeType == XmlNodeType.Element);

                    column = table.Columns[reader.Name];
                    Debug.Assert(column != null);

                    successfulRead = reader.Read();
                    Debug.Assert(successfulRead);
                    Debug.Assert(reader.NodeType == XmlNodeType.Text);

                    row[column] = reader.Value;

                    successfulRead = reader.Read();
                    Debug.Assert(successfulRead);
                    Debug.Assert(reader.NodeType == XmlNodeType.EndElement);
                }

                rowFixup?.Invoke(row);

                table.Rows.Add(row);

                Debug.Assert(reader.NodeType == XmlNodeType.EndElement);
            }

            table.EndLoadData();
            table.AcceptChanges();
        }

        private static DataTable CreateMetaDataCollectionsDataTable()
            => new DataTable(DbMetaDataCollectionNames.MetaDataCollections)
            {
                Columns =
                {
                    new DataColumn(DbMetaDataColumnNames.CollectionName, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.NumberOfRestrictions, typeof(int)),
                    new DataColumn(DbMetaDataColumnNames.NumberOfIdentifierParts, typeof(int)),
                    new DataColumn(PopulationMechanismKey, typeof(string)),
                    new DataColumn(PopulationStringKey, typeof(string)),
                    new DataColumn(MinimumVersionKey, typeof(string)),
                    new DataColumn(MaximumVersionKey, typeof(string))
                }
            };

        private static DataTable CreateRestrictionsDataTable()
            => new DataTable(DbMetaDataCollectionNames.Restrictions)
            {
                Columns =
                {
                    new DataColumn(DbMetaDataColumnNames.CollectionName, typeof(string)),
                    new DataColumn(RestrictionNameKey, typeof(string)),
                    new DataColumn(ParameterNameKey, typeof(string)),
                    new DataColumn(RestrictionDefaultKey, typeof(string)),
                    new DataColumn(RestrictionNumberKey, typeof(int)),
                    new DataColumn(MinimumVersionKey, typeof(string)),
                    new DataColumn(MaximumVersionKey, typeof(string))
                }
            };

        private static DataTable CreateDataSourceInformationDataTable()
            => new DataTable(DbMetaDataCollectionNames.DataSourceInformation)
            {
                Columns =
                {
                    new DataColumn(DbMetaDataColumnNames.CompositeIdentifierSeparatorPattern, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.DataSourceProductName, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.DataSourceProductVersion, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.DataSourceProductVersionNormalized, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.GroupByBehavior, typeof(GroupByBehavior)),
                    new DataColumn(DbMetaDataColumnNames.IdentifierPattern, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.IdentifierCase, typeof(IdentifierCase)),
                    new DataColumn(DbMetaDataColumnNames.OrderByColumnsInSelect, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.ParameterMarkerFormat, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.ParameterMarkerPattern, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.ParameterNameMaxLength, typeof(int)),
                    new DataColumn(DbMetaDataColumnNames.ParameterNamePattern, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.QuotedIdentifierPattern, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.QuotedIdentifierCase, typeof(IdentifierCase)),
                    new DataColumn(DbMetaDataColumnNames.StatementSeparatorPattern, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.StringLiteralPattern, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.SupportedJoinOperators, typeof(SupportedJoinOperators))
                }
            };

        private static DataTable CreateDataTypesDataTable()
            => new DataTable(DbMetaDataCollectionNames.DataTypes)
            {
                Columns =
                {
                    new DataColumn(DbMetaDataColumnNames.TypeName, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.ProviderDbType, typeof(int)),
                    new DataColumn(DbMetaDataColumnNames.ColumnSize, typeof(long)),
                    new DataColumn(DbMetaDataColumnNames.CreateFormat, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.CreateParameters, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.DataType, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.IsAutoIncrementable, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.IsBestMatch, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.IsCaseSensitive, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.IsFixedLength, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.IsFixedPrecisionScale, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.IsLong, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.IsNullable, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.IsSearchable, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.IsSearchableWithLike, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.IsUnsigned, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.MaximumScale, typeof(short)),
                    new DataColumn(DbMetaDataColumnNames.MinimumScale, typeof(short)),
                    new DataColumn(DbMetaDataColumnNames.IsConcurrencyType, typeof(bool)),
                    new DataColumn(MaximumVersionKey, typeof(string)),
                    new DataColumn(MinimumVersionKey, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.IsLiteralSupported, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.LiteralPrefix, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.LiteralSuffix, typeof(string))
                }
            };

        private static DataTable CreateReservedWordsDataTable()
            => new DataTable(DbMetaDataCollectionNames.ReservedWords)
            {
                Columns =
                {
                    new DataColumn(DbMetaDataColumnNames.ReservedWord, typeof(string)),
                    new DataColumn(MinimumVersionKey, typeof(string)),
                    new DataColumn(MaximumVersionKey, typeof(string))
                }
            };

        protected virtual DataTable PrepareCollection(string collectionName, string[] restrictions, DbConnection connection)
        {
            throw ADP.NotSupported();
        }

        private bool SupportedByCurrentVersion(DataRow requestedCollectionRow)
        {
            bool result = true;
            DataColumnCollection tableColumns = requestedCollectionRow.Table.Columns;
            DataColumn versionColumn;
            object version;

            // check the minimum version first
            versionColumn = tableColumns[MinimumVersionKey];
            if (versionColumn != null)
            {
                version = requestedCollectionRow[versionColumn];
                if (version != null)
                {
                    if (version != DBNull.Value)
                    {
                        if (0 > string.Compare(ServerVersion, (string)version, StringComparison.OrdinalIgnoreCase))
                        {
                            result = false;
                        }
                    }
                }
            }

            // if the minimum version was ok what about the maximum version
            if (result)
            {
                versionColumn = tableColumns[MaximumVersionKey];
                if (versionColumn != null)
                {
                    version = requestedCollectionRow[versionColumn];
                    if (version != null)
                    {
                        if (version != DBNull.Value)
                        {
                            if (0 < string.Compare(ServerVersion, (string)version, StringComparison.OrdinalIgnoreCase))
                            {
                                result = false;
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
