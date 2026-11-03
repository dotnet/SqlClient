// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

// TODO: Binding to an Azure extension that lacks the validator, or whose validator has
// mismatched signatures, is not covered: it needs a fake Azure extension assembly with the same
// name. Successful binding is covered by the Azure extension's tests.

/// <summary>
/// Tests for <see cref="AzureAttestationTokenValidatorBinding"/> when the Azure extension can't
/// be loaded. This test project doesn't reference the Azure extension.
/// </summary>
public class AzureAttestationTokenValidatorBindingTests
{
    /// <summary>
    /// Without the Azure extension there is no binding, rather than an exception, so the enclave
    /// provider can report the missing package with its own error.
    /// </summary>
    [Fact]
    public void Instance_WithoutAzureExtension_IsNull()
    {
        Assert.Null(AzureAttestationTokenValidatorBinding.Instance);
    }
}
