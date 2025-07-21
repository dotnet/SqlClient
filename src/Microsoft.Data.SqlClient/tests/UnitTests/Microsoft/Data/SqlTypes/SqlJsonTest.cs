// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Data.SqlTypes;
using System.Text.Json;
using Xunit;

namespace Microsoft.Data.SqlTypes.UnitTests;

#pragma warning disable CS1591 // Test classes do not require XML documentation comments

public class SqlJsonTest
{
    #region Private Fields

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    #endregion

    #region Tests

    // Test the static Null property.
    [Fact]
    public void StaticNull()
    {
        Assert.True(SqlJson.Null.IsNull);
        Assert.Throws<SqlNullValueException>(() => SqlJson.Null.Value);
        Assert.Null(SqlJson.Null.ToString());
    }

    // Test the constructor that takes no arguments.
    [Fact]
    public void Constructor_NoArgs()
    {
        SqlJson json = new();
        Assert.True(json.IsNull);
        Assert.Throws<SqlNullValueException>(() => json.Value);
        Assert.Null(json.ToString());
    }

    // Test the constructors that take a nullable string.
    [Fact]
    public void Constructor_String_Null()
    {
        const string? value = null;
        SqlJson json = new(value);
        Assert.True(json.IsNull);
        Assert.Throws<SqlNullValueException>(() => json.Value);
        Assert.Null(json.ToString());
    }

    [Fact]
    public void Constructor_String_NotNull()
    {
        const string value = "{\"key\":\"value\"}";
        SqlJson json = new(value);
        Assert.False(json.IsNull);
        Assert.Equal(value, json.Value);
        Assert.Equal(value, json.ToString());
    }

    [Theory]
    // Non-string key.
    [InlineData("{key:\"value\"}")]
    // Invalid value type.
    [InlineData("{\"key\":value}")]
    // Missing closing brace.
    [InlineData("{\"key\":\"value\"")]
    // Trailing comma.
    [InlineData("{\"key\":\"value\",}")]
    // Comment in JSON.
    [InlineData("// comment {\"key\":\"value\"}")]
    public void Constructor_String_Invalid(string invalid)
    {
        Assert.ThrowsAny<JsonException>(() => new SqlJson(invalid));
    }

    // Test the constructor that takes a nullable JsonDocument.
    [Fact]
    public void Constructor_JsonDocument_Null()
    {
        const JsonDocument? doc = null;
        SqlJson json = new(doc);
        Assert.True(json.IsNull);
        Assert.Throws<SqlNullValueException>(() => json.Value);
        Assert.Null(json.ToString());
    }

    [Fact]
    public void Constructor_JsonDocument_NotNull()
    {
        using JsonDocument doc = GenerateRandomJson();
        SqlJson json = new(doc);
        Assert.False(json.IsNull);
        Assert.Equal(doc.RootElement.GetRawText(), json.Value);
        Assert.Equal(doc.RootElement.GetRawText(), json.ToString());
    }

    [Fact]
    public void Constructor_JsonDocument_Disposed()
    {
        using JsonDocument doc = GenerateRandomJson();
        doc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new SqlJson(doc));
    }

    // IsNull, Value, and ToString() are covered by the above tests.

    // Test that the Value can be round-tripped through a JsonDocument.
    //
    // JsonElement.DeepEquals() is only available in .NET 9.0 and later.
    #if NET9_0_OR_GREATER
    [Fact]
    public void RoundTrip()
    {
        using JsonDocument doc = GenerateRandomJson();
        SqlJson json = new(doc);

        using var outputDocument = JsonDocument.Parse(json.Value);
        Assert.True(JsonElement.DeepEquals(
            doc.RootElement, outputDocument.RootElement));
    }
    #endif

    #endregion

    #region Helpers

    private static JsonDocument GenerateRandomJson()
    {
        Random random = new();

        object jsonObject = new
        {
            id = random.Next(1, 1000),
            name = $"Name{random.Next(1, 100)}",
            isActive = random.Next(0, 2) == 1,
            createdDate = DateTime.Now.AddDays(-random.Next(1, 100)).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            scores = new int[] { random.Next(1, 100), random.Next(1, 100), random.Next(1, 100) },
            details = new
            {
                age = random.Next(18, 60),
                city = $"City{random.Next(1, 100)}"
            }
        };

        return JsonSerializer.SerializeToDocument(jsonObject, _jsonOptions);
    }

    #endregion
}

#pragma warning restore CS1591
