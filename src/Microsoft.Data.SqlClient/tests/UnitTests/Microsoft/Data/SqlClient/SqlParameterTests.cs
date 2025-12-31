// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    public class SqlParameterTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_Parameterless()
        {
            // Act
            SqlParameter p = new SqlParameter();

            // Assert
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal(string.Empty, p.ParameterName);
            Assert.Equal(0, p.Precision);
            Assert.Equal(0, p.Scale);
            Assert.Equal(0, p.Size);
            Assert.Equal(string.Empty, p.SourceColumn);
            Assert.False(p.SourceColumnNullMapping);
            Assert.Equal(DataRowVersion.Current, p.SourceVersion);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Null(p.SqlValue);
            Assert.Equal(string.Empty, p.TypeName);
            Assert.Equal(string.Empty, p.UdtTypeName);
            Assert.Null(p.Value);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionOwningSchema);
        }

        public static TheoryData<SqlDbType, DbType> Constructor_NameType_Data =>
            new TheoryData<SqlDbType, DbType>
            {
                { SqlDbType.BigInt, DbType.Int64 },
                { SqlDbType.Binary, DbType.Binary },
                { SqlDbType.Bit, DbType.Boolean },
                { SqlDbType.Char, DbType.AnsiStringFixedLength },
                { SqlDbType.Date, DbType.Date },
                { SqlDbType.DateTime, DbType.DateTime },
                { SqlDbType.DateTimeOffset, DbType.DateTimeOffset },
                { SqlDbType.Decimal, DbType.Decimal },
                { SqlDbType.Float, DbType.Double },
                { SqlDbType.Image, DbType.Binary },
                { SqlDbType.Int, DbType.Int32 },
                { SqlDbType.Money, DbType.Currency },
                { SqlDbType.NChar, DbType.StringFixedLength },
                { SqlDbType.NText, DbType.String },
                { SqlDbType.NVarChar, DbType.String },
                { SqlDbType.Real, DbType.Single },
                { SqlDbType.SmallDateTime, DbType.DateTime },
                { SqlDbType.SmallInt, DbType.Int16 },
                { SqlDbType.SmallMoney, DbType.Currency },
                { SqlDbType.Structured, DbType.Object },
                { SqlDbType.Text, DbType.AnsiString },
                { SqlDbType.Time, DbType.Time },
                { SqlDbType.Timestamp, DbType.Binary },
                { SqlDbType.TinyInt, DbType.Byte },
                { SqlDbType.Udt, DbType.Object },
                { SqlDbType.UniqueIdentifier, DbType.Guid },
                { SqlDbType.VarBinary, DbType.Binary },
                { SqlDbType.VarChar, DbType.AnsiString },
                { SqlDbType.Variant, DbType.Object },
                { SqlDbType.Xml, DbType.Xml }
            };

        [Theory]
        #if NETFRAMEWORK
        // netfx stores system enums in the GAC, and xUnit cannot serialize enums that live there.
        // Thus, we disable enumeration of these to avoid a bunch of warnings in the console.
        [MemberData(nameof(Constructor_NameType_Data), DisableDiscoveryEnumeration = true)]
        #else
        [MemberData(nameof(Constructor_NameType_Data))]
        #endif
        public void Constructor_NameType(SqlDbType sqlDbType, DbType dbType)
        {
            // Act
            SqlParameter p = new SqlParameter("types", sqlDbType);

            // Assert
            // - Theoretically parameterless tests should validate "default" behavior
            // - Validate that type-specific values are set
            Assert.Equal(dbType, p.DbType);
            Assert.Equal(sqlDbType, p.SqlDbType);
            Assert.Null(p.Value);
        }

        #endregion
    }
}
