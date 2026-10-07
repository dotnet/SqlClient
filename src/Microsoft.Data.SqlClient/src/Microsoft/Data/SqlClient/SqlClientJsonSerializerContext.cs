// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Source-generated JSON metadata. Each <c>[JsonSerializable]</c> type gets a property named after its CLR type:
/// <c>float[]</c> -> <c>SingleArray</c>, <c>ReadOnlyMemory&lt;float&gt;</c> -> <c>ReadOnlyMemorySingle</c>, <c>List&lt;byte&gt;</c> -> <c>ListByte</c>.
/// </summary>
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(ReadOnlyMemory<float>))]
[JsonSerializable(typeof(List<byte>))]
internal sealed partial class SqlClientJsonSerializerContext : JsonSerializerContext
{
}
