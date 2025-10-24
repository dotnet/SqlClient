// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Identity;

#nullable enable

namespace Microsoft.Data.SqlClient.ManualTesting.Tests;

internal class ManagedIdentityProvider : SqlAuthenticationProvider
{
    public override async Task<SqlAuthenticationToken> AcquireTokenAsync(
        SqlAuthenticationParameters parameters)
    {
        const string s_defaultScopeSuffix = "/.default";

        string scope = parameters.Resource.EndsWith(
            s_defaultScopeSuffix, StringComparison.Ordinal)
            ? parameters.Resource
            : parameters.Resource + s_defaultScopeSuffix;

        TokenRequestContext context = new([scope]);

        try
        {
            TokenCredentialOptions options = new()
            {
                AuthorityHost = new Uri(parameters.Authority)
            };

            ManagedIdentityCredential credential = new(parameters.UserId, options);

            var cancellor = new CancellationTokenSource();
            cancellor.CancelAfter(parameters.AuthenticationTimeout * 1000);

            Console.WriteLine(
                "Acquiring token for ManagedIdentity " +
                $"userId={parameters.UserId} " +
                $"via authority={parameters.Authority} scope={scope}");

            AccessToken token =
            await credential.GetTokenAsync(context, cancellor.Token)
            .ConfigureAwait(false);

            Console.WriteLine(
                "Acquired token for ManagedIdentity " +
                $"userId={parameters.UserId} token={token.Token}");

            return new(token.Token, token.ExpiresOn);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to acquire token for ManagedIdentity userId={parameters.UserId} error={ex.Message}");
            Console.WriteLine(ex);
        }

        return new(string.Empty, DateTimeOffset.MinValue);
    }

    public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
    {
        return authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
    }

    static ManagedIdentityProvider()
    {
        SqlAuthenticationProviderManager.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryManagedIdentity,
            new ManagedIdentityProvider());
    }
}
