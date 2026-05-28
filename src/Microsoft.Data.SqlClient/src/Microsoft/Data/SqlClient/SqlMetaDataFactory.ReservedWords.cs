// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;

#nullable enable

namespace Microsoft.Data.SqlClient;

internal sealed partial class SqlMetaDataFactory
{
    /// <summary>
    /// Adds reserved words to the indicated metadata DataSet.
    /// </summary>
    /// <param name="metaDataCollectionsDataSet">The metadata DataSet to contain the reserved words.</param>
    /// <remarks>
    /// These reserved words are defined by the server, and vary depending upon the version
    /// and edition.
    /// </remarks>
    /// <see href="https://learn.microsoft.com/en-us/sql/t-sql/language-elements/reserved-keywords-transact-sql" />
    private static void LoadReservedWordsDataTables(DataSet metaDataCollectionsDataSet)
    {
        DataTable reservedWordsDataTable = CreateReservedWordsDataTable();

        reservedWordsDataTable.BeginLoadData();

        // @TODO: These have been ported from the existing XML resource file, but they don't perfectly
        // align with the referenced link. These need to be reviewed, and if it's correct to add
        // the new keywords then they need to indicate which version of SQL Server introduced them.
        // @TODO: Azure Synapse Analytics also has an extra reserved keyword. This isn't included at
        // the moment, but if we choose to do so then we need a way to identify such. Doing so may
        // be non-trivial, depending upon whether we query SERVERPROPERTY('EngineEdition') or use a
        // similar approach to ADP.IsAzureSynapseOnDemandEndpoint (i.e. check the data source string.)

        // Add reserved keywords used by SQL Server and Azure Synapse Analytics.
        AddReservedWords(minVersion: null, maxVersion: null);

        // Add ODBC reserved keywords.
        AddReservedWords(minVersion: null, maxVersion: null);

        // Add future reserved keywords.
        AddReservedWords(minVersion: null, maxVersion: null);

        reservedWordsDataTable.EndLoadData();
        reservedWordsDataTable.AcceptChanges();

        metaDataCollectionsDataSet.Tables.Add(reservedWordsDataTable);

        void AddReservedWords(string? minVersion, string? maxVersion, params ReadOnlySpan<string> reservedWords)
        {
            foreach (string reservedWord in reservedWords)
            {
                DataRow wordRow = reservedWordsDataTable.NewRow();

                wordRow[DbMetaDataColumnNames.ReservedWord] = reservedWord;

                if (minVersion is not null)
                {
                    wordRow[MinimumVersionKey] = minVersion;
                }

                if (maxVersion is not null)
                {
                    wordRow[MaximumVersionKey] = maxVersion;
                }

                reservedWordsDataTable.Rows.Add(wordRow);
            }
        }
    }

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
}
