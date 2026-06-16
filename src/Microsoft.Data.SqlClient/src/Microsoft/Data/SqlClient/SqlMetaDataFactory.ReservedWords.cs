// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient;

internal sealed partial class SqlMetaDataFactory
{
    /// <summary>
    /// Returns server reserved words
    /// </summary>
    /// <remarks>
    /// These reserved words are defined by the server, and vary depending upon the version
    /// and edition.
    /// </remarks>
    /// <see href="https://learn.microsoft.com/en-us/sql/t-sql/language-elements/reserved-keywords-transact-sql" />
    private sealed class ReservedWordsCollection : MetaDataCollectionBase
    {
        // @TODO: These have been ported from the existing XML resource file, but they don't perfectly
        // align with the referenced link. These need to be reviewed, and if it's correct to add
        // the new keywords then they need to indicate which version of SQL Server introduced them.
        // @TODO: Azure Synapse Analytics also has an extra reserved keyword. This isn't included at
        // the moment, but if we choose to do so then we need a way to identify such. Doing so may
        // be non-trivial, depending upon whether we query SERVERPROPERTY('EngineEdition') or use a
        // similar approach to ADP.IsAzureSynapseOnDemandEndpoint (i.e. check the data source string.)
        private static readonly string[] s_reservedWords = [
            // Reserved keywords used by SQL Server and Azure Synapse Analytics.
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
            "WHERE", "WHILE", "WITH", /* "WITHIN GROUP", */ "WRITETEXT",

            // ODBC reserved keywords.
            "ABSOLUTE", "ACTION", "ADA", "ALLOCATE", "ARE", "ASSERTION", "AT", "AVG", "BIT",
            "BIT_LENGTH", "BOTH", "CASCADED", "CAST", "CATALOG", "CHAR", "CHAR_LENGTH", "CHARACTER", "CHARACTER_LENGTH",
            "COLLATION", "CONNECT", "CONNECTION", "CONSTRAINTS", "CORRESPONDING", "COUNT", "DATE", "DAY", "DECIMAL",
            "DEFERRABLE", "DEFERRED", "DESCRIBE", "DESCRIPTOR", "DIAGNOSTICS", "DISCONNECT", "DOMAIN", "END-EXEC", "EXCEPTION",
            "EXTRACT", "FALSE", "FIRST", "FLOAT", "FORTRAN", "FOUND", "GET", "GLOBAL", "GO",
            "HOUR", "IMMEDIATE", "INCLUDE", "INDICATOR", "INITIALLY", "INPUT", "INSENSITIVE", "INT", "INTEGER",
            "INTERVAL", "ISOLATION", "LANGUAGE", "LAST", "LEADING", "LEVEL", "LOCAL", "LOWER", "MATCH",
            "MAX", "MIN", "MINUTE", "MODULE", "MONTH", "NAMES", "NATURAL", "NCHAR", "NEXT",
            "NO", "NONE", "NUMERIC", "OCTET_LENGTH", "ONLY", "OUTPUT", "OVERLAPS", "PAD", "PASCAL",
            "POSITION", "PREPARE", "PRESERVE", "PRIOR", "PRIVILEGES", "REAL", "RELATIVE", "ROWS", "SCROLL",
            "SECOND", "SECTION", "SESSION", "SIZE", "SMALLINT", "SPACE", "SQL", "SQLCA", "SQLCODE",
            "SQLERROR", "SQLSTATE", "SQLWARNING", "SUBSTRING", "SUM", "TEMPORARY", "TIME", "TIMESTAMP", "TIMEZONE_HOUR",
            "TIMEZONE_MINUTE", "TRAILING", "TRANSLATE", "TRANSLATION", "TRIM", "TRUE", "UNKNOWN", "UPPER", "USAGE",
            "USING", "VALUE", "VARCHAR", "WHENEVER", "WORK", "WRITE", "YEAR", "ZONE",

            // Future reserved keywords.
            // @TODO: Missing keywords: ASENSITIVE, ASYMMETRIC, ATOMIC
            "ADMIN", "AFTER", "AGGREGATE", "ALIAS", "ARRAY", /* "ASENSITIVE", "ASYMMETRIC", "ATOMIC", */ "BEFORE",
            // @TODO: Missing keyword: CALLED, CARDINALITY
            "BINARY", "BLOB", "BOOLEAN", "BREADTH", "CALL", /* "CALLED", "CARDINALITY", */ "CLASS", "CLOB",
            // @TODO: Missing keywords: COLLECT, CONDITION, CORR, COVAR_POP, COVAR_SAMP, CUME_DIST
            /* "COLLECT", */ "COMPLETION", /* "CONDITION", */ "CONSTRUCTOR", /* "CORR", "COVAR_POP", "COVAR_SAMP", */ "CUBE", /* "CUME_DIST", */
            // @TODO: Missing keywords: CURRENT_CATALOG, CURRENT_DEFAULT_TRANSFORM_GROUP
            /* "CURRENT_CATALOG", "CURRENT_DEFAULT_TRANSFORM_GROUP", */ "CURRENT_PATH", "CURRENT_ROLE",
            // @TODO: Missing keywords: CURRENT_SCHEMA, CURRENT_TRANSFORM_GROUP_FOR_TYPE
            /* "CURRENT_SCHEMA", "CURRENT_TRANSFORM_GROUP_FOR_TYPE", */ "CYCLE", "DATA", "DEC",
            // @TODO: Missing keyword: ELEMENT
            "DEPTH", "DEREF", "DESTROY", "DESTRUCTOR", "DETERMINISTIC", "DICTIONARY", "DYNAMIC", "EACH", /* "ELEMENT", */
            // @TODO: Missing keywords: FILTER, FULLTEXTTABLE, FUSION, HOLD
            "EQUALS", "EVERY", /* "FILTER", */ "FREE", /* "FULLTEXTTABLE", "FUSION", */ "GENERAL", "GROUPING", /* "HOLD", */
            // @TODO: Missing keyword: INTERSECTION
            "HOST", "IGNORE", "INITIALIZE", "INOUT", /* "INTERSECTION", */ "ITERATE", "LARGE", "LATERAL", "LESS",
            // @TODO: Missing keywords: LIKE_REGEX, LN, MEMBER, METHOD
            /* "LIKE_REGEX",*/ "LIMIT", /* "LN", */ "LOCALTIME", "LOCALTIMESTAMP", "LOCATOR", "MAP", /* "MEMBER", "METHOD", */
            // @TODO: Missing keywords: MOD, MULTISET, NORMALIZE, OCCURRENCES_REGEX
            /* "MOD", */ "MODIFIES", "MODIFY", /* "MULTISET", */ "NCLOB", "NEW", /* "NORMALIZE", */ "OBJECT", /* "OCCURRENCES_REGEX", */
            // @TODO: Missing keyword: OVERLAY, PARTITION
            "OLD", "OPERATION", "ORDINALITY", "OUT", /* "OVERLAY", */ "PARAMETER", "PARAMETERS", "PARTIAL", /* "PARTITION" */
            // @TODO: Missing keywords: PERCENT_RANK, PERCENTILE_CONT, PERCENTILE_DISC, POSITION_REGEX, RANGE
            "PATH", "POSTFIX", "PREFIX", "PREORDER", /* "PERCENT_RANK", "PERCENTILE_CONT", "PERCENTILE_DISC", "POSITION_REGEX", "RANGE", */
            "READS", "RECURSIVE", "REF", "REFERENCING",
            // @TODO: Missing keywords: REGR_AVGX, REGR_AVGY, REGR_COUNT, REGR_INTERCEPT, REGR_R2, REGR_SLOPE
            /* "REGR_AVGX", "REGR_AVGY", "REGR_COUNT", "REGR_INTERCEPT", "REGR_R2", "REGR_SLOPE", */
            // @TODO: Missing keywords: REGR_SXX, REGR_SXY, REGR_SYY, RELEASE
            /* "REGR_SXX", "REGR_SXY", "REGR_SYY", "RELEASE", */ "RESULT", "RETURNS", "ROLE", "ROLLUP", "ROUTINE",
            // @TODO: Missing keywords: SENSITIVE, SIMILAR
            "ROW", "SAVEPOINT", "SCOPE", "SEARCH", /* "SENSITIVE", */ "SEQUENCE", "SETS", /* "SIMILAR", */ "SPECIFIC",
            // @TODO: Missing keywords: STDDEV_POP, STDDEV_SAMP
            "SPECIFICTYPE", "SQLEXCEPTION", "START", "STATE", "STATEMENT", "STATIC", /* "STDDEV_POP", "STDDEV_SAMP", */ "STRUCTURE",

            // @TODO: Missing keywords: SUBMULTISET, SUBSTRING_REGEX, SYMMETRIC, SYSTEM, TRANSLATE_REGEX, UESCAPE
            /* "SUBMULTISET", "SUBSTRING_REGEX", "SYMMETRIC", "SYSTEM", */ "TERMINATE", "THAN", /* "TRANSLATE_REGEX", */ "TREAT", /* "UESCAPE", */
            // @TODO: Missing keywords: VAR_POP, VAR_SAMP, WIDTH_BUCKET, WINDOW, WITHIN
            "UNDER", "UNNEST", /* "VAR_POP", "VAR_SAMP", */ "VARIABLE", /* "WIDTH_BUCKET", */ "WITHOUT", /* , "WINDOW", "WITHIN", */
            // @TODO: Missing keywords: XMLAGG, XMLATTRIBUTES, XMLBINARY, XMLCAST, XMLCOMMENT, XMLCONCAT, XMLDOCUMENT, XMLELEMENT, XMLEXISTS
            /* "XMLAGG", "XMLATTRIBUTES", "XMLBINARY", "XMLCAST", "XMLCOMMENT", "XMLCONCAT", "XMLDOCUMENT", "XMLELEMENT", "XMLEXISTS", */
            // @TODO: Missing keywords: XMLFOREST, XMLITERATE, XMLNAMESPACES, XMLPARSE, XMLPI, XMLQUERY, XMLSERIALIZE, XMLTABLE, XMLTEXT
            /* "XMLFOREST", "XMLITERATE", "XMLNAMESPACES", "XMLPARSE", "XMLPI", "XMLQUERY", "XMLSERIALIZE", "XMLTABLE", "XMLTEXT", */
            // @TODO: Missing keyword: XMLVALIDATE
            /* "XMLVALIDATE" */

            // Keywords which appear in the SQL Server 2000 documentation but not in newer versions.
            // Preserved for backwards compatibility purposes.
             "DUMMY"
            ];

        internal ReservedWordsCollection()
            : base(DbMetaDataCollectionNames.ReservedWords, 0, 0)
        {
        }

        public override ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable accumulator = null)
        {
            if (!ADP.IsEmptyArray(context.RestrictionValues))
            {
                throw ADP.TooManyRestrictions(CollectionName);
            }

            DataTable table = accumulator ?? new(CollectionName)
            {
                Columns =
                {
                    new DataColumn(DbMetaDataColumnNames.ReservedWord, typeof(string))
                }
            };

            foreach (string reservedWord in s_reservedWords)
            {
                DataRow row = table.NewRow();
                row[DbMetaDataColumnNames.ReservedWord] = reservedWord;
                table.Rows.Add(row);
            }

            return new ValueTask<DataTable>(table);
        }
    }
}
