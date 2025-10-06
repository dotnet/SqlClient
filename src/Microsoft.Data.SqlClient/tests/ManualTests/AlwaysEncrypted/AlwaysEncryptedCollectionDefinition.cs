// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    /// <summary>
    /// This class has no code, and is never created. It serves only to
    /// associate the AlwaysEncrypted [CollectionDefinition] and its
    /// ICollectionFixture<> interfaces.
    /// </summary>
    [CollectionDefinition("AlwaysEncryptedCertStore")]
    public class AlwaysEncryptedCollectionDefinition :ICollectionFixture<SQLSetupStrategyCertStoreProvider>
    {
    }
}
