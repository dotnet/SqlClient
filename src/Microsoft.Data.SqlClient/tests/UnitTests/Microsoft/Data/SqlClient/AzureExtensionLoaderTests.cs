// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

// TODO: Loading succeeds, and the public key token check, are not covered here: this project
// doesn't reference the Azure extension, and the token check only runs in strong-name signed
// builds. Loading succeeds is covered indirectly by the Azure extension's tests, which bind to
// its validator through this loader.

/// <summary>
/// Tests for <see cref="AzureExtensionLoader"/>. This test project doesn't reference the Azure
/// extension, so these cover applications without it.
/// </summary>
public class AzureExtensionLoaderTests
{
    /// <summary>
    /// A missing Azure extension is reported as null rather than thrown, so that callers have a
    /// single failure mode to handle.
    /// </summary>
    [Fact]
    public void Load_WithoutAzureExtension_ReturnsNull()
    {
        Assert.Null(AzureExtensionLoader.Load(nameof(AzureExtensionLoaderTests)));
    }
}
