// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Identity;

#nullable enable

namespace Microsoft.Data.SqlClient.ManualTesting.Tests;

internal class ManagedIdentityProvider : SqlAuthenticationProvider
{
    // Our cache of managed identity user Ids to credential instances.
    private readonly ConcurrentDictionary<string, ManagedIdentityCredential>
    _credentialCache = new();

    // The default suffix to apply to resource scopes.
    private const string s_defaultScopeSuffix = "/.default";

    // Acquire a token using Managed Identity.  The UserId in the parameters is
    // used as the managed identity client ID.  Tokens are cached per UserId.
    //
    // GOTCHA: This assumes that the Resource and Authority in the parameters
    // never change for a given UserId, which is probably a safe assumption for
    // tests.
    //
    public override async Task<SqlAuthenticationToken> AcquireTokenAsync(
        SqlAuthenticationParameters parameters)
    {
        if (parameters.UserId is null)
        {
            throw new TokenException(
                "Refusing to acquire token for ManagedIdentity with null UserId");
        }

        try
        {
            // Build an appropriate scope.
            string scope = parameters.Resource.EndsWith(
                s_defaultScopeSuffix, StringComparison.Ordinal)
                ? parameters.Resource
                : parameters.Resource + s_defaultScopeSuffix;

            TokenRequestContext context = new([scope]);

            TokenCredentialOptions options = new()
            {
                AuthorityHost = new Uri(parameters.Authority)
            };

            // Create or re-use the ManagedIdentityCredential for this UserId.
            ManagedIdentityCredential credential =
                _credentialCache.GetOrAdd(
                    parameters.UserId,
                    (_) => new(parameters.UserId, options));

            // Set up a cancellation token based on the authentication timeout,
            // ignoring overflow since this is just test code.
            using CancellationTokenSource cancellor = new();
            cancellor.CancelAfter(parameters.ConnectionTimeout * 1000);

            // Acquire the token, which may be cached by the credential.
            AccessToken token =
                await credential.GetTokenAsync(context, cancellor.Token)
                .ConfigureAwait(false);

            return new(token.Token, token.ExpiresOn);
        }
        catch (Exception ex)
        {
            throw new TokenException(
                $"Failed to acquire token for ManagedIdentity " +
                $"userId ={parameters.UserId} error={ex.Message}", ex);
        }
    }

    /// We support only the Managed Identity authentication method.
    public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
    {
        return authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
    }

    // The exception we throw on any errors acquiring tokens.
    private sealed class TokenException : SqlAuthenticationProviderException
    {
        internal TokenException(string message, Exception? causedBy = null)
            : base(message, causedBy)
        {
        }
    }
}
