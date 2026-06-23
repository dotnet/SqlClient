// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    /// <summary>
    /// Defines a test collection that serializes execution of test classes
    /// which mutate the global <see cref="global::Microsoft.Data.SqlClient.SqlAuthenticationProvider"/> registry.
    /// </summary>
    [CollectionDefinition("SqlAuthenticationProvider")]
    public class SqlAuthenticationProviderCollection
    {
    }
}
