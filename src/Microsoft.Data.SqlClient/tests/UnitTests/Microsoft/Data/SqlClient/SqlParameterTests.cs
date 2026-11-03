// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Verifies parameter coercion remains separate from vector reader projections.</summary>
public sealed class SqlParameterTests
{
    /// <summary>Supplies each native vector type and unsupported text parameter destination.</summary>
    public static TheoryData<SqlDbType, object> NativeVectorTextInputs
    {
        get
        {
            TheoryData<SqlDbType, object> data = new();
            foreach (SqlDbType destination in new[]
            {
                SqlDbType.VarChar, SqlDbType.NVarChar, SqlDbTypeExtensions.Json
            })
            {
                data.Add(destination, new SqlVector<float>(new[] { 1.5f }));
                #if NET
                data.Add(destination, new SqlVector<Half>(new[] { (Half)1.5f }));
                #endif
            }
            return data;
        }
    }

    /// <summary>Rejects typed vectors instead of implicitly serializing them for text parameters.</summary>
    /// <param name="destination">The text parameter type.</param>
    /// <param name="value">The native vector value.</param>
    [Theory]
    [MemberData(nameof(NativeVectorTextInputs))]
    public void CoerceValue_NativeVectorToText_ThrowsInvalidCastException(
        SqlDbType destination, object value)
    {
        // Arrange
        MetaType type = MetaType.GetMetaTypeFromSqlDbType(destination, false);

        // Act
        Action action = () => SqlParameter.CoerceValue(value, type, out _, out _, false);

        // Assert
        Assert.Throws<InvalidCastException>(action);
    }
}
