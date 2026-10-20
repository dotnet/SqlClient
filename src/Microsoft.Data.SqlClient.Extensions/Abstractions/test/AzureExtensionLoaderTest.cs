// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies optional assembly loading without a dependency on the Azure extension.</summary>
public class AzureExtensionLoaderTest
{
    /// <summary>A missing extension does not prevent explicit authentication registration.</summary>
    [Fact]
    public void MissingAzureExtensionReturnsNull()
    {
        Assert.Null(AzureExtensionLoader.Load(nameof(AzureExtensionLoaderTest)));
    }
}
