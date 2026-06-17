// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Common;

#nullable enable

namespace Microsoft.Data.SqlClient;

internal sealed partial class SqlMetaDataFactory
{
    /// <summary>
    /// Returns <see href="https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/common-schema-collections#datasourceinformation">
    /// DataSourceInformation</see> schema collection.
    /// </summary>
    private sealed class DataSourceInformationCollection : MetaDataCollectionBase
    {
        private const string CompositeIdentifierSeparatorPattern = "\\.";
        private const string DataSourceProductName = "Microsoft SQL Server";
        private const GroupByBehavior GroupByBehavior = GroupByBehavior.Unrelated;
        private const string IdentifierPattern = @"(^\[\p{Lo}\p{Lu}\p{Ll}_@#][\p{Lo}\p{Lu}\p{Ll}\p{Nd}@$#_]*$)|(^\[[^\]\0]|\]\]+\]$)|(^\""[^\""\0]|\""\""+\""$)";
        private const IdentifierCase IdentifierCase = IdentifierCase.Insensitive;
        private const bool OrderByColumnsInSelect = false;
        private const string _parameterMarkerFormat = "{0}";
        private const string ParameterMarkerPattern = @"@[\p{Lo}\p{Lu}\p{Ll}\p{Lm}_@#][\p{Lo}\p{Lu}\p{Ll}\p{Lm}\p{Nd}\uff3f_@#\$]*(?=\s+|$)";
        private const int ParameterNameMaxLength = 128;
        private const string ParameterNamePattern = @"^[\p{Lo}\p{Lu}\p{Ll}\p{Lm}_@#][\p{Lo}\p{Lu}\p{Ll}\p{Lm}\p{Nd}\uff3f_@#\$]*(?=\s+|$)";
        private const string QuotedIdentifierPattern = "(([^\\[]|\\]\\])*)";
        private const IdentifierCase QuotedIdentifierCase = IdentifierCase.Insensitive;
        private const string StatementSeparatorPattern = ";";
        private const string StringLiteralPattern = "'(([^']|'')*)'";
        private const SupportedJoinOperators SupportedJoinOperators = SupportedJoinOperators.Inner
                                                                     | SupportedJoinOperators.LeftOuter
                                                                     | SupportedJoinOperators.RightOuter
                                                                     | SupportedJoinOperators.FullOuter;

        internal DataSourceInformationCollection()
            : base(DbMetaDataCollectionNames.DataSourceInformation, 0, 0)
        {
        }

        public override ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable? accumulator = null)
        {
            if (!ADP.IsEmptyArray(context.RestrictionValues))
            {
                throw ADP.TooManyRestrictions(CollectionName);
            }

            DataTable table = accumulator ?? new(DbMetaDataCollectionNames.DataSourceInformation)
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

            table.Rows.Add([
                CompositeIdentifierSeparatorPattern,
                DataSourceProductName,
                context.ServerVersion,
                context.ServerVersion,
                GroupByBehavior,
                IdentifierPattern,
                IdentifierCase,
                OrderByColumnsInSelect,
                _parameterMarkerFormat,
                ParameterMarkerPattern,
                ParameterNameMaxLength,
                ParameterNamePattern,
                QuotedIdentifierPattern,
                QuotedIdentifierCase,
                StatementSeparatorPattern,
                StringLiteralPattern,
                SupportedJoinOperators
                ]);

            return new ValueTask<DataTable>(table);
        }
    }
}
