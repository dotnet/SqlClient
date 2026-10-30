// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Verifies ANSI metadata sizing for SqlParameter output directions.</summary>
public class SqlParameterTests
{
    /// <summary>
    /// Input/output values can promote metadata; output-only metadata keeps its capacity.
    /// </summary>
    [Theory]
    [InlineData(ParameterDirection.Output, 8000, 8000L)]
    [InlineData(ParameterDirection.InputOutput, 8000, -1L)]
    [InlineData(ParameterDirection.Output, -1, -1L)]
    [InlineData(ParameterDirection.InputOutput, -1, -1L)]
    public void GetMetadataForTypeInfo_MultibyteOutputValue_UsesExpectedByteCapacity(
        ParameterDirection direction, int size, long expectedLength)
    {
        // Arrange
        SqlParameter parameter = new("@p", SqlDbType.VarChar, size)
        {
            Direction = direction,
            Offset = 1,
            Value = "a" + new string('\u00e9', 4001)
        };
        TdsParser parser = new(false, false);
        typeof(TdsParser).GetField("_defaultEncoding",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(parser, Encoding.UTF8);

        // Act
        SmiParameterMetaData metadata = parameter.GetMetadataForTypeInfo(parser);

        // Assert
        Assert.Equal(expectedLength, metadata.MaxLength);
        Assert.Equal(expectedLength == -1, parameter.InternalMetaType.IsPlp);
        Assert.Equal(size, parameter.Size);
    }

    /// <summary>Null values preserve the declared bounded or MAX output capacity.</summary>
    [Theory]
    [InlineData(ParameterDirection.Output, 8000)]
    [InlineData(ParameterDirection.InputOutput, 8000)]
    [InlineData(ParameterDirection.Output, -1)]
    [InlineData(ParameterDirection.InputOutput, -1)]
    public void GetMetadataForTypeInfo_NullOutputValue_UsesDeclaredCapacity(
        ParameterDirection direction, int size)
    {
        // Arrange
        SqlParameter parameter = new("@p", SqlDbType.VarChar, size)
        {
            Direction = direction,
            Offset = 1,
            Value = DBNull.Value
        };
        TdsParser parser = new(false, false);
        typeof(TdsParser).GetField("_defaultEncoding",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(parser, Encoding.UTF8);

        // Act
        SmiParameterMetaData metadata = parameter.GetMetadataForTypeInfo(parser);

        // Assert
        Assert.Equal(size, metadata.MaxLength);
        Assert.Equal(size == -1, parameter.InternalMetaType.IsPlp);
        Assert.Equal(size, parameter.Size);
    }
}
