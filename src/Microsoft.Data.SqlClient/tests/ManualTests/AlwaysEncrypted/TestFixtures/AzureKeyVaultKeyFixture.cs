// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using Microsoft.Data.SqlClient.TestUtilities.Fixtures;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public sealed class AzureKeyVaultKeyFixture : AzureKeyVaultKeyFixtureBase
    {
        public AzureKeyVaultKeyFixture()
            : base(DataTestUtility.AKVBaseUri, DataTestUtility.GetTokenCredential())
        {
            GeneratedKeyUri = CreateKey(nameof(GeneratedKeyUri), 2048).ToString();
        }

        public string GeneratedKeyUri { get; }
    }
}
