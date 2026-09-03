// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

public class TdsTypeInfoTests
{
    [Fact]
    public void Clone_CopiesAllUnclonedProperties()
    {
        // Arrange
        var original = new TdsTypeInfo
        {
            CodePage = 1251,
            DbType = SqlDbType.Decimal,
            Encoding = Encoding.Unicode,
            TdsType = 0x6A,
            IsEncrypted = true
        };
        original.length = 17;
        original.precision = 28;
        original.scale = 8;
        original.collation = new SqlCollation(0x00, 0x00);

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Equal(original.CodePage, clone.CodePage);
        Assert.Equal(original.DbType, clone.DbType);
        Assert.Equal(original.Encoding, clone.Encoding);
        Assert.Equal(original.TdsType, clone.TdsType);
        Assert.Equal(original.IsEncrypted, clone.IsEncrypted);
        Assert.Equal(original.length, clone.length);
        Assert.Equal(original.precision, clone.precision);
        Assert.Equal(original.scale, clone.scale);
        Assert.Equal(original.collation, clone.collation);
    }

    [Fact]
    public void Clone_WithUdtTypeInfo_CreatesDeepCopy()
    {
        // Arrange
        var original = new TdsTypeInfo
        {
            TdsType = 0xF0,
            UdtTypeInfo = new TdsUdtTypeInfo()
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.NotNull(clone.UdtTypeInfo);
        Assert.NotSame(original.UdtTypeInfo, clone.UdtTypeInfo);
    }

    [Fact]
    public void Clone_WithXmlTypeInfo_CreatesDeepCopy()
    {
        // Arrange
        var original = new TdsTypeInfo
        {
            TdsType = 0xF1,
            XmlTypeInfo = new TdsXmlTypeInfo()
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.NotNull(clone.XmlTypeInfo);
        Assert.NotSame(original.XmlTypeInfo, clone.XmlTypeInfo);
    }
}
