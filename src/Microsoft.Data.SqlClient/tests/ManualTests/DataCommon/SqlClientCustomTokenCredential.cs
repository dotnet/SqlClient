// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlClientCustomTokenCredential : TokenCredential
    {
        string _authority = "";
        string _resource = "";
        string _akvUrl = "";

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            AcquireTokenAsync().GetAwaiter().GetResult();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            await AcquireTokenAsync();

        private async Task<AccessToken> AcquireTokenAsync()
        {
            // Added to reduce HttpClient calls.
            // For multi-user support, a better design can be implemented as needed.
            if (_akvUrl != DataTestUtility.AKVUrl)
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage response = await httpClient.GetAsync(DataTestUtility.AKVUrl);
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
                // Since this is a test, we only create single-instance temp cache
                _akvUrl = DataTestUtility.AKVUrl;
            }

            string strAccessToken = await AzureActiveDirectoryAuthenticationCallback(_authority, _resource);
            DateTime expiryTime = InterceptAccessTokenForExpiry(strAccessToken);
            return new AccessToken(strAccessToken, new DateTimeOffset(expiryTime));
        }

        private DateTime InterceptAccessTokenForExpiry(string accessToken)
        {
            if (null == accessToken)
            {
                throw new ArgumentNullException(accessToken);
            }

            var jwtHandler = new JwtSecurityTokenHandler();
            var jwtOutput = string.Empty;

            // Check Token Format
            if (!jwtHandler.CanReadToken(accessToken))
                throw new FormatException(accessToken);

            JwtSecurityToken token = jwtHandler.ReadJwtToken(accessToken);

            // Re-serialize the Token Headers to just Key and Values
            var jwtHeader = JsonConvert.SerializeObject(token.Header.Select(h => new { h.Key, h.Value }));
            jwtOutput = $"{{\r\n\"Header\":\r\n{JToken.Parse(jwtHeader)},";

            // Re-serialize the Token Claims to just Type and Values
            var jwtPayload = JsonConvert.SerializeObject(token.Claims.Select(c => new { c.Type, c.Value }));
            jwtOutput += $"\r\n\"Payload\":\r\n{JToken.Parse(jwtPayload)}\r\n}}";

            // Output the whole thing to pretty JSON object formatted.
            string jToken = JToken.Parse(jwtOutput).ToString(Formatting.Indented);
            JToken payload = JObject.Parse(jToken).GetValue("Payload");

            return new DateTime(1970, 1, 1).AddSeconds((long)payload[4]["Value"]);
        }

        private static string ValidateChallenge(string challenge)
        {
            string Bearer = "Bearer ";
            if (string.IsNullOrEmpty(challenge))
                throw new ArgumentNullException(nameof(challenge));

            string trimmedChallenge = challenge.Trim();

            if (!trimmedChallenge.StartsWith(Bearer, StringComparison.Ordinal))
                throw new ArgumentException("Challenge is not Bearer", nameof(challenge));

            return trimmedChallenge.Substring(Bearer.Length);
        }

        /// <summary>
        /// Legacy implementation of Authentication Callback, used by Azure Key Vault provider 1.0.
        /// This can be leveraged to support multi-user authentication support in the same Azure Key Vault Provider.
        /// </summary>
        /// <param name="authority">Authorization URL</param>
        /// <param name="resource">Resource</param>
        /// <returns></returns>
        public static async Task<string> AzureActiveDirectoryAuthenticationCallback(string authority, string resource)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);
            if (result == null)
            {
                throw new InvalidOperationException($"Failed to retrieve an access token for {resource}");
            }
            return result.AccessToken;
        }
    }
}
