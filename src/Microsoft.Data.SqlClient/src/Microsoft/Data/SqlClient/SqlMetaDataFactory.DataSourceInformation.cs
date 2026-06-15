// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlMetaDataFactory
    {
        private sealed class DataSourceInformationCollection : MetaDataCollectionBase
        {
            private readonly string _compositeIdentifierSeparatorPattern;
            private readonly string _dataSourceProductName;
            private readonly GroupByBehavior _groupByBehavior;
            private readonly string _identifierPattern;
            private readonly IdentifierCase _identifierCase;
            private readonly bool _orderByColumnsInSelect;
            private readonly string _parameterMarkerFormat;
            private readonly string _parameterMarkerPattern;
            private readonly int _parameterNameMaxLength;
            private readonly string _parameterNamePattern;
            private readonly string _quotedIdentifierPattern;
            private readonly IdentifierCase _quotedIdentifierCase;
            private readonly string _statementSeparatorPattern;
            private readonly string _stringLiteralPattern;
            private readonly SupportedJoinOperators _supportedJoinOperators;

            internal DataSourceInformationCollection(string CompositeIdentifierSeparatorPattern,
                                                     string DataSourceProductName,
                                                     GroupByBehavior GroupByBehavior,
                                                     string IdentifierPattern,
                                                     IdentifierCase IdentifierCase,
                                                     bool OrderByColumnsInSelect,
                                                     string ParameterMarkerFormat,
                                                     string ParameterMarkerPattern,
                                                     int ParameterNameMaxLength,
                                                     string ParameterNamePattern,
                                                     string QuotedIdentifierPattern,
                                                     IdentifierCase QuotedIdentifierCase,
                                                     string StatementSeparatorPattern,
                                                     string StringLiteralPattern,
                                                     SupportedJoinOperators SupportedJoinOperators)
                : base(DbMetaDataCollectionNames.DataSourceInformation, 0, 0)
            {
                this._compositeIdentifierSeparatorPattern = CompositeIdentifierSeparatorPattern;
                this._dataSourceProductName = DataSourceProductName;
                this._groupByBehavior = GroupByBehavior;
                this._identifierPattern = IdentifierPattern;
                this._identifierCase = IdentifierCase;
                this._orderByColumnsInSelect = OrderByColumnsInSelect;
                this._parameterMarkerFormat = ParameterMarkerFormat;
                this._parameterMarkerPattern = ParameterMarkerPattern;
                this._parameterNameMaxLength = ParameterNameMaxLength;
                this._parameterNamePattern = ParameterNamePattern;
                this._quotedIdentifierPattern = QuotedIdentifierPattern;
                this._quotedIdentifierCase = QuotedIdentifierCase;
                this._statementSeparatorPattern = StatementSeparatorPattern;
                this._stringLiteralPattern = StringLiteralPattern;
                this._supportedJoinOperators = SupportedJoinOperators;
            }

            public override ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable accumulator = null)
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

                DataRow row = table.NewRow();
                table.Rows.Add([
                    _compositeIdentifierSeparatorPattern,
                    _dataSourceProductName,
                    context.ServerVersion,
                    context.ServerVersion,
                    _groupByBehavior,
                    _identifierPattern,
                    _identifierCase,
                    _orderByColumnsInSelect,
                    _parameterMarkerFormat,
                    _parameterMarkerPattern,
                    _parameterNameMaxLength,
                    _parameterNamePattern,
                    _quotedIdentifierPattern,
                    _quotedIdentifierCase,
                    _statementSeparatorPattern,
                    _stringLiteralPattern,
                    _supportedJoinOperators
                    ]);
                return new ValueTask<DataTable>(table);
            }
        }
    }
}
