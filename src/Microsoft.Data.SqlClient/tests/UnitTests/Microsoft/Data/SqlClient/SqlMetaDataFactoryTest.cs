using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public sealed class SqlMetaDataFactoryTest: IClassFixture<TdsServerFixture>
    {
        private readonly string _connectionString;
        public SqlMetaDataFactoryTest(TdsServerFixture fixture)
        {
            _connectionString = new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{fixture.TdsServer.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false, // No pooling needed; avoids leaking a pooled connection to this ephemeral port
            }.ConnectionString;
        }

        [Fact]
        public void ReservedWords_Values()
        {
            string[] expected = [
 "ADD", "EXCEPT", "PERCENT", "ALL", "EXEC", "PLAN", "ALTER", "EXECUTE", "PRECISION", "AND", "EXISTS", "PRIMARY", "ANY", "EXIT",
 "PRINT", "AS", "FETCH", "PROC", "ASC", "FILE", "PROCEDURE", "AUTHORIZATION", "FILLFACTOR", "PUBLIC", "BACKUP", "FOR", "RAISERROR",
 "BEGIN", "FOREIGN", "READ", "BETWEEN", "FREETEXT", "READTEXT", "BREAK", "FREETEXTTABLE", "RECONFIGURE", "BROWSE", "FROM",
 "REFERENCES", "BULK", "FULL", "REPLICATION", "BY", "FUNCTION", "RESTORE", "CASCADE", "GOTO", "RESTRICT", "CASE", "GRANT", "RETURN",
 "CHECK", "GROUP", "REVOKE", "CHECKPOINT", "HAVING", "RIGHT", "CLOSE", "HOLDLOCK", "ROLLBACK", "CLUSTERED", "IDENTITY", "ROWCOUNT",
 "COALESCE", "IDENTITY_INSERT", "ROWGUIDCOL", "COLLATE", "IDENTITYCOL", "RULE", "COLUMN", "IF", "SAVE", "COMMIT", "IN", "SCHEMA",
 "COMPUTE", "INDEX", "SELECT", "CONSTRAINT", "INNER", "SESSION_USER", "CONTAINS", "INSERT", "SET", "CONTAINSTABLE", "INTERSECT",
 "SETUSER", "CONTINUE", "INTO", "SHUTDOWN", "CONVERT", "IS", "SOME", "CREATE", "JOIN", "STATISTICS", "CROSS", "KEY", "SYSTEM_USER",
 "CURRENT", "KILL", "TABLE", "CURRENT_DATE", "LEFT", "TEXTSIZE", "CURRENT_TIME", "LIKE", "THEN", "CURRENT_TIMESTAMP", "LINENO", "TO",
 "CURRENT_USER", "LOAD", "TOP", "CURSOR", "NATIONAL", "TRAN", "DATABASE", "NOCHECK", "TRANSACTION", "DBCC", "NONCLUSTERED",
 "TRIGGER", "DEALLOCATE", "NOT", "TRUNCATE", "DECLARE", "NULL", "TSEQUAL", "DEFAULT", "NULLIF", "UNION", "DELETE", "OF", "UNIQUE",
 "DENY", "OFF", "UPDATE", "DESC", "OFFSETS", "UPDATETEXT", "DISK", "ON", "USE", "DISTINCT", "OPEN", "USER", "DISTRIBUTED",
 "OPENDATASOURCE", "VALUES", "DOUBLE", "OPENQUERY", "VARYING", "DROP", "OPENROWSET", "VIEW", "DUMMY", "OPENXML", "WAITFOR", "DUMP",
 "OPTION", "WHEN", "ELSE", "OR", "WHERE", "END", "ORDER", "WHILE", "ERRLVL", "OUTER", "WITH", "ESCAPE", "OVER", "WRITETEXT",
 "ABSOLUTE", "FOUND", "PRESERVE", "ACTION", "FREE", "PRIOR", "ADMIN", "GENERAL", "PRIVILEGES", "AFTER", "GET", "READS", "AGGREGATE",
 "GLOBAL", "REAL", "ALIAS", "GO", "RECURSIVE", "ALLOCATE", "GROUPING", "REF", "ARE", "HOST", "REFERENCING", "ARRAY", "HOUR",
 "RELATIVE", "ASSERTION", "IGNORE", "RESULT", "AT", "IMMEDIATE", "RETURNS", "BEFORE", "INDICATOR", "ROLE", "BINARY", "INITIALIZE",
 "ROLLUP", "BIT", "INITIALLY", "ROUTINE", "BLOB", "INOUT", "ROW", "BOOLEAN", "INPUT", "ROWS", "BOTH", "INT", "SAVEPOINT", "BREADTH",
 "INTEGER", "SCROLL", "CALL", "INTERVAL", "SCOPE", "CASCADED", "ISOLATION", "SEARCH", "CAST", "ITERATE", "SECOND", "CATALOG",
 "LANGUAGE", "SECTION", "CHAR", "LARGE", "SEQUENCE", "CHARACTER", "LAST", "SESSION", "CLASS", "LATERAL", "SETS", "CLOB", "LEADING",
 "SIZE", "COLLATION", "LESS", "SMALLINT", "COMPLETION", "LEVEL", "SPACE", "CONNECT", "LIMIT", "SPECIFIC", "CONNECTION", "LOCAL",
 "SPECIFICTYPE", "CONSTRAINTS", "LOCALTIME", "SQL", "CONSTRUCTOR", "LOCALTIMESTAMP", "SQLEXCEPTION", "CORRESPONDING", "LOCATOR",
 "SQLSTATE", "CUBE", "MAP", "SQLWARNING", "CURRENT_PATH", "MATCH", "START", "CURRENT_ROLE", "MINUTE", "STATE", "CYCLE", "MODIFIES",
 "STATEMENT", "DATA", "MODIFY", "STATIC", "DATE", "MODULE", "STRUCTURE", "DAY", "MONTH", "TEMPORARY", "DEC", "NAMES", "TERMINATE",
 "DECIMAL", "NATURAL", "THAN", "DEFERRABLE", "NCHAR", "TIME", "DEFERRED", "NCLOB", "TIMESTAMP", "DEPTH", "NEW", "TIMEZONE_HOUR",
 "DEREF", "NEXT", "TIMEZONE_MINUTE", "DESCRIBE", "NO", "TRAILING", "DESCRIPTOR", "NONE", "TRANSLATION", "DESTROY", "NUMERIC",
 "TREAT", "DESTRUCTOR", "OBJECT", "TRUE", "DETERMINISTIC", "OLD", "UNDER", "DICTIONARY", "ONLY", "UNKNOWN", "DIAGNOSTICS",
 "OPERATION", "UNNEST", "DISCONNECT", "ORDINALITY", "USAGE", "DOMAIN", "OUT", "USING", "DYNAMIC", "OUTPUT", "VALUE", "EACH", "PAD",
 "VARCHAR", "END-EXEC", "PARAMETER", "VARIABLE", "EQUALS", "PARAMETERS", "WHENEVER", "EVERY", "PARTIAL", "WITHOUT", "EXCEPTION",
 "PATH", "WORK", "EXTERNAL", "POSTFIX", "WRITE", "FALSE", "PREFIX", "YEAR", "FIRST", "PREORDER", "ZONE", "FLOAT", "PREPARE", "ADA",
 "AVG", "BIT_LENGTH", "CHAR_LENGTH", "CHARACTER_LENGTH", "COUNT", "EXTRACT", "FORTRAN", "INCLUDE", "INSENSITIVE", "LOWER", "MAX",
 "MIN", "OCTET_LENGTH", "OVERLAPS", "PASCAL", "POSITION", "SQLCA", "SQLCODE", "SQLERROR", "SUBSTRING", "SUM", "TRANSLATE", "TRIM", "UPPER",
                ];

            using SqlConnection connection = new(_connectionString);
            connection.Open();

            DataTable dataTypes = connection.GetSchema(DbMetaDataCollectionNames.ReservedWords);
            var actual = new HashSet<string>();
            foreach (DataRow row in dataTypes.Rows)
            {
                Assert.False(row.IsNull(0));
                Assert.IsType<string>(row[0]);
                Assert.Equal(row[0] as string, ((string)row[0]).ToUpper()); // Test for uppercase
                actual.Add((string)row[0]);
            }

            IEnumerable<string> absentReserverdWords = expected.Except(actual);
            IEnumerable<string> unexpectedReservedWords = actual.Except(expected);
            Assert.Empty(absentReserverdWords);
            Assert.Empty(unexpectedReservedWords);
        }

        [Fact]
        public void DataSourceInformation_Values()
        {
            using SqlConnection connection = new(_connectionString);
            connection.Open();

            DataTable dataTypes = connection.GetSchema(DbMetaDataCollectionNames.DataSourceInformation);
            Assert.Single(dataTypes.Rows);

            DataRow row = dataTypes.Rows[0];
            Assert.True(dataTypes.Columns.Contains("CompositeIdentifierSeparatorPattern"));
            Assert.NotNull(row["CompositeIdentifierSeparatorPattern"]);
            Assert.True(dataTypes.Columns.Contains("DataSourceProductName"));
            Assert.NotNull(row["DataSourceProductName"]);
            Assert.True(dataTypes.Columns.Contains("DataSourceProductVersion"));
            Assert.Matches(@"^\d+\.\d+\.\d+$", (string)row["DataSourceProductVersion"]);
            Assert.True(dataTypes.Columns.Contains("DataSourceProductVersionNormalized"));
            Assert.Matches(@"^\d+\.\d+\.\d+$", (string)row["DataSourceProductVersionNormalized"]);
            Assert.True(dataTypes.Columns.Contains("GroupByBehavior"));
            Assert.Equal(typeof(GroupByBehavior), dataTypes.Columns["GroupByBehavior"]!.DataType);
            Assert.IsType<int>(row["GroupByBehavior"]);
            Assert.True(dataTypes.Columns.Contains("IdentifierPattern"));
            Assert.NotNull(row["IdentifierPattern"]);
            Assert.True(dataTypes.Columns.Contains("IdentifierCase"));
            Assert.Equal(typeof(IdentifierCase), dataTypes.Columns["IdentifierCase"]!.DataType);
            Assert.IsType<int>(row["IdentifierCase"]);
            Assert.True(dataTypes.Columns.Contains("OrderByColumnsInSelect"));
            Assert.IsType<bool>(row["OrderByColumnsInSelect"]);
            Assert.True(dataTypes.Columns.Contains("ParameterMarkerFormat"));
            Assert.NotNull(row["ParameterMarkerFormat"]);
            Assert.True(dataTypes.Columns.Contains("ParameterMarkerPattern"));
            Assert.NotNull(row["ParameterMarkerPattern"]);
            Assert.True(dataTypes.Columns.Contains("ParameterNameMaxLength"));
            Assert.IsType<int>(row["ParameterNameMaxLength"]);
            Assert.InRange((int)row["ParameterNameMaxLength"], 0, short.MaxValue);
            Assert.True(dataTypes.Columns.Contains("ParameterNamePattern"));
            Assert.NotNull(row["ParameterNamePattern"]);
            Assert.True(dataTypes.Columns.Contains("QuotedIdentifierPattern"));
            Assert.NotNull(row["QuotedIdentifierPattern"]);
            Assert.True(dataTypes.Columns.Contains("QuotedIdentifierCase"));
            Assert.Equal(typeof(IdentifierCase), dataTypes.Columns["QuotedIdentifierCase"]!.DataType);
            Assert.IsType<int>(row["QuotedIdentifierCase"]);
            Assert.True(dataTypes.Columns.Contains("StatementSeparatorPattern"));
            Assert.NotNull(row["StatementSeparatorPattern"]);
            Assert.True(dataTypes.Columns.Contains("StringLiteralPattern"));
            Assert.NotNull(row["StringLiteralPattern"]);
            Assert.True(dataTypes.Columns.Contains("SupportedJoinOperators"));
            Assert.IsType<int>(row["SupportedJoinOperators"]);
        }

        [Theory]
        [InlineData("MetaDataCollections", 0, 0)]
        [InlineData("DataSourceInformation", 0, 0)]
        [InlineData("DataTypes", 0, 0)]
        [InlineData("Restrictions", 0, 0)]
        [InlineData("ReservedWords", 0, 0)]
        [InlineData("Users", 1, 1)]
        [InlineData("Databases", 1, 1)]
        [InlineData("Tables", 4, 3)]
        [InlineData("Columns", 4, 4)]
        [InlineData("AllColumns", 4, 4)]
        [InlineData("ColumnSetColumns", 3, 3)]
        [InlineData("StructuredTypeMembers", 4, 4)]
        [InlineData("Views", 3, 3)]
        [InlineData("ViewColumns", 4, 4)]
        [InlineData("ProcedureParameters", 4, 1)]
        [InlineData("Procedures", 4, 3)]
        [InlineData("ForeignKeys", 4, 3)]
        [InlineData("IndexColumns", 5, 4)]
        [InlineData("Indexes", 4, 3)]
        [InlineData("UserDefinedTypes", 2, 1)]
        public void MetaDataCollections_Values(string collectionName, int numberOfRestrictions, int numberOfIdentifierParts)
        {
            using SqlConnection connection = new(_connectionString);
            connection.Open();

            DataTable dataTypes = connection.GetSchema(DbMetaDataCollectionNames.MetaDataCollections);
            DataRow[] rows = dataTypes.Select($"CollectionName = '{collectionName}'");
            Assert.Single(rows);

            DataRow row = rows[0];
            Assert.Equal(numberOfRestrictions, row["NumberOfRestrictions"]);
            Assert.Equal(numberOfIdentifierParts, row["NumberOfIdentifierParts"]);
        }

        [Theory]
        [InlineData("Users", "User_Name", "@Name", "name", 1)]
        [InlineData("Databases", "Name", "@Name", "Name", 1)]
        [InlineData("Tables", "Catalog", "@Catalog", "TABLE_CATALOG", 1)]
        [InlineData("Tables", "Owner", "@Owner", "TABLE_SCHEMA", 2)]
        [InlineData("Tables", "Table", "@Name", "TABLE_NAME", 3)]
        [InlineData("Tables", "TableType", "@TableType", "TABLE_TYPE", 4)]
        [InlineData("Columns", "Catalog", "@Catalog", "TABLE_CATALOG", 1)]
        [InlineData("Columns", "Owner", "@Owner", "TABLE_SCHEMA", 2)]
        [InlineData("Columns", "Table", "@Table", "TABLE_NAME", 3)]
        [InlineData("Columns", "Column", "@Column", "COLUMN_NAME", 4)]
        [InlineData("AllColumns", "Catalog", "@Catalog", "TABLE_CATALOG", 1)]
        [InlineData("AllColumns", "Owner", "@Owner", "TABLE_SCHEMA", 2)]
        [InlineData("AllColumns", "Table", "@Table", "TABLE_NAME", 3)]
        [InlineData("AllColumns", "Column", "@Column", "COLUMN_NAME", 4)]
        [InlineData("ColumnSetColumns", "Catalog", "@Catalog", "TABLE_CATALOG", 1)]
        [InlineData("ColumnSetColumns", "Owner", "@Owner", "TABLE_SCHEMA", 2)]
        [InlineData("ColumnSetColumns", "Table", "@Table", "TABLE_NAME", 3)]
        [InlineData("StructuredTypeMembers", "Catalog", "@Catalog", "TYPE_CATALOG", 1)]
        [InlineData("StructuredTypeMembers", "Owner", "@Owner", "TYPE_SCHEMA", 2)]
        [InlineData("StructuredTypeMembers", "Type", "@Type", "TYPE_NAME", 3)]
        [InlineData("StructuredTypeMembers", "Member", "@Member", "MEMBER_NAME", 4)]
        [InlineData("Views", "Catalog", "@Catalog", "TABLE_CATALOG", 1)]
        [InlineData("Views", "Owner", "@Owner", "TABLE_SCHEMA", 2)]
        [InlineData("Views", "Table", "@Table", "TABLE_NAME", 3)]
        [InlineData("ViewColumns", "Catalog", "@Catalog", "VIEW_CATALOG", 1)]
        [InlineData("ViewColumns", "Owner", "@Owner", "VIEW_SCHEMA", 2)]
        [InlineData("ViewColumns", "Table", "@Table", "VIEW_NAME", 3)]
        [InlineData("ViewColumns", "Column", "@Column", "COLUMN_NAME", 4)]
        [InlineData("ProcedureParameters", "Catalog", "@Catalog", "SPECIFIC_CATALOG", 1)]
        [InlineData("ProcedureParameters", "Owner", "@Owner", "SPECIFIC_SCHEMA", 2)]
        [InlineData("ProcedureParameters", "Name", "@Name", "SPECIFIC_NAME", 3)]
        [InlineData("ProcedureParameters", "Parameter", "@Parameter", "PARAMETER_NAME", 4)]
        [InlineData("Procedures", "Catalog", "@Catalog", "SPECIFIC_CATALOG", 1)]
        [InlineData("Procedures", "Owner", "@Owner", "SPECIFIC_SCHEMA", 2)]
        [InlineData("Procedures", "Name", "@Name", "SPECIFIC_NAME", 3)]
        [InlineData("Procedures", "Type", "@Type", "ROUTINE_TYPE", 4)]
        [InlineData("IndexColumns", "Catalog", "@Catalog", "db_name()", 1)]
        [InlineData("IndexColumns", "Owner", "@Owner", "user_name()", 2)]
        [InlineData("IndexColumns", "Table", "@Table", "o.name", 3)]
        [InlineData("IndexColumns", "ConstraintName", "@ConstraintName", "x.name", 4)]
        [InlineData("IndexColumns", "Column", "@Column", "c.name", 5)]
        [InlineData("Indexes", "Catalog", "@Catalog", "db_name()", 1)]
        [InlineData("Indexes", "Owner", "@Owner", "user_name()", 2)]
        [InlineData("Indexes", "Table", "@Table", "o.name", 3)]
        [InlineData("Indexes", "Name", "@Name", "x.name", 4)]
        [InlineData("UserDefinedTypes", "assembly_name", "@AssemblyName", "assemblies.name", 1)]
        [InlineData("UserDefinedTypes", "udt_name", "@UDTName", "types.assembly_class", 2)]
        [InlineData("ForeignKeys", "Catalog", "@Catalog", "CONSTRAINT_CATALOG", 1)]
        [InlineData("ForeignKeys", "Owner", "@Owner", "CONSTRAINT_SCHEMA", 2)]
        [InlineData("ForeignKeys", "Table", "@Table", "TABLE_NAME", 3)]
        [InlineData("ForeignKeys", "Name", "@Name", "CONSTRAINT_NAME", 4)]
        public void Restrictions_Values(string collectionName, string restrictionName, string parameterName, string restrictioinDefault, int restrictionNumber)
        {
            using SqlConnection connection = new(_connectionString);
            connection.Open();

            DataTable dataTypes = connection.GetSchema(DbMetaDataCollectionNames.Restrictions);
            DataRow[] rows = dataTypes.Select($"CollectionName = '{collectionName}' AND RestrictionName = '{restrictionName}'");
            Assert.Single(rows);

            DataRow row = rows[0];
            Assert.Equal(parameterName, row["ParameterName"]);
            Assert.Equal(restrictioinDefault, row["RestrictionDefault"]);
            Assert.Equal(restrictionNumber, row["RestrictionNumber"]);
        }

        [Theory]
        [InlineData("smallint", SqlDbType.SmallInt, 5L, "smallint", null, "System.Int16", true, true, false, true, true, false, true, true, false, false, null, null, false, null, null, null)]
        [InlineData("int", SqlDbType.Int, 10L, "int", null, "System.Int32", true, true, false, true, true, false, true, true, false, false, null, null, false, null, null, null)]
        [InlineData("real", SqlDbType.Real, 7L, "real", null, "System.Single", false, true, false, true, false, false, true, true, false, false, null, null, false, null, null, null)]
        [InlineData("float", SqlDbType.Float, 53L, "float({0})", "number of bits used to store the mantissa", "System.Double", false, true, false, true, false, false, true, true, false, false, null, null, false, null, null, null)]
        [InlineData("money", SqlDbType.Money, 19L, "money", null, "System.Decimal", false, false, false, true, true, false, true, true, false, false, null, null, false, null, null, null)]
        [InlineData("smallmoney", SqlDbType.SmallMoney, 10L, "smallmoney", null, "System.Decimal", false, false, false, true, true, false, true, true, false, false, null, null, false, null, null, null)]
        [InlineData("bit", SqlDbType.Bit, 1L, "bit", null, "System.Boolean", false, false, false, true, false, false, true, true, false, null, null, null, false, null, null, null)]
        [InlineData("tinyint", SqlDbType.TinyInt, 3L, "tinyint", null, "System.Byte", true, true, false, true, true, false, true, true, false, true, null, null, false, null, null, null)]
        [InlineData("bigint", SqlDbType.BigInt, 19L, "bigint", null, "System.Int64", true, true, false, true, true, false, true, true, false, false, null, null, false, null, null, null)]
        [InlineData("timestamp", SqlDbType.Timestamp, 8L, "timestamp", null, "System.Byte[]", false, false, false, true, false, false, false, true, false, null, null, null, true, null, "0x", null)]
        [InlineData("binary", SqlDbType.Binary, 8000L, "binary({0})", "length", "System.Byte[]", false, true, false, true, false, false, true, true, false, null, null, null, false, null, "0x", null)]
        [InlineData("image", SqlDbType.Image, 2147483647L, "image", null, "System.Byte[]", false, true, false, false, false, true, true, false, false, null, null, null, false, null, "0x", null)]
        [InlineData("text", SqlDbType.Text, 2147483647L, "text", null, "System.String", false, true, false, false, false, true, true, false, true, null, null, null, false, null, "'", "'")]
        [InlineData("ntext", SqlDbType.NText, 1073741823L, "ntext", null, "System.String", false, true, false, false, false, true, true, false, true, null, null, null, false, null, "N'", "'")]
        [InlineData("decimal", SqlDbType.Decimal, 38L, "decimal({0}, {1})", "precision,scale", "System.Decimal", true, true, false, true, false, false, true, true, false, false, (short)38, (short)0, false, null, null, null)]
        [InlineData("numeric", SqlDbType.Decimal, 38L, "numeric({0}, {1})", "precision,scale", "System.Decimal", true, true, false, true, false, false, true, true, false, false, (short)38, (short)0, false, null, null, null)]
        [InlineData("datetime", SqlDbType.DateTime, 23L, "datetime", null, "System.DateTime", false, true, false, true, false, false, true, true, true, null, null, null, false, null, "{ts '", "'}")]
        [InlineData("smalldatetime", SqlDbType.SmallDateTime, 16L, "smalldatetime", null, "System.DateTime", false, true, false, true, false, false, true, true, true, null, null, null, false, null, "{ts '", "'}")]
        [InlineData("sql_variant", SqlDbType.Variant, null, "sql_variant", null, "System.Object", false, true, false, false, false, false, true, true, false, null, null, null, false, false, null, null)]
        [InlineData("xml", SqlDbType.Xml, 2147483647L, "xml", null, "System.String", false, false, false, false, false, true, true, false, false, null, null, null, false, false, null, null)]
        [InlineData("json", (SqlDbType)35, 2147483647L, "json", null, "System.String", false, false, false, false, false, true, true, false, false, null, null, null, false, null, "'", "'")]
        [InlineData("varchar", SqlDbType.VarChar, 2147483647L, "varchar({0})", "max length", "System.String", false, true, false, false, false, false, true, true, true, null, null, null, false, null, "'", "'")]
        [InlineData("char", SqlDbType.Char, 2147483647L, "char({0})", "length", "System.String", false, true, false, true, false, false, true, true, true, null, null, null, false, null, "'", "'")]
        [InlineData("nchar", SqlDbType.NChar, 1073741823L, "nchar({0})", "length", "System.String", false, true, false, true, false, false, true, true, true, null, null, null, false, null, "N'", "'")]
        [InlineData("nvarchar", SqlDbType.NVarChar, 1073741823L, "nvarchar({0})", "max length", "System.String", false, true, false, false, false, false, true, true, true, null, null, null, false, null, "N'", "'")]
        [InlineData("varbinary", SqlDbType.VarBinary, 1073741823L, "varbinary({0})", "max length", "System.Byte[]", false, true, false, false, false, false, true, true, false, null, null, null, false, null, "0x", null)]
        [InlineData("uniqueidentifier", SqlDbType.UniqueIdentifier, 16L, "uniqueidentifier", null, "System.Guid", false, true, false, true, false, false, true, true, false, null, null, null, false, null, "'", "'")]
        [InlineData("date", SqlDbType.Date, 3L, "date", null, "System.DateTime", false, false, false, true, true, false, true, true, true, null, null, null, false, null, "{ts '", "'}")]
        [InlineData("time", SqlDbType.Time, 5L, "time({0})", "scale", "System.TimeSpan", false, false, false, false, false, false, true, true, true, null, (short)7, (short)0, false, null, "{ts '", "'}")]
        [InlineData("datetime2", SqlDbType.DateTime2, 8L, "datetime2({0})", "scale", "System.DateTime", false, true, false, false, false, false, true, true, true, null, (short)7, (short)0, false, null, "{ts '", "'}")]
        [InlineData("datetimeoffset", SqlDbType.DateTimeOffset, 10L, "datetimeoffset({0})", "scale", "System.DateTimeOffset", false, true, false, false, false, false, true, true, true, null, (short)7, (short)0, false, null, "{ts '", "'}")]
        [InlineData("Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types", SqlDbType.Udt, 892L, null, null, null, null, null, null, false, null, null, true, true, null, null, null, null, null, false, null, null, Skip = "UDT types are not accessible in UnitTests")]
        [InlineData("Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types", SqlDbType.Udt, -1L, null, null, null, null, null, null, false, null, null, true, true, null, null, null, null, null, false, null, null, Skip = "UDT types are not accessible in UnitTests")]
        [InlineData("Microsoft.SqlServer.Types.SqlGeography, Microsoft.SqlServer.Types", SqlDbType.Udt, -1L, null, null, null, null, null, null, false, null, null, true, true, null, null, null, null, null, false, null, null, Skip = "UDT types are not accessible in UnitTests")]
        public void DataTypesTable_AllTypes(string typeName, SqlDbType providerDbType, long? columnSize, string? createFormat, string? createParameters,
                string? dataType, bool? isAutoIncrementable, bool? isBestMatch, bool? isCaseSensitive, bool? isFixedLength, bool? isFixedPrecisionScale,
                bool? isLong, bool? isNullable, bool? isSearchable, bool? isSearchableWithLike, bool? isUnsigned, short? maximumScale, short? minimumScale,
                bool? isConcurrencyType, bool? isLiteralSupported, string? literalPrefix, string? literalSuffix)
        {
            using SqlConnection connection = new(_connectionString);
            connection.Open();

            DataTable dataTypes = connection.GetSchema(DbMetaDataCollectionNames.DataTypes);
            DataRow[] dataTypeRows = dataTypes.Select($"(ProviderDbType = {SqlDbType.Udt:d} AND TypeName Like '{typeName}%') OR (TypeName = '{typeName}')");

            if ((typeName is "date" or "time" or "datetime2" or "datetimeoffset" && !connection.InnerConnection.Capabilities.ExpandedDateTimeDataTypes) ||
                (typeName is "xml" && !connection.InnerConnection.Capabilities.XmlDataType) ||
                (typeName is "json" && !connection.InnerConnection.Capabilities.JsonType))
            {
                Assert.Empty(dataTypeRows);
                return;
            }

            Assert.Single(dataTypeRows);

            DataRow dataTypeRow = dataTypeRows[0];
            if (providerDbType != SqlDbType.Udt)
            {
                AssertDataType(dataTypeRow, DbMetaDataColumnNames.TypeName, typeName);
            }
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.ProviderDbType, (int)providerDbType);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.ColumnSize, columnSize);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.CreateFormat, createFormat);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.CreateParameters, createParameters);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.DataType, dataType);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsAutoIncrementable, isAutoIncrementable);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsBestMatch, isBestMatch);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsCaseSensitive, isCaseSensitive);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsFixedLength, isFixedLength);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsFixedPrecisionScale, isFixedPrecisionScale);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsLong, isLong);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsNullable, isNullable);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsSearchable, isSearchable);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsSearchableWithLike, isSearchableWithLike);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsUnsigned, isUnsigned);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.MaximumScale, maximumScale);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.MinimumScale, minimumScale);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsConcurrencyType, isConcurrencyType);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.IsLiteralSupported, isLiteralSupported);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.LiteralPrefix, literalPrefix);
            AssertDataType(dataTypeRow, DbMetaDataColumnNames.LiteralSuffix, literalSuffix);

            static void AssertDataType(DataRow dataTypeRow, string columnName, object? expected)
            {
                if (expected is null)
                {
                    Assert.True(dataTypeRow.IsNull(columnName));
                }
                else
                {
                    Assert.Equal(expected, dataTypeRow[columnName]);
                }
            }
        }
    }
}
