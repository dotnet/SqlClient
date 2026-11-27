// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Diagnostics;

/// <summary>
/// Verifies that opt-in query text masks literals and comments, and that text which cannot be
/// masked safely is withheld entirely instead of being partially exported.
/// </summary>
public class SqlStatementSanitizerTests
{
    /// <summary>Literals, numbers and comments are masked while structure is kept.</summary>
    [Theory]
    [InlineData("SELECT * FROM t WHERE a = 'secret' AND b = 42",
        "SELECT * FROM t WHERE a = ? AND b = ?")]
    [InlineData("select N'x''y' , 0x1F, 1.5e-3", "select ? , ?, ?")]
    [InlineData("select 1 -- secret\r\nfrom [my table]", "select ? from [my table]")]
    [InlineData("select /* a /* nested */ b */ col1 from t2", "select col1 from t2")]
    [InlineData("update t set c = \"x\" where id IN (1, 2)",
        "update t set c = ? where id IN (?, ?)")]
    public void Mask_MaskableText_ReturnsMaskedText(string sql, string expected)
    {
        // Act
        string masked = SqlStatementSanitizer.Mask(sql);

        // Assert
        Assert.Equal(expected, masked);
    }

    /// <summary>Text that could leak unmasked values or is unbounded yields null.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("EXEC sp_set_password 'secret'")]
    [InlineData("  execute dbo.p 1")]
    [InlineData("select 'unterminated")]
    [InlineData("select 1 /* unterminated")]
    [InlineData("select [unterminated")]
    public void Mask_UnsafeText_ReturnsNull(string sql)
    {
        // Act
        string masked = SqlStatementSanitizer.Mask(sql);

        // Assert
        Assert.Null(masked);
    }

    /// <summary>Input beyond the bounded length is withheld.</summary>
    [Fact]
    public void Mask_OversizeInput_ReturnsNull()
    {
        // Arrange
        string sql = "select " + new string('a', SqlStatementSanitizer.MaximumInputLength);

        // Act
        string masked = SqlStatementSanitizer.Mask(sql);

        // Assert
        Assert.Null(masked);
    }
}
