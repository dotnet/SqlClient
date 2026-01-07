// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlClientCustomTokenCredential : TokenCredential
    {
        private const string DEFAULT_PREFIX = "/.default";
        private const string AKVKeyName = "TestSqlClientAzureKeyVaultProvider";

        string _authority = "";
        string _resource = "";

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            AcquireTokenAsync().GetAwaiter().GetResult();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            await AcquireTokenAsync();

        private async Task<AccessToken> AcquireTokenAsync()
        {
            // Added to reduce HttpClient calls.
            // For multi-user support, a better design can be implemented as needed.
            if (string.IsNullOrEmpty(_authority) || string.IsNullOrEmpty(_resource))
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    string akvUrl = new Uri(DataTestUtility.AKVBaseUri, $"/keys/{AKVKeyName}").AbsoluteUri;
                    HttpResponseMessage response = await httpClient.GetAsync(akvUrl);
                    string challenge = response?.Headers.WwwAuthenticate.FirstOrDefault()?.ToString();
                    string trimmedChallenge = ValidateChallenge(challenge);
                    string[] pairs = trimmedChallenge.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    if (pairs != null && pairs.Length > 0)
                    {
                        for (int i = 0; i < pairs.Length; i++)
                        {
                            string[] pair = pairs[i]?.Split('=');

                            if (pair.Length == 2)
                            {
                                string key = pair[0]?.Trim().Trim(new char[] { '\"' });
                                string value = pair[1]?.Trim().Trim(new char[] { '\"' });

                                if (!string.IsNullOrEmpty(key))
                                {
                                    if (key.Equals("authorization", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        _authority = value;
                                    }
                                    else if (key.Equals("resource", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        _resource = value;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            AccessToken accessToken = await AzureActiveDirectoryAuthenticationCallback(_authority, _resource);
            return accessToken;
        }

        private static string ValidateChallenge(string challenge)
        {
            string Bearer = "Bearer ";
            if (string.IsNullOrEmpty(challenge))
            {
                throw new ArgumentNullException(nameof(challenge));
            }

            string trimmedChallenge = challenge.Trim();

            if (!trimmedChallenge.StartsWith(Bearer, StringComparison.Ordinal))
            {
                throw new ArgumentException("Challenge is not Bearer", nameof(challenge));
            }

            return trimmedChallenge.Substring(Bearer.Length);
        }

        /// <summary>
        /// Legacy implementation of Authentication Callback, used by Azure Key Vault provider 1.0.
        /// This can be leveraged to support multi-user authentication support in the same Azure Key Vault Provider.
        /// </summary>
        /// <param name="authority">Authorization URL</param>
        /// <param name="resource">Resource</param>
        /// <returns></returns>
        public static async Task<AccessToken> AzureActiveDirectoryAuthenticationCallback(string authority, string resource)
        {
            using CancellationTokenSource cts = new();
            cts.CancelAfter(30000); // Hard coded for tests
            string[] scopes = new string[] { resource + DEFAULT_PREFIX };
            TokenRequestContext tokenRequestContext = new(scopes);
            int separatorIndex = authority.LastIndexOf('/');
            string authorityHost = authority.Remove(separatorIndex + 1);
            string audience = authority.Substring(separatorIndex + 1);
            TokenCredentialOptions tokenCredentialOptions = new TokenCredentialOptions() { AuthorityHost = new Uri(authorityHost) };
            AccessToken accessToken = await DataTestUtility.GetTokenCredential().GetTokenAsync(tokenRequestContext, cts.Token).ConfigureAwait(false);
            return accessToken;
        }
    }
}
