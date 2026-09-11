// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

public class TdsXmlTypeInfoTests
{
    [Fact]
    public void Constructor_InitializesPropertiesWithDefaultValues()
    {
        // Arrange & Act
        var udtTypeInfo = new TdsXmlTypeInfo();

        // Assert
        Assert.Null(udtTypeInfo.Database);
        Assert.Null(udtTypeInfo.Name);
        Assert.Null(udtTypeInfo.OwningSchema);
    }

    [Fact]
    public void Clone_CopiesAllProperties()
    {
        // Arrange
        var original = new TdsXmlTypeInfo
        {
            Database = "TestDatabase",
            Name = "TestName",
            OwningSchema = "TestSchema"
        };

        // Act
        var cloned = original.Clone();

        // Assert
        Assert.Equal(original.Database, cloned.Database);
        Assert.Equal(original.Name, cloned.Name);
        Assert.Equal(original.OwningSchema, cloned.OwningSchema);
    }
}
