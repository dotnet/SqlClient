// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Azure.Core;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted;

public class TrustedUrlsTest
{
    private readonly SqlColumnEncryptionAzureKeyVaultProvider _provider;
    private readonly MethodInfo _method;

    public TrustedUrlsTest()
    {
        _provider = new(new SqlClientCustomTokenCredential());
        
        var assembly = typeof(SqlColumnEncryptionAzureKeyVaultProvider).Assembly;
        var clazz = assembly.GetType("Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.SqlColumnEncryptionAzureKeyVaultProvider");
        _method = clazz.GetMethod(
            "ValidateNonEmptyAKVPath",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
    }

    private static string MakeUrl(string vault)
    {
        return $"https://{vault}/keys/dummykey/dummykeyid";
    }

    public static string MakeInvalidVaultErrorMessage(string url)
    {
        return
            $"Invalid Azure Key Vault key path specified: '{url}'. " +
            "Valid trusted endpoints: " +
            "vault.azure.net, " +
            "vault.azure.cn, " +
            "vault.usgovcloudapi.net, " +
            "vault.microsoftazure.de, " +
            "vault.sovcloud-api.fr, " +
            "vault.sovcloud-api.de, " +
            "managedhsm.azure.net, " +
            "managedhsm.azure.cn, " +
            "managedhsm.usgovcloudapi.net, " +
            "managedhsm.microsoftazure.de, " +
            "managedhsm.sovcloud-api.fr, " +
            "managedhsm.sovcloud-api.de." +
            @"\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
    }

    [Theory]
    [InlineData("www.microsoft.com")]
    [InlineData("www.microsoft.vault.azure.com")]
    [InlineData("vault.azure.net.io")]
    public void InvalidVaults(string vault)
    {
        // Test that invalid key paths throw and contain the expected error
        // message.
        var url = MakeUrl(vault);

        try
        {
            _method.Invoke(_provider, new object[] { url, false });
        }
        catch (TargetInvocationException ex)
        {
            // Unwrap the exception to get the actual ArgumentException thrown
            var argEx = ex.InnerException as ArgumentException;
            Assert.NotNull(argEx);
            Assert.Matches(MakeInvalidVaultErrorMessage(url), argEx.Message);
        }
    }

    [Theory]
    // Normal vaults.
    [InlineData("vault.azure.net")]
    [InlineData("vault.azure.cn")]
    [InlineData("vault.usgovcloudapi.net")]
    [InlineData("vault.microsoftazure.de")]
    [InlineData("vault.sovcloud-api.fr")]
    [InlineData("vault.sovcloud-api.de")]
    // HSM vaults.
    [InlineData("managedhsm.azure.net")]
    [InlineData("managedhsm.azure.cn")]
    [InlineData("managedhsm.usgovcloudapi.net")]
    [InlineData("managedhsm.microsoftazure.de")]
    [InlineData("managedhsm.sovcloud-api.fr")]
    [InlineData("managedhsm.sovcloud-api.de")]
    // Vaults with prefixes.
    [InlineData("foo.bar.vault.microsoftazure.de")]
    [InlineData("baz.bar.foo.managedhsm.sovcloud-api.fr")]
    public void ValidVaults(string vault)
    {
        // Test that valid vault key paths do not throw exceptions
        _method.Invoke(_provider, new object[] { MakeUrl(vault), false });
    }
}
