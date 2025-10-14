// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.TestFixtures
{
    /// <summary>
    /// This class has no code, and is never created. It serves only to
    /// associate the AlwaysEncryptedAKV [CollectionDefinition] and its
    /// ICollectionFixture<> interfaces.
    /// 
    /// Use this collection definition for tests that rely on the 
    /// SQLSetupStrategyAzureKeyVault fixture. Always clean data from
    /// any tables used directly in the test, as the same fixture instance
    /// and database tables are shared across test classes.
    /// </summary>
    [CollectionDefinition("AlwaysEncryptedAKV")]
    public class AlwaysEncryptedAKVCollection : ICollectionFixture<SQLSetupStrategyAzureKeyVault>
    {
    }
}
