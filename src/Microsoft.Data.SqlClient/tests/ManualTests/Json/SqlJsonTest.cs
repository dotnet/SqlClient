// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.Json
{

    public class SqlJsonTest
    {
        [Fact]
        public void SqlJsonTest_Null()
        {
            SqlJson json = new();
            Assert.True(json.IsNull);
            Assert.Throws<SqlNullValueException>(() => json.Value);

        }

        [Fact]
        public void SqlJsonTest_NullString()
        {
            string nullString = null;
            SqlJson json = new(nullString);
            Assert.True(json.IsNull);
            Assert.Throws<SqlNullValueException>(() => json.Value);
        }

        [Fact]
        public void SqlJsonTest_NullJsonDocument()
        {
            JsonDocument doc = null;
            SqlJson json = new(doc);
            Assert.True(json.IsNull);
            Assert.Throws<SqlNullValueException>(() => json.Value);
        }

        [Fact]
        public void SqlJsonTest_String()
        {
            SqlJson json = new("{\"key\":\"value\"}");
            Assert.False(json.IsNull);
            Assert.Equal("{\"key\":\"value\"}", json.Value);
        }

        [Fact]
        public void SqlJsonTest_BadString()
        {
            Assert.ThrowsAny<JsonException>(()=> new SqlJson("{\"key\":\"value\""));           
        }

        [Fact]
        public void SqlJsonTest_JsonDocument()
        {
            JsonDocument doc = GenerateRandomJson();
            SqlJson json = new(doc);
            Assert.False(json.IsNull);

            var outputDocument = JsonDocument.Parse(json.Value);
            Assert.True(JsonElementsAreEqual(doc.RootElement, outputDocument.RootElement));
        }

        [Fact]
        public void SqlJsonTest_NullProperty()
        {
            SqlJson json = SqlJson.Null;
            Assert.True(json.IsNull);
            Assert.Throws<SqlNullValueException>(() => json.Value);
        }

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
    }
}
