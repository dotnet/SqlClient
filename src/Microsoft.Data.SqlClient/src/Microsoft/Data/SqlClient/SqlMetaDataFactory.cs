// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlMetaDataFactory : DbMetaDataFactory
    {

        private const string ServerVersionNormalized90 = "09.00.0000";
        private const string ServerVersionNormalized10 = "10.00.0000";
        private readonly HashSet<int> _assemblyPropertyUnsupportedEngines = new() { 6, 9, 11 };

        public SqlMetaDataFactory(Stream XMLStream,
                                    string serverVersion,
                                    string serverVersionNormalized) :
                base(XMLStream, serverVersion, serverVersionNormalized)
        { }

        private async ValueTask AddUDTsToDataTypesTableAsync(DataTable dataTypesTable, SqlConnection connection, string ServerVersion, bool isAsync, CancellationToken cancellationToken)
        {
            const string sqlCommand =
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

            // pre 9.0/2005 servers do not have UDTs
            if (0 > string.Compare(ServerVersion, ServerVersionNormalized90, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SqlCommand engineEditionCommand = connection.CreateCommand();
            engineEditionCommand.CommandText = "SELECT SERVERPROPERTY('EngineEdition');";
            var engineEdition = (int)(isAsync ? await engineEditionCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) : engineEditionCommand.ExecuteScalar());

            if (_assemblyPropertyUnsupportedEngines.Contains(engineEdition))
            {
                // Azure SQL Edge (9) throws an exception when querying sys.assemblies
                // Azure Synapse Analytics (6) and Azure Synapse serverless SQL pool (11)
                // do not support ASSEMBLYPROPERTY
                return;
            }

            // Execute the SELECT statement
            SqlCommand command = connection.CreateCommand();
            command.CommandText = sqlCommand;
            DataColumn providerDbtype = dataTypesTable.Columns[DbMetaDataColumnNames.ProviderDbType];
            DataColumn columnSize = dataTypesTable.Columns[DbMetaDataColumnNames.ColumnSize];
            DataColumn isFixedLength = dataTypesTable.Columns[DbMetaDataColumnNames.IsFixedLength];
            DataColumn isSearchable = dataTypesTable.Columns[DbMetaDataColumnNames.IsSearchable];
            DataColumn isLiteralSupported = dataTypesTable.Columns[DbMetaDataColumnNames.IsLiteralSupported];
            DataColumn typeName = dataTypesTable.Columns[DbMetaDataColumnNames.TypeName];
            DataColumn isNullable = dataTypesTable.Columns[DbMetaDataColumnNames.IsNullable];

            if ((providerDbtype == null) ||
                (columnSize == null) ||
                (isFixedLength == null) ||
                (isSearchable == null) ||
                (isLiteralSupported == null) ||
                (typeName == null) ||
                (isNullable == null))
            {
                throw ADP.InvalidXml();
            }

            const int columnSizeIndex = 10;
            const int isFixedLengthIndex = 9;
            const int isNullableIndex = 8;
            const int assemblyNameIndex = 0;
            const int assemblyClassIndex = 1;
            const int versionMajorIndex = 2;
            const int versionMinorIndex = 3;
            const int versionBuildIndex = 4;
            const int versionRevisionIndex = 5;
            const int cultureInfoIndex = 6;
            const int publicKeyIndex = 7;


            using (DbDataReader reader = isAsync ? await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false) : command.ExecuteReader())
            {

                object[] values = new object[11];
                while (isAsync ? await reader.ReadAsync(cancellationToken).ConfigureAwait(false) : reader.Read())
                {
                    reader.GetValues(values);
                    DataRow newRow = dataTypesTable.NewRow();

                    newRow[providerDbtype] = SqlDbType.Udt;

                    if (values[columnSizeIndex] != DBNull.Value)
                    {
                        newRow[columnSize] = values[columnSizeIndex];
                    }

                    if (values[isFixedLengthIndex] != DBNull.Value)
                    {
                        newRow[isFixedLength] = values[isFixedLengthIndex];
                    }

                    newRow[isSearchable] = true;
                    newRow[isLiteralSupported] = false;
                    if (values[isNullableIndex] != DBNull.Value)
                    {
                        newRow[isNullable] = values[isNullableIndex];
                    }

                    if ((values[assemblyNameIndex] != DBNull.Value) &&
                        (values[assemblyClassIndex] != DBNull.Value) &&
                        (values[versionMajorIndex] != DBNull.Value) &&
                        (values[versionMinorIndex] != DBNull.Value) &&
                        (values[versionBuildIndex] != DBNull.Value) &&
                        (values[versionRevisionIndex] != DBNull.Value))
                    {

                        StringBuilder nameString = new();
                        nameString.Append(values[assemblyClassIndex].ToString());
                        nameString.Append(", ");
                        nameString.Append(values[assemblyNameIndex].ToString());
                        nameString.Append(", Version=");

                        nameString.Append(values[versionMajorIndex].ToString());
                        nameString.Append(".");
                        nameString.Append(values[versionMinorIndex].ToString());
                        nameString.Append(".");
                        nameString.Append(values[versionBuildIndex].ToString());
                        nameString.Append(".");
                        nameString.Append(values[versionRevisionIndex].ToString());

                        if (values[cultureInfoIndex] != DBNull.Value)
                        {
                            nameString.Append(", Culture=");
                            nameString.Append(values[cultureInfoIndex].ToString());
                        }

                        if (values[publicKeyIndex] != DBNull.Value)
                        {

                            nameString.Append(", PublicKeyToken=");

                            StringBuilder resultString = new();
                            byte[] byteArrayValue = (byte[])values[publicKeyIndex];
                            foreach (byte b in byteArrayValue)
                            {
                                resultString.Append(string.Format("{0,-2:x2}", b));
                            }
                            nameString.Append(resultString.ToString());
                        }

                        newRow[typeName] = nameString.ToString();
                        dataTypesTable.Rows.Add(newRow);
                        newRow.AcceptChanges();
                    } // if assembly name

                    cancellationToken.ThrowIfCancellationRequested();
                }//end while
            } // end using
        }

        private async ValueTask AddTVPsToDataTypesTableAsync(DataTable dataTypesTable, SqlConnection connection, string ServerVersion, bool isAsync, CancellationToken cancellationToken)
        {

            const string sqlCommand =
                "select " +
                    "name, " +
                    "is_nullable, " +
                    "max_length " +
                "from sys.types " +
                "where is_table_type = 1";

            // TODO: update this check once the server upgrades major version number!!!
            // pre 9.0/2005 servers do not have Table types
            if (0 > string.Compare(ServerVersion, ServerVersionNormalized10, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Execute the SELECT statement
            SqlCommand command = connection.CreateCommand();
            command.CommandText = sqlCommand;
            DataColumn providerDbtype = dataTypesTable.Columns[DbMetaDataColumnNames.ProviderDbType];
            DataColumn columnSize = dataTypesTable.Columns[DbMetaDataColumnNames.ColumnSize];
            DataColumn isSearchable = dataTypesTable.Columns[DbMetaDataColumnNames.IsSearchable];
            DataColumn isLiteralSupported = dataTypesTable.Columns[DbMetaDataColumnNames.IsLiteralSupported];
            DataColumn typeName = dataTypesTable.Columns[DbMetaDataColumnNames.TypeName];
            DataColumn isNullable = dataTypesTable.Columns[DbMetaDataColumnNames.IsNullable];

            if ((providerDbtype == null) ||
                (columnSize == null) ||
                (isSearchable == null) ||
                (isLiteralSupported == null) ||
                (typeName == null) ||
                (isNullable == null))
            {
                throw ADP.InvalidXml();
            }

            const int columnSizeIndex = 2;
            const int isNullableIndex = 1;
            const int typeNameIndex = 0;

            using (DbDataReader reader = isAsync ? await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false) : command.ExecuteReader())
            {

                object[] values = new object[11];
                while (isAsync ? await reader.ReadAsync(cancellationToken).ConfigureAwait(false) : reader.Read())
                {

                    reader.GetValues(values);
                    DataRow newRow = dataTypesTable.NewRow();

                    newRow[providerDbtype] = SqlDbType.Structured;

                    if (values[columnSizeIndex] != DBNull.Value)
                    {
                        newRow[columnSize] = values[columnSizeIndex];
                    }

                    newRow[isSearchable] = false;
                    newRow[isLiteralSupported] = false;
                    if (values[isNullableIndex] != DBNull.Value)
                    {
                        newRow[isNullable] = values[isNullableIndex];
                    }

                    if (values[typeNameIndex] != DBNull.Value)
                    {
                        newRow[typeName] = values[typeNameIndex];
                        dataTypesTable.Rows.Add(newRow);
                        newRow.AcceptChanges();
                    } // if type name

                    cancellationToken.ThrowIfCancellationRequested();
                }//end while
            } // end using
        }

        private async ValueTask<DataTable> GetDataTypesTableAsync(SqlConnection connection, bool isAsync, CancellationToken cancellationToken)
        {
            // verify the existence of the table in the data set
            DataTable dataTypesTable = CollectionDataSet.Tables[DbMetaDataCollectionNames.DataTypes];
            if (dataTypesTable == null)
            {
                throw ADP.UnableToBuildCollection(DbMetaDataCollectionNames.DataTypes);
            }

            cancellationToken.ThrowIfCancellationRequested();
            // copy the table filtering out any rows that don't apply to tho current version of the provider
            dataTypesTable = CloneAndFilterCollection(DbMetaDataCollectionNames.DataTypes, null);

            await AddUDTsToDataTypesTableAsync(dataTypesTable, connection, ServerVersionNormalized, isAsync, cancellationToken).ConfigureAwait(false);
            await AddTVPsToDataTypesTableAsync(dataTypesTable, connection, ServerVersionNormalized, isAsync, cancellationToken).ConfigureAwait(false);

            dataTypesTable.AcceptChanges();
            return dataTypesTable;
        }

        protected async override ValueTask<DataTable> PrepareCollectionAsync(string collectionName, string[] restrictions, DbConnection connection, bool isAsync, CancellationToken cancellationToken)
        {
            SqlConnection sqlConnection = (SqlConnection)connection;
            DataTable resultTable = null;

            if (collectionName == DbMetaDataCollectionNames.DataTypes)
            {
                if (ADP.IsEmptyArray(restrictions) == false)
                {
                    throw ADP.TooManyRestrictions(DbMetaDataCollectionNames.DataTypes);
                }
                resultTable = await GetDataTypesTableAsync(sqlConnection, isAsync, cancellationToken).ConfigureAwait(false);
            }

            if (resultTable == null)
            {
                throw ADP.UnableToBuildCollection(collectionName);
            }

            return resultTable;

        }



    }
}
