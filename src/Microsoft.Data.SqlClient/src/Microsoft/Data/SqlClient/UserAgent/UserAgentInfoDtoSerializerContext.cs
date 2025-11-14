// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

#nullable enable

namespace Microsoft.Data.SqlClient.UserAgent;

[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(UserAgentInfoDto), GenerationMode = JsonSourceGenerationMode.Serialization)]
internal sealed partial class UserAgentInfoDtoSerializerContext : JsonSerializerContext
{
}
