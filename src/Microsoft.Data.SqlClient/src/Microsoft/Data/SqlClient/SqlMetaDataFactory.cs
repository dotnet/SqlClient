// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlMetaDataFactory
    {    
        #pragma warning disable format
        private readonly static MetaDataCollectionBase[] s_metaDataCollection = [
            new MetaDataCollection(),
            new DataSourceInformationCollection(
                CompositeIdentifierSeparatorPattern: "\\.",
                DataSourceProductName: "Microsoft SQL Server",
                GroupByBehavior: GroupByBehavior.Unrelated,
                IdentifierPattern: @"(^\[\p{Lo}\p{Lu}\p{Ll}_@#][\p{Lo}\p{Lu}\p{Ll}\p{Nd}@$#_]*$)|(^\[[^\]\0]|\]\]+\]$)|(^\""[^\""\0]|\""\""+\""$)",
                IdentifierCase: IdentifierCase.Insensitive,
                OrderByColumnsInSelect: false,
                ParameterMarkerFormat: "{0}",
                ParameterMarkerPattern: @"@[\p{Lo}\p{Lu}\p{Ll}\p{Lm}_@#][\p{Lo}\p{Lu}\p{Ll}\p{Lm}\p{Nd}\uff3f_@#\$]*(?=\s+|$)",
                ParameterNameMaxLength: 128,
                ParameterNamePattern: @"^[\p{Lo}\p{Lu}\p{Ll}\p{Lm}_@#][\p{Lo}\p{Lu}\p{Ll}\p{Lm}\p{Nd}\uff3f_@#\$]*(?=\s+|$)",
                QuotedIdentifierPattern:"(([^\\[]|\\]\\])*)",
                QuotedIdentifierCase: IdentifierCase.Insensitive,
                StatementSeparatorPattern: ";",
                StringLiteralPattern: "'(([^']|'')*)'",
                SupportedJoinOperators: SupportedJoinOperators.Inner | SupportedJoinOperators.LeftOuter | SupportedJoinOperators.RightOuter | SupportedJoinOperators.FullOuter),
            new DataTypesCollection([
                // Type order follows the order from SqlMetaData.xml               
                //                                                                                            IsBestMatch             isFixedPrecisionScale   IsSearchable           MaximumScale             MinimumVersion                  LiteralPrefix
                //                      ProviderDbType                                                                IsCaseSensitive         IsLong                  IsSearchableWithLike    MinimumScale                    MaximumVersion          LiteralSuffix
                //   TypeName                ColumnSize CreateFormat          DataType                IsAutoIncrementable     IsFixedLength           IsNullable              isUnsigned              IsConcurrencyType               IsLiteralSupported      CreateParameters
                new ("smallint"        ,16  ,5         ,"smallint"           ,"System.Int16"         ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
                new ("int"             ,8   ,10        ,"int"                ,"System.Int32"         ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
                new ("real"            ,13  ,7         ,"real"               ,"System.Single"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
                new ("float"           ,6   ,53        ,"float({0})"         ,"System.Double"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,"number of bits used to store the mantissa"),
                new ("money"           ,9   ,19        ,"money"              ,"System.Decimal"       ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
                new ("smallmoney"      ,17  ,10        ,"smallmoney"         ,"System.Decimal"       ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
                new ("bit"             ,2   ,1         ,"bit"                ,"System.Boolean"       ,false  ,false  ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
                new ("tinyint"         ,20  ,3         ,"tinyint"            ,"System.Byte"          ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
                new ("bigint"          ,0   ,19        ,"bigint"             ,"System.Int64"         ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
                new ("timestamp"       ,19  ,8         ,"timestamp"          ,"System.Byte[]"        ,false  ,false  ,false  ,true   ,false  ,false  ,false  ,true   ,false  ,null   ,-1     ,-1     ,true   ,null           ,null   ,null   ,"0x"   ,null   ,""),
                new ("binary"          ,1   ,8000      ,"binary({0})"        ,"System.Byte[]"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"0x"   ,null   ,"length"),
                new ("image"           ,7   ,2147483647,"image"              ,"System.Byte[]"        ,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"0x"   ,null   ,""),
                new ("text"            ,18  ,2147483647,"text"               ,"System.String"        ,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,""),
                new ("ntext"           ,11  ,1073741823,"ntext"              ,"System.String"        ,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"N'"   ,"'"    ,""),
                new ("decimal"         ,5   ,38        ,"decimal({0}, {1})"  ,"System.Decimal"       ,true   ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,38     ,0      ,false  ,null           ,null   ,null   ,null   ,null   ,"precision,scale"),
                new ("numeric"         ,5   ,38        ,"numeric({0}, {1})"  ,"System.Decimal"       ,true   ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,38     ,0      ,false  ,null           ,null   ,null   ,null   ,null   ,"precision,scale"),
                new ("datetime"        ,4   ,23        ,"datetime"           ,"System.DateTime"      ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"{ts '","'}"   ,""),
                new ("smalldatetime"   ,15  ,16        ,"smalldatetime"      ,"System.DateTime"      ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"{ts '","'}"   ,""),
                new ("sql_variant"     ,23  ,-1        ,"sql_variant"        ,"System.Object"        ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,false  ,null   ,null   ,""),
                new ("xml"             ,25  ,2147483647,"xml"                ,"System.String"        ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,false  ,null   ,null   ,""),
                new ("varchar"         ,22  ,2147483647,"varchar({0})"       ,"System.String"        ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,"max length"),
                new ("char"            ,3   ,2147483647,"char({0})"          ,"System.String"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,"length"),
                new ("nchar"           ,10  ,1073741823,"nchar({0})"         ,"System.String"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"N'"   ,"'"    ,"length"),
                new ("nvarchar"        ,12  ,1073741823,"nvarchar({0})"      ,"System.String"        ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"N'"   ,"'"    ,"max length"),
                new ("varbinary"       ,21  ,1073741823,"varbinary({0})"     ,"System.Byte[]"        ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"0x"   ,null   ,"max length"),
                new ("uniqueidentifier",14  ,16        ,"uniqueidentifier"   ,"System.Guid"          ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,""),
                new ("date"            ,31  ,3         ,"date"               ,"System.DateTime"      ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,""),
                new ("time"            ,32  ,5         ,"time({0})"          ,"System.TimeSpan"      ,false  ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,"scale"),
                new ("datetime2"       ,33  ,8         ,"datetime2({0})"     ,"System.DateTime"      ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,"scale"),
                new ("datetimeoffset"  ,34  ,10        ,"datetimeoffset({0})","System.DateTimeOffset",false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,"scale"),
                new ("json"            ,35  ,2147483647,"json"               ,"System.String"        ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,"17.00.000.0"  ,null   ,false  ,"'"    ,"'"    ,""),
                ]),
            new RestrictionsCollection(),
            new ReservedWordsCollection([
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
                ]),
            new SqlCommandCollection("Users",                  1, 1, null, null,          "select uid, name as user_name, createdate, updatedate from sysusers where (name = @Name or (@Name is null))",
                    [new Restriction(1, "User_Name", "@Name")]),
            new SqlCommandCollection("Databases",              1, 1, null, "09.99.999.9", "select name as database_name, dbid, crdate as create_date from master..sysdatabases where (name = @Name or (@Name is null))",
                    [new Restriction(1, "Name", "@Name")]),
            new SqlCommandCollection("Databases",              1, 1, "10.00.000.0", null, "IF OBJECT_ID('master..sysdatabases') IS NULL EXEC sp_executesql N'select name as database_name, dbid, crdate as create_date from sysdatabases where (name = @Name or (@Name is null))',N'@Name NVARCHAR(128)',@Name=@Name ELSE EXEC sp_executesql N'select name as database_name, dbid, crdate as create_date from master..sysdatabases where (name = @Name or (@Name is null))',N'@Name NVARCHAR(128)',@Name=@Name",
                    [new Restriction(1, "Name", "@Name")]),
            new SqlCommandCollection("Tables",                 4, 3, null,          null, "select TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE from INFORMATION_SCHEMA.TABLES where (TABLE_CATALOG = @Catalog or (@Catalog is null)) and (TABLE_SCHEMA = @Owner or (@Owner is null)) and (TABLE_NAME = @Name or (@Name is null)) and (TABLE_TYPE = @TableType or (@TableType is null))",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Name"),
                     new Restriction(4, "TableType", "@TableType")]),
            new SqlCommandCollection("Columns",                4, 4, null, "09.99.999.9", "select TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, ORDINAL_POSITION, COLUMN_DEFAULT, IS_NULLABLE, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, CHARACTER_OCTET_LENGTH, NUMERIC_PRECISION, NUMERIC_PRECISION_RADIX, NUMERIC_SCALE, DATETIME_PRECISION, CHARACTER_SET_CATALOG, CHARACTER_SET_SCHEMA, CHARACTER_SET_NAME, COLLATION_CATALOG from INFORMATION_SCHEMA.COLUMNS where (TABLE_CATALOG = @Catalog or (@Catalog is null)) and (TABLE_SCHEMA = @Owner or (@Owner is null)) and (TABLE_NAME = @Table or (@Table is null)) and (COLUMN_NAME = @Column or (@Column is null)) order by TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Column", "@Column")]),
            new SqlCommandCollection("Columns",                4, 4, "10.00.000.0", null, "EXEC sys.sp_columns_managed @Catalog, @Owner, @Table, @Column, 0",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Column", "@Column")]),
            new SqlCommandCollection("AllColumns",             4, 4, "10.00.000.0", null, "EXEC sys.sp_columns_managed @Catalog, @Owner, @Table, @Column, 1",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Column", "@Column")]),
            new SqlCommandCollection("ColumnSetColumns",       3, 3, "10.00.000.0", null, "EXEC sys.sp_columns_managed @Catalog, @Owner, @Table, null, 2",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table")]),
            new SqlCommandCollection("StructuredTypeMembers",  4, 4, "10.00.000.0", null, "SELECT DB_NAME() AS TYPE_CATALOG, sc.name AS TYPE_SCHEMA, tt.name AS TYPE_NAME, c.name AS MEMBER_NAME, ColumnProperty(c.object_id, c.name, 'ordinal') AS ORDINAL_POSITION, convert(nvarchar(4000), object_definition(c.default_object_id)) AS MEMBER_DEFAULT, convert(varchar(3), CASE c.is_nullable WHEN 1 THEN 'YES' ELSE 'NO' END) AS IS_NULLABLE, type_name(c.system_type_id) AS DATA_TYPE, ColumnProperty(c.object_id, c.name, 'charmaxlen') AS CHARACTER_MAXIMUM_LENGTH, ColumnProperty(c.object_id, c.name, 'octetmaxlen') AS CHARACTER_OCTET_LENGTH, convert(tinyint, CASE WHEN c.system_type_id IN /* int/decimal/numeric/real/float/money */ (48, 52, 56, 59, 60, 62, 106, 108, 122, 127) THEN c.precision END) AS NUMERIC_PRECISION, convert(smallint, CASE WHEN c.system_type_id IN /* int/money/decimal/numeric */ (48, 52, 56, 60, 106, 108, 122, 127) THEN 10 WHEN c.system_type_id IN /* real/float */ (59, 62) THEN 2 END) AS NUMERIC_PRECISION_RADIX, convert(int, CASE WHEN c.system_type_id IN /* datetime/smalldatetime */ (58, 61) THEN NULL ELSE odbcscale(c.system_type_id, c.scale) END) AS NUMERIC_SCALE, convert(smallint, CASE WHEN c.system_type_id IN /* datetime/smalldatetime */ (58, 61) THEN 3 END) AS DATETIME_PRECISION, convert(sysname, null) AS CHARACTER_SET_CATALOG, convert(sysname, null) AS CHARACTER_SET_SCHEMA, convert(sysname, CASE WHEN c.system_type_id IN /* char/varchar/text */ (35, 167, 175) THEN CollationProperty(c.collation_name, 'sqlcharsetname') WHEN c.system_type_id IN /* nchar/nvarchar/ntext */ (99, 231, 239) THEN N'UNICODE' END) AS CHARACTER_SET_NAME, convert(sysname, null) AS COLLATION_CATALOG FROM sys.table_types tt join sys.objects o on o.object_id = tt.type_table_object_id JOIN sys.schemas sc on sc.schema_id = tt.schema_id JOIN sys.columns c ON c.object_id = o.object_id LEFT JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE o.type IN ('TT') AND (DB_NAME() = @Catalog or (@Catalog is null)) and (sc.name = @Owner or (@Owner is null)) and (tt.name = @Type or (@Type is null)) and (c.name = @Member or (@Member is null)) order by sc.name, tt.name, c.name",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Type", "@Type"),
                     new Restriction(4, "Member", "@Member")]),
            new SqlCommandCollection("Views",                  3, 3, "08.00.000.0", null, "select TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, CHECK_OPTION, IS_UPDATABLE from INFORMATION_SCHEMA.VIEWS where (TABLE_CATALOG = @Catalog or (@Catalog is null)) and (TABLE_SCHEMA = @Owner or (@Owner is null)) and (TABLE_NAME = @Table or (@Table is null)) order by TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table")]),
            new SqlCommandCollection("ViewColumns",            4, 4, "08.00.000.0", null, "select VIEW_CATALOG, VIEW_SCHEMA, VIEW_NAME, TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME from INFORMATION_SCHEMA.VIEW_COLUMN_USAGE where (VIEW_CATALOG = @Catalog or (@Catalog is null)) and (VIEW_SCHEMA = @Owner or (@Owner is null)) and (VIEW_NAME = @Table or (@Table is null)) and (COLUMN_NAME = @Column or (@Column is null)) order by VIEW_CATALOG, VIEW_SCHEMA, VIEW_NAME",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Column", "@Column")]),
            new SqlCommandCollection("ProcedureParameters",    4, 1, "08.00.0000", null,  "select SPECIFIC_CATALOG, SPECIFIC_SCHEMA, SPECIFIC_NAME, ORDINAL_POSITION, PARAMETER_MODE, IS_RESULT, AS_LOCATOR, PARAMETER_NAME, CASE WHEN DATA_TYPE IS NULL THEN USER_DEFINED_TYPE_NAME WHEN DATA_TYPE = 'table type' THEN USER_DEFINED_TYPE_NAME ELSE DATA_TYPE END as DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, CHARACTER_OCTET_LENGTH, COLLATION_CATALOG, COLLATION_SCHEMA, COLLATION_NAME, CHARACTER_SET_CATALOG, CHARACTER_SET_SCHEMA, CHARACTER_SET_NAME, NUMERIC_PRECISION, NUMERIC_PRECISION_RADIX, NUMERIC_SCALE, DATETIME_PRECISION, INTERVAL_TYPE, INTERVAL_PRECISION from INFORMATION_SCHEMA.PARAMETERS where (SPECIFIC_CATALOG = @Catalog or (@Catalog is null)) and (SPECIFIC_SCHEMA = @Owner or (@Owner is null)) and (SPECIFIC_NAME = @Name or (@Name is null)) and (PARAMETER_NAME = @Parameter or (@Parameter is null)) order by SPECIFIC_CATALOG, SPECIFIC_SCHEMA, SPECIFIC_NAME, PARAMETER_NAME",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Name", "@Name"),
                     new Restriction(4, "Parameter", "@Parameter")]),
            new SqlCommandCollection("Procedures",             4, 3, "08.00.0000", null,  "select SPECIFIC_CATALOG, SPECIFIC_SCHEMA, SPECIFIC_NAME, ROUTINE_CATALOG, ROUTINE_SCHEMA, ROUTINE_NAME, ROUTINE_TYPE, CREATED, LAST_ALTERED from INFORMATION_SCHEMA.ROUTINES where (SPECIFIC_CATALOG = @Catalog or (@Catalog is null)) and (SPECIFIC_SCHEMA = @Owner or (@Owner is null)) and (SPECIFIC_NAME = @Name or (@Name is null)) and (ROUTINE_TYPE = @Type or (@Type is null)) order by SPECIFIC_CATALOG, SPECIFIC_SCHEMA, SPECIFIC_NAME",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Name", "@Name"),
                     new Restriction(4, "Type", "@Type")]),
            new SqlCommandCollection("IndexColumns",           5, 4, null, "09.99.9999",  "select distinct db_Name() as constraint_catalog, constraint_schema = user_name(o.uid), constraint_name = x.name, table_catalog  = db_name(), table_schema = user_name(o.uid), table_name = o.name, column_name = c.name, ordinal_position = convert(int, xk.keyno), KeyType  = c.xtype, index_name = x.name from sysobjects o, sysindexes x, syscolumns c, sysindexkeys xk where o.type in ('U') and x.id = o.id  and o.id = c.id and o.id = xk.id and x.indid = xk.indid and c.colid = xk.colid and xk.keyno < = x.keycnt and permissions(o.id, c.name) <> 0  and (db_name() = @Catalog or (@Catalog is null)) and (user_name()= @Owner or (@Owner is null)) and (o.name = @Table or (@Table is null)) and (x.name = @ConstraintName or (@ConstraintName is null)) and (c.name = @Column or (@Column is null)) order by table_name, index_name",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "ConstraintName", "@ConstraintName"),
                     new Restriction(5, "Column", "@Column")]),
            new SqlCommandCollection("IndexColumns",           5, 4, "10.00.0000", null,  "EXEC sys.sp_indexcolumns_managed @Catalog, @Owner, @Table, @ConstraintName, @Column",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "ConstraintName", "@ConstraintName"),
                     new Restriction(5, "Column", "@Column")]),
            new SqlCommandCollection("Indexes",                4, 3, null, "09.99.9999",  "select distinct db_Name() as constraint_catalog, constraint_schema = user_name(o.uid), constraint_name = x.name, table_catalog  = db_name(), table_schema = user_name(o.uid), table_name = o.name, index_name = x.name from sysobjects o, sysindexes x, sysindexkeys xk where o.type in ('U') and x.id = o.id  and o.id = xk.id and x.indid = xk.indid and xk.keyno < = x.keycnt and (db_name() = @Catalog or (@Catalog is null)) and (user_name()= @Owner or (@Owner is null)) and (o.name = @Table or (@Table is null)) and (x.name = @Name or (@Name is null)) order by table_name, index_name",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Name", "@Name")]),
            new SqlCommandCollection("Indexes",                4, 3, "10.00.0000", null,  "EXEC sys.sp_indexes_managed @Catalog, @Owner, @Table, @Name",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Name", "@Name")]),
            new SqlCommandCollection("UserDefinedTypes",       2, 1, "09.00.0000", null,  "select assemblies.name as assembly_name, types.assembly_class as udt_name, ASSEMBLYPROPERTY(assemblies.name, 'VersionMajor') as version_major, ASSEMBLYPROPERTY(assemblies.name, 'VersionMinor') as version_minor, ASSEMBLYPROPERTY(assemblies.name, 'VersionBuild') as version_build, ASSEMBLYPROPERTY(assemblies.name, 'VersionRevision') as version_revision, ASSEMBLYPROPERTY(assemblies.name, 'CultureInfo') as culture_info, ASSEMBLYPROPERTY(assemblies.name, 'PublicKey') as public_key, is_fixed_length, max_length, Create_Date, Permission_set_desc from sys.assemblies as assemblies  join sys.assembly_types as types on assemblies.assembly_id = types.assembly_id where (assemblies.name = @AssemblyName or (@AssemblyName is null)) and (types.assembly_class = @UDTName or (@UDTName is null))",
                    [new Restriction(1, "assembly_name", "@AssemblyName"),
                     new Restriction(2, "udt_name", "@UDTName")]),
            new SqlCommandCollection("ForeignKeys",            4, 3, null,         null, "select CONSTRAINT_CATALOG, CONSTRAINT_SCHEMA, CONSTRAINT_NAME, TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, CONSTRAINT_TYPE, IS_DEFERRABLE, INITIALLY_DEFERRED from INFORMATION_SCHEMA.TABLE_CONSTRAINTS where (CONSTRAINT_CATALOG = @Catalog or (@Catalog is null)) and (CONSTRAINT_SCHEMA = @Owner or (@Owner is null)) and (TABLE_NAME = @Table or (@Table is null)) and (CONSTRAINT_NAME = @Name or (@Name is null)) and CONSTRAINT_TYPE = 'FOREIGN KEY' order by CONSTRAINT_CATALOG, CONSTRAINT_SCHEMA, CONSTRAINT_NAME",
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Name", "@Name")]),
            new SqlCommandCollection("TVPs",                   0, 0, "10.00.0000", null, @"select name TypeName, 30 ProviderDbType, max_length ColumnSize, null CreateFormat, null CreateParameters, null DataType, null IsAutoincrementable, null IsBestMatch, null IsCaseSensitive, null IsFixedLength, null IsFixedPrecisionScale, null IsLong, is_nullable IsNullable, 0 IsSearchable, null IsSearchableWithLike, null IsUnsigned, null MaximumScale, null MinimumScale, null IsConcurrencyType, 0 IsLiteralSupported, null LiteralPrefix, null LiteralSuffix, null NativeDataType from sys.types  where is_table_type = 1", null),
            new SqlCommandCollection("UDTs",                   0, 0, "09.00.0000", null, @"select types.assembly_class COLLATE database_default + ', ' + assemblies.name + ', Version=' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'VersionMajor')) + '.' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'VersionMinor')) + '.' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'VersionBuild')) + '.' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'VersionRevision')) + ISNULL(', Culture=' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'CultureInfo')),'') + ISNULL(', PublicKeyToken=' + LOWER(REPLACE(CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'PublicKey'),1),'0x','')),'') TypeName, 29 ProviderDbType, max_length ColumnSize, null CreateFormat, null CreateParameters, null DataType, null IsAutoincrementable, null IsBestMatch, null IsCaseSensitive, is_fixed_length IsFixedLength, null IsFixedPrecisionScale, null IsLong, is_nullable IsNullable, 1 IsSearchable, null IsSearchableWithLike, null IsUnsigned, null MaximumScale, null MinimumScale, null IsConcurrencyType, 0 IsLiteralSupported, null LiteralPrefix, null LiteralSuffix, null NativeDataType from sys.assemblies as assemblies  join sys.assembly_types as types on assemblies.assembly_id = types.assembly_id", null),
        ];
    #pragma warning restore format


        // Well-known column names
        private const string MaximumVersionKey = "MaximumVersion";
        private const string MinimumVersionKey = "MinimumVersion";
        private const string RestrictionDefaultKey = "RestrictionDefault";
        private const string RestrictionNumberKey = "RestrictionNumber";
        private const string RestrictionNameKey = "RestrictionName";
        private const string ParameterNameKey = "ParameterName";

        private static readonly HashSet<int> s_assemblyPropertyUnsupportedEngines = new() { 6, 9, 11 };

        private readonly string _serverVersion;

        public SqlMetaDataFactory(string serverVersion)
        {
            ADP.CheckArgumentNull(serverVersion, nameof(serverVersion));

            _serverVersion = serverVersion;
        }

        public DataTable GetSchema(DbConnection connection, string collectionName, string[] restrictions) =>
            GetSchemaCore(connection, collectionName, restrictions, isAsync: false, default).Result;

        public async Task<DataTable> GetSchemaAsync(DbConnection connection, string collectionName, string[] restrictions, CancellationToken cancellationToken) =>
            await GetSchemaCore(connection, collectionName, restrictions, isAsync: true, cancellationToken).ConfigureAwait(false);

        public async ValueTask<DataTable> GetSchemaCore(DbConnection connection, string collectionName, string[] restrictions, bool isAsync, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MetaDataCollection metadataRoot = s_metaDataCollection[0] as MetaDataCollection;
            // We expect first element of s_metaDataCollection to be an instance of MetaDataCollection
            Debug.Assert(metadataRoot != null);
            DataTable schema = await metadataRoot.GetMetadata(collectionName, new MetaDataContext(_serverVersion, restrictions, connection, isAsync, cancellationToken));

            return schema;
        }






               
        internal sealed class MetaDataContext
        {
            public readonly string ServerVersion;
            public readonly string[] RestrictionValues;
            public readonly DbConnection Connection;
            public readonly bool IsAsync = false;
            public readonly CancellationToken CancellationToken;

            internal MetaDataContext(string serverVersion, string[] restrictions, DbConnection connection, bool isAsync, CancellationToken cancellationToken)
            {
                ServerVersion = serverVersion;
                RestrictionValues = restrictions;
                Connection = connection;
                IsAsync = isAsync;
                CancellationToken = cancellationToken;
            }
        }

        internal sealed class Restriction
        {
            public readonly string RestrictionName;
            public readonly string ParameterName;
            public readonly int RestrictionNumber;

            internal Restriction(int restrictionNumber, string restrictionName, string parameterName)
            {
                RestrictionName = restrictionName;
                ParameterName = parameterName;
                RestrictionNumber = restrictionNumber;
            }
        }

        internal abstract class MetaDataCollectionBase
        {
            public readonly string CollectionName;
            public readonly int NumberOfRestrictions;
            public readonly int NumberOfIdentifierParts;
            private readonly string _minimumVersion;
            private readonly string _maximumVersion;

            internal MetaDataCollectionBase(string collectionName, int numberOfRestrictions, int numberOfIdentifierParts, string minimumVersion = null, string maximumVersion = null)
            {
                CollectionName = collectionName;
                NumberOfRestrictions = numberOfRestrictions;
                NumberOfIdentifierParts = numberOfIdentifierParts;
                _minimumVersion = minimumVersion;
                _maximumVersion = maximumVersion;
            }

            public abstract ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable accumulator = null);

            public bool SupportedByCurrentVersion(string serverVersion) =>
                (_minimumVersion == null || string.Compare(serverVersion, _minimumVersion, StringComparison.OrdinalIgnoreCase) >= 0) &&
                (_maximumVersion == null || string.Compare(serverVersion, _maximumVersion, StringComparison.OrdinalIgnoreCase) <= 0);

            protected MetaDataCollectionBase FindMetaDataCollection(string collectionName, string serverVersion)
            {
                bool versionFailure = false;
                bool haveExactMatch = false;
                bool haveMultipleInexactMatches = false;
                string exactCollectionName = null;
                MetaDataCollectionBase requestedCollection = null;

                foreach (MetaDataCollectionBase metaData in s_metaDataCollection)
                {
                    if (string.Equals(metaData.CollectionName, collectionName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!metaData.SupportedByCurrentVersion(serverVersion))
                        {
                            versionFailure = true;
                        }
                        else if (collectionName == metaData.CollectionName)
                        {
                            if (haveExactMatch)
                            {
                                throw ADP.CollectionNameIsNotUnique(collectionName);
                            }
                            requestedCollection = metaData;
                            exactCollectionName = metaData.CollectionName;
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
                            requestedCollection = metaData;
                            exactCollectionName = metaData.CollectionName;
                        }
                    }
                }

                if (requestedCollection is null)
                {
                    if (!versionFailure)
                    {
                        throw ADP.UndefinedCollection(collectionName);
                    }
                    else
                    {
                        //throw ADP.UnsupportedVersion(collectionName);
                    }
                }

                if (!haveExactMatch && haveMultipleInexactMatches)
                {
                    throw ADP.AmbiguousCollectionName(collectionName);
                }

                return requestedCollection;
            }
        }
    }
}
