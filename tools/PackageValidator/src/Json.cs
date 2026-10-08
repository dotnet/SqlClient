// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PackageValidator;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for the validation run output, providing
/// trim- and AOT-friendly serialization with indented, camel-cased output that omits null members
/// and writes enums as strings.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ValidationRun))]
internal sealed partial class JsonContext : JsonSerializerContext
{
    private static JsonSerializerOptions? s_serializerOptions;

    /// <summary>
    /// Gets serializer options based on this context's generated metadata but with relaxed escaping,
    /// so characters that are safe inside a JSON string (such as <c>+</c>) are emitted literally
    /// rather than as <c>\uXXXX</c> escapes.
    /// </summary>
    /// <remarks>
    /// Built lazily rather than in a field initializer to avoid a static-initialization ordering
    /// dependency on the source-generated <see cref="JsonSerializerContext.Default"/> instance.
    /// </remarks>
    public static JsonSerializerOptions SerializerOptions =>
        s_serializerOptions ??= new(Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
}
