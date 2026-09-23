// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure.Core;
using Azure.Identity;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Guards the migration of authentication credentials from Azure.Identity to Azure.Core.
/// </summary>
public class AzureDependencyTests
{
    /// <summary>
    /// Credentials used by the provider must come from Azure.Core, without a separate
    /// Azure.Identity implementation assembly.
    /// </summary>
    [Theory]
    [InlineData(typeof(DefaultAzureCredential))]
    [InlineData(typeof(ManagedIdentityCredential))]
    [InlineData(typeof(ClientSecretCredential))]
    [InlineData(typeof(WorkloadIdentityCredential))]
    public void CredentialTypes_AreProvidedByAzureCore(Type credentialType)
    {
        Assert.Same(typeof(TokenCredential).Assembly, credentialType.Assembly);
    }

    /// <summary>
    /// The Azure extension must no longer require Azure.Identity at runtime.
    /// </summary>
    [Fact]
    public void AuthenticationProvider_DoesNotReferenceAzureIdentity()
    {
        Assert.DoesNotContain(
            typeof(ActiveDirectoryAuthenticationProvider).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "Azure.Identity");
    }
}
