// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    internal class AzureKeyVaultProviderTokenCredential : TokenCredential
    {
        private const string Bearer = "Bearer ";

        private AuthenticationCallback Callback { get; set; }

        private string Authority { get; set; }

        private string Resource { get; set; }

        internal AzureKeyVaultProviderTokenCredential(AuthenticationCallback authenticationCallback, string masterKeyPath)
        {
            Callback = authenticationCallback;
            using (HttpClient httpClient = new HttpClient())
            {
                HttpResponseMessage response = httpClient.GetAsync(masterKeyPath).GetAwaiter().GetResult();
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
                                    Authority = value;
                                }
                                else if (key.Equals("resource", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Resource = value;
                                }
                            }
                        }
                    }
                }
            }
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            string token = Callback.Invoke(Authority, Resource, string.Empty).GetAwaiter().GetResult();
            return new AccessToken(token, DateTimeOffset.Now.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            string token = Callback.Invoke(Authority, Resource, string.Empty).GetAwaiter().GetResult();
            Task<AccessToken> task = Task.FromResult(new AccessToken(token, DateTimeOffset.Now.AddHours(1)));
            return new ValueTask<AccessToken>(task);
        }

        private static string ValidateChallenge(string challenge)
        {
            if (string.IsNullOrEmpty(challenge))
                throw new ArgumentNullException(nameof(challenge));

            string trimmedChallenge = challenge.Trim();

            if (!trimmedChallenge.StartsWith(Bearer))
                throw new ArgumentException("Challenge is not Bearer", nameof(challenge));

            return trimmedChallenge.Substring(Bearer.Length);
        }
    }
}
