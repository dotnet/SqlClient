// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

public class TdsUdtTypeInfoTests
{
    [Fact]
    public void Constructor_InitializesPropertiesWithDefaultValues()
    {
        // Arrange & Act
        var udtTypeInfo = new TdsUdtTypeInfo();

        // Assert
        Assert.Null(udtTypeInfo.AssemblyQualifiedName);
        Assert.Null(udtTypeInfo.DatabaseName);
        Assert.Null(udtTypeInfo.SchemaName);
        Assert.Null(udtTypeInfo.TypeName);
        Assert.Null(udtTypeInfo.Type);
    }

    [Fact]
    public void Clone_CopiesAllProperties()
    {
        // Arrange
        var original = new TdsUdtTypeInfo
        {
            AssemblyQualifiedName = "MyAssembly.MyType, MyAssembly, Version=1.0.0.0",
            DatabaseName = "TestDatabase",
            SchemaName = "dbo",
            TypeName = "MyUdtType",
            Type = typeof(string)
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Equal(original.AssemblyQualifiedName, clone.AssemblyQualifiedName);
        Assert.Equal(original.DatabaseName, clone.DatabaseName);
        Assert.Equal(original.SchemaName, clone.SchemaName);
        Assert.Equal(original.TypeName, clone.TypeName);
        Assert.Equal(original.Type, clone.Type);
    }

    [Fact]
    public void Type_CanBeSetAndRetrieved()
    {
        // Arrange
        var udtTypeInfo = new TdsUdtTypeInfo();
        var testType = typeof(DateTime);

        // Act
        udtTypeInfo.Type = testType;

        // Assert
        Assert.Equal(testType, udtTypeInfo.Type);
    }
}
