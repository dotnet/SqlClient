// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

#nullable enable

namespace Microsoft.Data.SqlClient.ManualTesting.Tests;

internal class UsernamePasswordProvider : SqlAuthenticationProvider
{
    readonly string _appClientId;
    const string s_defaultScopeSuffix = "/.default";

    internal UsernamePasswordProvider(string appClientId)
    {
        _appClientId = appClientId;
    }

    public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
    {
        try
        {
            string scope =
                parameters.Resource.EndsWith(s_defaultScopeSuffix, StringComparison.Ordinal)
                ? parameters.Resource
                : parameters.Resource + s_defaultScopeSuffix;

            using CancellationTokenSource cts = new();
            cts.CancelAfter(parameters.ConnectionTimeout * 1000);

            AuthenticationResult result =
                #pragma warning disable CS0618 // Type or member is obsolete
                await PublicClientApplicationBuilder.Create(_appClientId)
                .WithAuthority(parameters.Authority)
                .Build()
                .AcquireTokenByUsernamePassword([scope], parameters.UserId, parameters.Password)
                #pragma warning restore CS0618 // Type or member is obsolete
                .WithCorrelationId(parameters.ConnectionId)
                .ExecuteAsync(cancellationToken: cts.Token);

            return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
        }
        catch (Exception ex)
        {
            throw new TokenException(
                $"Failed to acquire token for username/password " +
                $"userId ={parameters.UserId} error={ex.Message}", ex);
        }
    }

    public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
    {
        #pragma warning disable 0618 // Type or member is obsolete
        return authenticationMethod.Equals(SqlAuthenticationMethod.ActiveDirectoryPassword);
        #pragma warning restore 0618 // Type or member is obsolete
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
