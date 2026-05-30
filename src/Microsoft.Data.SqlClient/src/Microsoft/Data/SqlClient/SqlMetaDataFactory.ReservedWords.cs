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
        AddReservedWords(minVersion: null, maxVersion: null,
            "ADD", "ALL", "ALTER", "AND", "ANY", "AS", "ASC", "AUTHORIZATION", "BACKUP",
            "BEGIN", "BETWEEN", "BREAK", "BROWSE", "BULK", "BY", "CASCADE", "CASE", "CHECK",
            "CHECKPOINT", "CLOSE", "CLUSTERED", "COALESCE", "COLLATE", "COLUMN", "COMMIT", "COMPUTE", "CONSTRAINT",
            "CONTAINS", "CONTAINSTABLE", "CONTINUE", "CONVERT", "CREATE", "CROSS", "CURRENT", "CURRENT_DATE", "CURRENT_TIME",
            "CURRENT_TIMESTAMP", "CURRENT_USER", "CURSOR", "DATABASE", "DBCC", "DEALLOCATE", "DECLARE", "DEFAULT", "DELETE",
            "DENY", "DESC", "DISK", "DISTINCT", "DISTRIBUTED", "DOUBLE", "DROP", "DUMP", "ELSE",
            "END", "ERRLVL", "ESCAPE", "EXCEPT", "EXEC", "EXECUTE", "EXISTS", "EXIT", "EXTERNAL",
            "FETCH", "FILE", "FILLFACTOR", "FOR", "FOREIGN", "FREETEXT", "FREETEXTTABLE", "FROM", "FULL",
            "FUNCTION", "GOTO", "GRANT", "GROUP", "HAVING", "HOLDLOCK", "IDENTITY", "IDENTITY_INSERT", "IDENTITYCOL",
            "IF", "IN", "INDEX", "INNER", "INSERT", "INTERSECT", "INTO", "IS", "JOIN",
            // @TODO: Missing keyword: MERGE
            "KEY", "KILL", "LEFT", "LIKE", "LINENO", "LOAD", /* "MERGE", */ "NATIONAL", "NOCHECK",
            "NONCLUSTERED", "NOT", "NULL", "NULLIF", "OF", "OFF", "OFFSETS", "ON", "OPEN",
            "OPENDATASOURCE", "OPENQUERY", "OPENROWSET", "OPENXML", "OPTION", "OR", "ORDER", "OUTER", "OVER",
            // @TODO: Missing keyword: PIVOT
            "PERCENT", /* "PIVOT", */ "PLAN", "PRECISION", "PRIMARY", "PRINT", "PROC", "PROCEDURE", "PUBLIC",
            "RAISERROR", "READ", "READTEXT", "RECONFIGURE", "REFERENCES", "REPLICATION", "RESTORE", "RESTRICT", "RETURN",
            // @TODO: Missing keyword: REVERT
            /* "REVERT", */ "REVOKE", "RIGHT", "ROLLBACK", "ROWCOUNT", "ROWGUIDCOL", "RULE", "SAVE", "SCHEMA",
            // @TODO: Missing keywords: SECURITYAUDIT, SEMANTICKEYPHRASETABLE, SEMANTICSIMILARITYDETAILSTABLE
            /* "SECURITYAUDIT", */ "SELECT", /* "SEMANTICKEYPHRASETABLE", "SEMANTICSIMILARITYDETAILSTABLE", */
            // @TODO: Missing keyword: SEMANTICSIMILARITYTABLE
            /* "SEMANTICSIMILARITYTABLE", */ "SESSION_USER", "SET", "SETUSER", "SHUTDOWN",
            // @TODO: Missing keyword: TABLESAMPLE
            "SOME", "STATISTICS", "SYSTEM_USER", "TABLE", /* "TABLESAMPLE", */ "TEXTSIZE", "THEN", "TO", "TOP",
            // @TODO: Missing keywords: TRY_CONVERT, UNPIVOT
            "TRAN", "TRANSACTION", "TRIGGER", "TRUNCATE", /* "TRY_CONVERT", */ "TSEQUAL", "UNION", "UNIQUE", /* "UNPIVOT", */
            "UPDATE", "UPDATETEXT", "USE", "USER", "VALUES", "VARYING", "VIEW", "WAITFOR", "WHEN",
            // @TODO: Missing keyword: WITHIN GROUP
            "WHERE", "WHILE", "WITH", /* "WITHIN GROUP", */ "WRITETEXT");

        // Add ODBC reserved keywords. Some of these overlap with the previous category, and are not included.
        AddReservedWords(minVersion: null, maxVersion: null,
            "ABSOLUTE", "ACTION", "ADA", "ALLOCATE", "ARE", "ASSERTION", "AT", "AVG", "BIT",
            "BIT_LENGTH", "BOTH", "CASCADED", "CAST", "CATALOG", "CHAR", "CHAR_LENGTH", "CHARACTER", "CHARACTER_LENGTH",
            "COLLATION", "CONNECT", "CONNECTION", "CONSTRAINTS", "CORRESPONDING", "COUNT", "DATE", "DAY", "DECIMAL",
            "DEFERRABLE", "DEFERRED", "DESCRIBE", "DESCRIPTOR", "DIAGNOSTICS", "DISCONNECT", "DOMAIN", "END-EXEC", "EXCEPTION",
            "EXTRACT", "FALSE", "FIRST", "FLOAT", "FORTRAN", "FOUND", "GET", "GLOBAL", "GO",
            "HOUR", "IMMEDIATE", "INCLUDE", "INDICATOR", "INITIALLY", "INPUT", "INSENSITIVE", "INT", "INTEGER",
            "INTERVAL", "ISOLATION", "LANGUAGE", "LAST", "LEADING", "LEVEL", "LOCAL", "LOWER", "MATCH",
            "MAX", "MIN", "MINUTE", "MODULE", "MONTH", "NAMES", "NATURAL", "NCHAR", "NEXT",
            "NO", "NONE", "NUMERIC", "OCTET_LENGTH", "ONLY", "OUTPUT", "OVERLAPS", "PAD", "PASCAL",
            "POSITION", "PREPARE", "PRESERVE", "PRIOR", "PRIVILEGES", "REAL", "RELATIVE", "ROWS", "SCROLL");

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
