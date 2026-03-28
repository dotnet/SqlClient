// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlMetaDataFactory : IDisposable
    {
        // Well-known column names
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

        // Well-known server versions
        private const string ServerVersionNormalized90 = "09.00.0000";
        private const string ServerVersionNormalized10 = "10.00.0000";

        private static readonly HashSet<int> s_assemblyPropertyUnsupportedEngines = new() { 6, 9, 11 };

        private readonly DataSet _collectionDataSet;
        private readonly string _serverVersion;

        public SqlMetaDataFactory(Stream xmlStream, ConnectionCapabilities connectionCapabilities)
        {
            ADP.CheckArgumentNull(xmlStream, nameof(xmlStream));
            ADP.CheckArgumentNull(connectionCapabilities, nameof(connectionCapabilities));
            ADP.CheckArgumentNull(connectionCapabilities.ServerVersion, nameof(connectionCapabilities.ServerVersion));

            _serverVersion = connectionCapabilities.ServerVersion;

            _collectionDataSet = LoadDataSetFromXml(xmlStream);
        }

        private bool SupportedByCurrentVersion(DataRow requestedCollectionRow)
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
                    && string.Compare(_serverVersion, minVersion, StringComparison.OrdinalIgnoreCase) < 0)
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
                    && string.Compare(_serverVersion, maxVersion, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private DataRow FindMetaDataCollectionRow(string collectionName)
        {
            bool versionFailure = false;
            bool haveExactMatch = false;
            bool haveMultipleInexactMatches = false;

            DataTable metaDataCollectionsTable = _collectionDataSet.Tables[DbMetaDataCollectionNames.MetaDataCollections];
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

        public DataTable GetSchema(DbConnection connection, string collectionName, string[] restrictions)
        {
            const string DataTableKey = "DataTable";
            const string SqlCommandKey = "SQLCommand";
            const string PrepareCollectionKey = "PrepareCollection";

            Debug.Assert(_collectionDataSet is not null);

            DataTable metaDataCollectionsTable = _collectionDataSet.Tables[DbMetaDataCollectionNames.MetaDataCollections];
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
                        ? [PopulationMechanismKey, PopulationStringKey]
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

        public void Dispose() => Dispose(true);

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _collectionDataSet.Dispose();
            }
        }

        #region GetSchema Helpers: DataTable Population Method
        private static bool IncludeThisColumn(DataColumn sourceColumn, ReadOnlySpan<string> hiddenColumnNames)
        {
            string sourceColumnName = sourceColumn.ColumnName;

            return sourceColumnName switch
            {
                MinimumVersionKey or MaximumVersionKey => false,
#if NET
                _ => !hiddenColumnNames.Contains(sourceColumnName),
#else
                _ => hiddenColumnNames.IndexOf(sourceColumnName) == -1,
#endif
            };
        }

        private static DataColumn[] FilterColumns(DataTable sourceTable, ReadOnlySpan<string> hiddenColumnNames, DataColumnCollection destinationColumns)
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

        private DataTable CloneAndFilterCollection(string collectionName, ReadOnlySpan<string> hiddenColumnNames)
        {
            DataTable destinationTable;
            DataColumn[] filteredSourceColumns;
            DataColumnCollection destinationColumns;
            DataRow newRow;

            DataTable sourceTable = _collectionDataSet.Tables[collectionName];

            if (sourceTable?.TableName != collectionName)
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
        #endregion

        #region GetSchema Helpers: ExecuteCommand Population Method
        private string GetParameterName(string neededCollectionName, int neededRestrictionNumber)
        {
            DataTable restrictionsTable = _collectionDataSet.Tables[DbMetaDataCollectionNames.Restrictions];

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

        private DataTable ExecuteCommand(DataRow requestedCollectionRow, string[] restrictions, DbConnection connection)
        {
            Debug.Assert(requestedCollectionRow is not null);

            DataTable metaDataCollectionsTable = _collectionDataSet.Tables[DbMetaDataCollectionNames.MetaDataCollections];
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
        #endregion

        #region GetSchema Helpers: PrepareCollection Population Method
        private static void AddUDTsToDataTypesTable(DataTable dataTypesTable, SqlConnection connection, string serverVersion)
        {
            const string GetEngineEditionSqlCommand = "SELECT SERVERPROPERTY('EngineEdition');";
            const string ListUdtSqlCommand =
                "select " +
                    "assemblies.name, " +
                    "types.assembly_class, " +
                    "ASSEMBLYPROPERTY(assemblies.name, 'VersionMajor') as version_major, " +
                    "ASSEMBLYPROPERTY(assemblies.name, 'VersionMinor') as version_minor, " +
                    "ASSEMBLYPROPERTY(assemblies.name, 'VersionBuild') as version_build, " +
                    "ASSEMBLYPROPERTY(assemblies.name, 'VersionRevision') as version_revision, " +
                    "ASSEMBLYPROPERTY(assemblies.name, 'CultureInfo') as culture_info, " +
                    "ASSEMBLYPROPERTY(assemblies.name, 'PublicKey') as public_key, " +
                    "is_nullable, " +
                    "is_fixed_length, " +
                    "max_length " +
                "from sys.assemblies as assemblies  join sys.assembly_types as types " +
                "on assemblies.assembly_id = types.assembly_id ";
            const int AssemblyNameSqlIndex = 0;
            const int AssemblyClassSqlIndex = 1;
            const int VersionMajorSqlIndex = 2;
            const int VersionMinorSqlIndex = 3;
            const int VersionBuildSqlIndex = 4;
            const int VersionRevisionSqlIndex = 5;
            const int CultureInfoSqlIndex = 6;
            const int PublicKeySqlIndex = 7;
            const int IsNullableSqlIndex = 8;
            const int IsFixedLengthSqlIndex = 9;
            const int ColumnSizeSqlIndex = 10;

            // pre 9.0/2005 servers do not have UDTs
            if (0 > string.Compare(serverVersion, ServerVersionNormalized90, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using SqlCommand command = connection.CreateCommand();

            command.CommandText = GetEngineEditionSqlCommand;
            int engineEdition = (int)command.ExecuteScalar();

            if (s_assemblyPropertyUnsupportedEngines.Contains(engineEdition))
            {
                // Azure SQL Edge (9) throws an exception when querying sys.assemblies
                // Azure Synapse Analytics (6) and Azure Synapse serverless SQL pool (11)
                // do not support ASSEMBLYPROPERTY
                return;
            }

            DataColumn providerDbType = dataTypesTable.Columns[DbMetaDataColumnNames.ProviderDbType];
            DataColumn columnSize = dataTypesTable.Columns[DbMetaDataColumnNames.ColumnSize];
            DataColumn isFixedLength = dataTypesTable.Columns[DbMetaDataColumnNames.IsFixedLength];
            DataColumn isSearchable = dataTypesTable.Columns[DbMetaDataColumnNames.IsSearchable];
            DataColumn isLiteralSupported = dataTypesTable.Columns[DbMetaDataColumnNames.IsLiteralSupported];
            DataColumn typeName = dataTypesTable.Columns[DbMetaDataColumnNames.TypeName];
            DataColumn isNullable = dataTypesTable.Columns[DbMetaDataColumnNames.IsNullable];

            Debug.Assert(providerDbType is not null, "providerDbType column not found");
            Debug.Assert(columnSize is not null, "columnSize column not found");
            Debug.Assert(isFixedLength is not null, "isFixedLength column not found");
            Debug.Assert(isSearchable is not null, "isSearchable column not found");
            Debug.Assert(isLiteralSupported is not null, "isLiteralSupported column not found");
            Debug.Assert(typeName is not null, "typeName column not found");
            Debug.Assert(isNullable is not null, "isNullable column not found");

            // Execute the SELECT statement
            command.CommandText = ListUdtSqlCommand;

            using SqlDataReader reader = command.ExecuteReader();
            object[] values = new object[11];

            while (reader.Read())
            {
                reader.GetValues(values);
                DataRow newRow = dataTypesTable.NewRow();

                newRow[providerDbType] = SqlDbType.Udt;
                newRow[columnSize] = values[ColumnSizeSqlIndex];

                if (values[IsFixedLengthSqlIndex] != DBNull.Value)
                {
                    newRow[isFixedLength] = values[IsFixedLengthSqlIndex];
                }

                newRow[isSearchable] = true;
                newRow[isLiteralSupported] = false;
                if (values[IsNullableSqlIndex] != DBNull.Value)
                {
                    newRow[isNullable] = values[IsNullableSqlIndex];
                }

                if ((values[AssemblyClassSqlIndex] != DBNull.Value) &&
                    (values[VersionMajorSqlIndex] != DBNull.Value) &&
                    (values[VersionMinorSqlIndex] != DBNull.Value) &&
                    (values[VersionBuildSqlIndex] != DBNull.Value) &&
                    (values[VersionRevisionSqlIndex] != DBNull.Value))
                {

                    StringBuilder nameString = new();
                    nameString.Append($"{values[AssemblyClassSqlIndex]}, {values[AssemblyNameSqlIndex]}"
                        + $", Version={values[VersionMajorSqlIndex]}.{values[VersionMinorSqlIndex]}.{values[VersionBuildSqlIndex]}.{values[VersionRevisionSqlIndex]}");

                    if (values[CultureInfoSqlIndex] != DBNull.Value)
                    {
                        nameString.Append($", Culture={values[CultureInfoSqlIndex]}");
                    }

                    if (values[PublicKeySqlIndex] != DBNull.Value)
                    {

                        nameString.Append(", PublicKeyToken=");

                        byte[] byteArrayValue = (byte[])values[PublicKeySqlIndex];
#if NET9_0_OR_GREATER
                        nameString.Append(Convert.ToHexStringLower(byteArrayValue));
#elif NET8_0
                        nameString.Append(Convert.ToHexString(byteArrayValue).ToLowerInvariant());
#else
                        foreach (byte b in byteArrayValue)
                        {
                            nameString.Append(string.Format("{0,-2:x2}", b));
                        }
#endif
                    }

                    newRow[typeName] = nameString.ToString();
                    dataTypesTable.Rows.Add(newRow);
                    newRow.AcceptChanges();
                } // if assembly name
            }//end while
        }

        private static void AddTVPsToDataTypesTable(DataTable dataTypesTable, SqlConnection connection, string serverVersion)
        {
            const string ListTvpsSqlCommand =
                "select " +
                    "name, " +
                    "is_nullable, " +
                    "max_length " +
                "from sys.types " +
                "where is_table_type = 1";
            const int TypeNameSqlIndex = 0;
            const int IsNullableSqlIndex = 1;
            const int ColumnSizeSqlIndex = 2;

            // TODO: update this check once the server upgrades major version number!!!
            // pre 9.0/2005 servers do not have Table types
            if (0 > string.Compare(serverVersion, ServerVersionNormalized10, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Execute the SELECT statement
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = ListTvpsSqlCommand;

            DataColumn providerDbType = dataTypesTable.Columns[DbMetaDataColumnNames.ProviderDbType];
            DataColumn columnSize = dataTypesTable.Columns[DbMetaDataColumnNames.ColumnSize];
            DataColumn isSearchable = dataTypesTable.Columns[DbMetaDataColumnNames.IsSearchable];
            DataColumn isLiteralSupported = dataTypesTable.Columns[DbMetaDataColumnNames.IsLiteralSupported];
            DataColumn typeName = dataTypesTable.Columns[DbMetaDataColumnNames.TypeName];
            DataColumn isNullable = dataTypesTable.Columns[DbMetaDataColumnNames.IsNullable];

            Debug.Assert(providerDbType is not null, "providerDbType column not found");
            Debug.Assert(columnSize is not null, "columnSize column not found");
            Debug.Assert(isSearchable is not null, "isSearchable column not found");
            Debug.Assert(isLiteralSupported is not null, "isLiteralSupported column not found");
            Debug.Assert(typeName is not null, "typeName column not found");
            Debug.Assert(isNullable is not null, "isNullable column not found");

            using SqlDataReader reader = command.ExecuteReader();

            object[] values = new object[11];
            while (reader.Read())
            {

                reader.GetValues(values);
                DataRow newRow = dataTypesTable.NewRow();

                newRow[providerDbType] = SqlDbType.Structured;
                newRow[columnSize] = values[ColumnSizeSqlIndex];
                newRow[isSearchable] = false;
                newRow[isLiteralSupported] = false;

                if (values[IsNullableSqlIndex] != DBNull.Value)
                {
                    newRow[isNullable] = values[IsNullableSqlIndex];
                }

                newRow[typeName] = values[TypeNameSqlIndex];

                dataTypesTable.Rows.Add(newRow);
                newRow.AcceptChanges();
            }//end while
        }

        private DataTable GetDataTypesTable(SqlConnection connection)
        {
            // verify the existence of the table in the data set
            Debug.Assert(_collectionDataSet.Tables.Contains(DbMetaDataCollectionNames.DataTypes), "DataTypes collection not found in the DataSet");

            // copy the table filtering out any rows that don't apply to tho current version of the provider
            DataTable dataTypesTable = CloneAndFilterCollection(DbMetaDataCollectionNames.DataTypes, []);

            AddUDTsToDataTypesTable(dataTypesTable, connection, _serverVersion);
            AddTVPsToDataTypesTable(dataTypesTable, connection, _serverVersion);

            dataTypesTable.AcceptChanges();
            return dataTypesTable;
        }

        private DataTable PrepareCollection(string collectionName, string[] restrictions, DbConnection connection)
        {
            SqlConnection sqlConnection = (SqlConnection)connection;
            DataTable resultTable = null;

            if (collectionName == DbMetaDataCollectionNames.DataTypes)
            {
                if (ADP.IsEmptyArray(restrictions) == false)
                {
                    throw ADP.TooManyRestrictions(DbMetaDataCollectionNames.DataTypes);
                }
                resultTable = GetDataTypesTable(sqlConnection);
            }

            if (resultTable == null)
            {
                throw ADP.UnableToBuildCollection(collectionName);
            }

            return resultTable;
        }
        #endregion

        #region Create MetaDataCollections DataSet from XML
        private DataSet LoadDataSetFromXml(Stream XmlStream)
        {
            DataSet metaDataCollectionsDataSet = new("NewDataSet")
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

                if (dataTable is not null)
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
                    Debug.Assert(column is not null);

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

        private void FixUpDataSourceInformationRow(DataRow dataSourceInfoRow)
        {
            Debug.Assert(dataSourceInfoRow.Table.Columns.Contains(DbMetaDataColumnNames.DataSourceProductVersion));
            Debug.Assert(dataSourceInfoRow.Table.Columns.Contains(DbMetaDataColumnNames.DataSourceProductVersionNormalized));

            dataSourceInfoRow[DbMetaDataColumnNames.DataSourceProductVersion] = _serverVersion;
            dataSourceInfoRow[DbMetaDataColumnNames.DataSourceProductVersionNormalized] = _serverVersion;
        }

        private static DataTable CreateMetaDataCollectionsDataTable()
            => new(DbMetaDataCollectionNames.MetaDataCollections)
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
            => new(DbMetaDataCollectionNames.Restrictions)
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
            => new(DbMetaDataCollectionNames.DataSourceInformation)
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
            => new(DbMetaDataCollectionNames.DataTypes)
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
            => new(DbMetaDataCollectionNames.ReservedWords)
            {
                Columns =
                {
                    new DataColumn(DbMetaDataColumnNames.ReservedWord, typeof(string)),
                    new DataColumn(MinimumVersionKey, typeof(string)),
                    new DataColumn(MaximumVersionKey, typeof(string))
                }
            };
        #endregion
    }
}
