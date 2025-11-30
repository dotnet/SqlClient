// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlMetaDataFactory : DbMetaDataFactory
    {

        private const string ServerVersionNormalized90 = "09.00.0000";
        private const string ServerVersionNormalized10 = "10.00.0000";
        private static readonly HashSet<int> s_assemblyPropertyUnsupportedEngines = new() { 6, 9, 11 };

        public SqlMetaDataFactory(Stream XMLStream, string serverVersion) :
                base(XMLStream, serverVersion)
        { }

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
            Debug.Assert(CollectionDataSet.Tables.Contains(DbMetaDataCollectionNames.DataTypes), "DataTypes collection not found in the DataSet");

            // copy the table filtering out any rows that don't apply to tho current version of the provider
            DataTable dataTypesTable = CloneAndFilterCollection(DbMetaDataCollectionNames.DataTypes, null);

            AddUDTsToDataTypesTable(dataTypesTable, connection, ServerVersion);
            AddTVPsToDataTypesTable(dataTypesTable, connection, ServerVersion);

            dataTypesTable.AcceptChanges();
            return dataTypesTable;
        }

        protected override DataTable PrepareCollection(string collectionName, string[] restrictions, DbConnection connection)
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
    }
}
