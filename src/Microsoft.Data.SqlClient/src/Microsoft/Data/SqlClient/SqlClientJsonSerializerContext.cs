// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Data.SqlClient;

[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(ReadOnlyMemory<float>))]
[JsonSerializable(typeof(List<byte>))]
internal sealed partial class SqlClientJsonSerializerContext : JsonSerializerContext
{
}
