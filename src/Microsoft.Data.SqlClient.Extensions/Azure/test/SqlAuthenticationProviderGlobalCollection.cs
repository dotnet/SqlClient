// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Defines a test collection that serializes execution of test classes
/// which mutate the global <see cref="SqlAuthenticationProvider"/> registry.
/// </summary>
[CollectionDefinition("SqlAuthenticationProviderGlobal")]
public class SqlAuthenticationProviderGlobalCollection
{
}
