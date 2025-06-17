// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Data.SqlTypes;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

public class SqlJsonTest
{
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
    public void Constructor_String()
    {
        // Null string.
        string? value = null;
        SqlJson json = new(value);
        Assert.True(json.IsNull);
        Assert.Throws<SqlNullValueException>(() => json.Value);
        Assert.Null(json.ToString());

        // Not-null string.
        value = "{\"key\":\"value\"}";
        json = new(value);
        Assert.False(json.IsNull);
        Assert.Equal(value, json.Value);
        Assert.Equal(value, json.ToString());

        // Invalid JSON syntax.
        foreach (var invalid in new[]
            {
                // Non-string key.
                "{key:\"value\"}",
                // Invalid value type.
                "{\"key\":value}",
                // Missing closing brace.
                "{\"key\":\"value\"",
                // Trailing comma.
                "{\"key\":\"value\",}",
                // Comment in JSON.
                "// comment {\"key\":\"value\"}"
            })
        {
            Assert.ThrowsAny<JsonException>(() => new SqlJson(invalid));
        }
    }

    // Test the constructor that takes a nullable JsonDocument.
    [Fact]
    public void Constructor_JsonDocument()
    {
        // Null document.
        JsonDocument? doc = null;
        SqlJson json = new(doc);
        Assert.True(json.IsNull);
        Assert.Throws<SqlNullValueException>(() => json.Value);
        Assert.Null(json.ToString());

        // Not-null document.
        doc = GenerateRandomJson();
        json = new(doc);
        Assert.False(json.IsNull);
        Assert.Equal(doc.RootElement.GetRawText(), json.Value);
        Assert.Equal(doc.RootElement.GetRawText(), json.ToString());

        // Document has been disposed of.
        doc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new SqlJson(doc));
    }

    // IsNull, Value, and ToString() are covered by the above tests.

    // Test that the Value can be round-tripped through a JsonDocument.
    [Fact]
    public void RoundTrip()
    {
        var doc = GenerateRandomJson();
        SqlJson json = new(doc);

        var outputDocument = JsonDocument.Parse(json.Value);
        Assert.True(JsonElementsAreEqual(doc.RootElement, outputDocument.RootElement));
    }

    #endregion

    #region Helpers

    static JsonDocument GenerateRandomJson()
    {
        var random = new Random();

        var jsonObject = new
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

        string jsonString = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
        return JsonDocument.Parse(jsonString);
    }

    static bool JsonElementsAreEqual(JsonElement element1, JsonElement element2)
    {
        if (element1.ValueKind != element2.ValueKind)
            return false;

        switch (element1.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    JsonElement.ObjectEnumerator obj1 = element1.EnumerateObject();
                    JsonElement.ObjectEnumerator obj2 = element2.EnumerateObject();
                    var dict1 = obj1.ToDictionary(p => p.Name, p => p.Value);
                    var dict2 = obj2.ToDictionary(p => p.Name, p => p.Value);

                    if (dict1.Count != dict2.Count)
                        return false;

                    foreach (var kvp in dict1)
                    {
                        if (!dict2.TryGetValue(kvp.Key, out var value2))
                            return false;

                        if (!JsonElementsAreEqual(kvp.Value, value2))
                            return false;
                    }

                    return true;
                }
            case JsonValueKind.Array:
                {
                    var array1 = element1.EnumerateArray();
                    var array2 = element2.EnumerateArray();

                    if (array1.Count() != array2.Count())
                        return false;

                    return array1.Zip(array2, (e1, e2) => JsonElementsAreEqual(e1, e2)).All(equal => equal);
                }
            case JsonValueKind.String:
                return element1.GetString() == element2.GetString();
            case JsonValueKind.Number:
                return element1.GetDecimal() == element2.GetDecimal();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element1.GetBoolean() == element2.GetBoolean();
            case JsonValueKind.Null:
                return true;
            default:
                throw new NotSupportedException($"Unsupported JsonValueKind: {element1.ValueKind}");
        }
    }

    #endregion
}
