// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json;
#if NET9_0_OR_GREATER
using System.Text.Json.Nodes;
#endif

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.JsonTest
{
    internal static class JsonTestHelper
    {
        // Test data is Array → Object → Value (3 levels). Use 64 as a safe ceiling.
        private const int MaxDepth = 64;

        /// <summary>
        /// Performs a deep structural comparison of two <see cref="JsonElement"/> values.
        /// On .NET 9+ this delegates to <see cref="JsonNode.DeepEquals"/>; on earlier
        /// runtimes it uses a recursive comparison over the element trees.
        /// </summary>
        internal static bool JsonDeepEquals(JsonElement a, JsonElement b)
        {
#if NET9_0_OR_GREATER
            return JsonNode.DeepEquals(
                JsonNode.Parse(a.GetRawText()),
                JsonNode.Parse(b.GetRawText()));
#else
            return DeepEqualsCore(a, b, depth: 0);
#endif
        }

        private static bool DeepEqualsCore(JsonElement a, JsonElement b, int depth)
        {
            if (depth > MaxDepth)
            {
                throw new InvalidOperationException($"JSON comparison exceeded maximum depth of {MaxDepth}.");
            }

            if (a.ValueKind != b.ValueKind)
            {
                return false;
            }

            switch (a.ValueKind)
            {
                case JsonValueKind.Object:
                    int countA = a.EnumerateObject().Count();
                    int countB = b.EnumerateObject().Count();
                    if (countA != countB)
                    {
                        return false;
                    }
                    foreach (JsonProperty prop in a.EnumerateObject())
                    {
                        if (!b.TryGetProperty(prop.Name, out JsonElement bValue) ||
                            !DeepEqualsCore(prop.Value, bValue, depth + 1))
                        {
                            return false;
                        }
                    }
                    return true;

                case JsonValueKind.Array:
                    JsonElement.ArrayEnumerator arrA = a.EnumerateArray();
                    JsonElement.ArrayEnumerator arrB = b.EnumerateArray();
                    while (arrA.MoveNext())
                    {
                        if (!arrB.MoveNext() || !DeepEqualsCore(arrA.Current, arrB.Current, depth + 1))
                        {
                            return false;
                        }
                    }
                    return !arrB.MoveNext();

                case JsonValueKind.String:
                    return a.GetString() == b.GetString();

                default:
                    return a.GetRawText() == b.GetRawText();
            }
        }
    }
}
