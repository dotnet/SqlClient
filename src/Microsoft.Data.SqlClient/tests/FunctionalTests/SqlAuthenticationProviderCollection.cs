// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    /// <summary>
    /// Defines a test collection that serializes execution of test classes
    /// which mutate the global <see cref="SqlAuthenticationProvider"/> registry.
    /// </summary>
    [CollectionDefinition("SqlAuthenticationProvider")]
    public class SqlAuthenticationProviderCollection
    {
    }
}
