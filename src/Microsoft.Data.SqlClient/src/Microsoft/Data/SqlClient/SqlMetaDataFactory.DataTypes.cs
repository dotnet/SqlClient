// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Common;

#nullable enable

namespace Microsoft.Data.SqlClient;

internal sealed partial class SqlMetaDataFactory
{
    private sealed class DataTypesCollection : MetaDataCollectionBase
    {
        private readonly TypeMetaData[] _types;

        internal DataTypesCollection(TypeMetaData[] types)
            : base(DbMetaDataCollectionNames.DataTypes, 0, 0)
        {
            _types = types;
        }

        public async override ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable? accumulator = null)
        {
            if (ADP.IsEmptyArray(context.RestrictionValues) == false)
            {
                throw ADP.TooManyRestrictions(DbMetaDataCollectionNames.DataTypes);
            }

            DataTable result = accumulator ?? new(DbMetaDataCollectionNames.DataTypes)
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
                    //new DataColumn(MaximumVersionKey, typeof(string)),
                    //new DataColumn(MinimumVersionKey, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.IsLiteralSupported, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.LiteralPrefix, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.LiteralSuffix, typeof(string))
                }
            };

            // 1. Load built-in types
            result.BeginLoadData();
            foreach(TypeMetaData t in _types)
            {
                if ((t.MinimumVersion == null || string.Compare(context.ServerVersion, t.MinimumVersion, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    (t.MaximumVersion == null || string.Compare(context.ServerVersion, t.MaximumVersion, StringComparison.OrdinalIgnoreCase) <= 0))
                {
                    DataRow row = result.NewRow();
                    row[DbMetaDataColumnNames.TypeName] = t.TypeName;
                    row[DbMetaDataColumnNames.ProviderDbType] = t.ProviderDbType;
                    if (t.ColumnSize > -1)
                    {
                        row[DbMetaDataColumnNames.ColumnSize] = t.ColumnSize;
                    }

                    row[DbMetaDataColumnNames.CreateFormat] = t.CreateFormat;
                    row[DbMetaDataColumnNames.CreateParameters] = t.CreateParameters;
                    row[DbMetaDataColumnNames.DataType] = t.DataType;
                    row[DbMetaDataColumnNames.IsAutoIncrementable] = t.IsAutoIncrementable;
                    row[DbMetaDataColumnNames.IsBestMatch] = t.IsBestMatch;
                    row[DbMetaDataColumnNames.IsCaseSensitive] = t.IsCaseSensitive;
                    row[DbMetaDataColumnNames.IsFixedLength] = t.IsFixedLength;
                    row[DbMetaDataColumnNames.IsFixedPrecisionScale] = t.IsFixedPrecisionScale;
                    row[DbMetaDataColumnNames.IsLong] = t.IsLong;
                    row[DbMetaDataColumnNames.IsNullable] = t.IsNullable;
                    row[DbMetaDataColumnNames.IsSearchable] = t.IsSearchable;
                    row[DbMetaDataColumnNames.IsSearchableWithLike] = t.IsSearchableWithLike;
                    if (t.IsUnsigned != null)
                    {
                        row[DbMetaDataColumnNames.IsUnsigned] = t.IsUnsigned;
                    }

                    if (t.MaximumScale > -1)
                    {
                        row[DbMetaDataColumnNames.MaximumScale] = t.MaximumScale;
                    }

                    if (t.MinimumScale > -1)
                    {
                        row[DbMetaDataColumnNames.MinimumScale] = t.MinimumScale;
                    }

                    row[DbMetaDataColumnNames.IsConcurrencyType] = t.IsConcurrencyType;
                    if (t.IsLiteralSupported != null)
                    {
                        row[DbMetaDataColumnNames.IsLiteralSupported] = t.IsLiteralSupported;
                    }

                    row[DbMetaDataColumnNames.LiteralPrefix] = t.LiteralPrefix;
                    row[DbMetaDataColumnNames.LiteralSuffix] = t.LiteralSuffix;
                    result.Rows.Add(row);
                }
            }
            result.EndLoadData();
            result.AcceptChanges();

            // 2. Add UDTs from the server if supported
            MetaDataCollectionBase udtCollection = FindMetaDataCollection("UDTs", context.ServerVersion);
            if (udtCollection != null)
            {
                const string GetEngineEditionSqlCommand = "SELECT SERVERPROPERTY('EngineEdition');";
                using SqlCommand command = ((SqlConnection)context.Connection).CreateCommand();

                command.CommandText = GetEngineEditionSqlCommand;
                int engineEdition = (int)(context.IsAsync ? await command.ExecuteScalarAsync(context.CancellationToken).ConfigureAwait(false) : command.ExecuteScalar());
                // Azure SQL Edge (9) throws an exception when querying sys.assemblies
                // Azure Synapse Analytics (6) and Azure Synapse serverless SQL pool (11)
                // do not support ASSEMBLYPROPERTY
                if (!s_assemblyPropertyUnsupportedEngines.Contains(engineEdition))
                {
                    await udtCollection.GetMetadata(context, result);
                }
            }

            // 3. Add TVPs from the server if supported
            MetaDataCollectionBase tvpCollection = FindMetaDataCollection("TVPs", context.ServerVersion);
            if (tvpCollection != null)
            {
                await tvpCollection.GetMetadata(context, result);
            }

            return result;
        }
    }

    private sealed class TypeMetaData
    {
        public readonly string TypeName;
        public readonly int ProviderDbType;
        public readonly long ColumnSize;
        public readonly string CreateFormat;
        public readonly string? CreateParameters;
        public readonly string DataType;
        public readonly bool IsAutoIncrementable;
        public readonly bool IsBestMatch;
        public readonly bool IsCaseSensitive;
        public readonly bool IsFixedLength;
        public readonly bool IsFixedPrecisionScale;
        public readonly bool IsLong;
        public readonly bool IsNullable;
        public readonly bool IsSearchable;
        public readonly bool IsSearchableWithLike;
        public readonly bool? IsUnsigned;
        public readonly short MaximumScale;
        public readonly short MinimumScale;
        public readonly bool IsConcurrencyType;
        public readonly string? MaximumVersion;
        public readonly string? MinimumVersion;
        public readonly bool? IsLiteralSupported;
        public readonly string? LiteralPrefix;
        public readonly string? LiteralSuffix;

        public TypeMetaData(string typeName, int providerDbType, long columnSize, string createFormat,
            string dataType, bool isAutoIncrementable, bool isBestMatch, bool isCaseSensitive, bool isFixedLength,
            bool isFixedPrecisionScale, bool isLong, bool isNullable, bool isSearchable, bool isSearchableWithLike,
            bool? isUnsigned, short maximumScale, short minimumScale, bool isConcurrencyType,
            string? minimumVersion, string? maximumVersion, bool? isLiteralSupported, string? literalPrefix, string? literalSuffix, string createParameters)
        {
            TypeName = typeName;
            ProviderDbType = providerDbType;
            ColumnSize = columnSize;
            CreateFormat = createFormat;
            CreateParameters = createParameters;
            DataType = dataType;
            IsAutoIncrementable = isAutoIncrementable;
            IsBestMatch = isBestMatch;
            IsCaseSensitive = isCaseSensitive;
            IsFixedLength = isFixedLength;
            IsFixedPrecisionScale = isFixedPrecisionScale;
            IsLong = isLong;
            IsNullable = isNullable;
            IsSearchable = isSearchable;
            IsSearchableWithLike = isSearchableWithLike;
            IsUnsigned = isUnsigned;
            MaximumScale = maximumScale;
            MinimumScale = minimumScale;
            IsConcurrencyType = isConcurrencyType;
            MaximumVersion = maximumVersion;
            MinimumVersion = minimumVersion;
            IsLiteralSupported = isLiteralSupported;
            LiteralPrefix = literalPrefix;
            LiteralSuffix = literalSuffix;
        }

        public TypeMetaData(SqlDbType target,
            string dataType, bool isAutoIncrementable, bool isBestMatch, bool isCaseSensitive, bool isFixedLength,
            bool isFixedPrecisionScale, bool isLong, bool isNullable, bool isSearchable, bool isSearchableWithLike,
            bool isUnsigned, short maximumScale, short minimumScale, bool isConcurrencyType,
            string maximumVersion, string minimumVersion, bool isLiteralSupported, string literalPrefix, string literalSuffix)
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(target, isMultiValued: false);

            TypeName = metaType.TypeName;
            ProviderDbType = (int)metaType.SqlDbType;
            ColumnSize = metaType.FixedLength;
            CreateFormat = metaType.TypeName;
            CreateParameters = metaType.ClassType.FullName;
            DataType = dataType;
            IsAutoIncrementable = isAutoIncrementable;
            IsBestMatch = isBestMatch;
            IsCaseSensitive = isCaseSensitive;
            IsFixedLength = isFixedLength;
            IsFixedPrecisionScale = isFixedPrecisionScale;
            IsLong = isLong;
            IsNullable = isNullable;
            IsSearchable = isSearchable;
            IsSearchableWithLike = isSearchableWithLike;
            IsUnsigned = isUnsigned;
            MaximumScale = maximumScale;
            MinimumScale = minimumScale;
            IsConcurrencyType = isConcurrencyType;
            MaximumVersion = maximumVersion;
            MinimumVersion = minimumVersion;
            IsLiteralSupported = isLiteralSupported;
            LiteralPrefix = literalPrefix;
            LiteralSuffix = literalSuffix;
        }
    }
}
