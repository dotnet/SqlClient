using Azure.Core;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.Encryption.AzureKeyVaultProvider
{

    public class AzureKeyVaultProviderTokenCredential : TokenCredential
    {
        /// <summary>
        /// The authentication callback delegate which is to be implemented by the client code
        /// </summary>
        /// <param name="authority"> Identifier of the authority, a URL. </param>
        /// <param name="resource"> Identifier of the target resource that is the recipient of the requested token, a URL. </param>
        /// <param name="scope"> The scope of the authentication request. </param>
        /// <returns> access token </returns>
        public delegate Task<string> AuthenticationCallback(string authority, string resource, string scope);

        private AuthenticationCallback Callback { get; set; }

        private string Authority { get; set; }

        private string Resource { get; set; }

        public AzureKeyVaultProviderTokenCredential(AuthenticationCallback authenticationCallback, string masterKeyPath)
        {
            Callback = authenticationCallback;
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = httpClient.GetAsync(masterKeyPath).GetAwaiter().GetResult();
            string challenge = response?.Headers.WwwAuthenticate.FirstOrDefault()?.ToString();
            string trimmedChallenge = ValidateChallenge(challenge);
            string[] pairs = trimmedChallenge.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            Authority = pairs[0].Split('=')[1].Trim().Trim(new char[] { '\"' });
            Resource = pairs[1].Split('=')[1].Trim().Trim(new char[] { '\"' });
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
            const string bearer = "Bearer ";
            if (string.IsNullOrEmpty(challenge))
                throw new ArgumentNullException("challenge");

            string trimmedChallenge = challenge.Trim();

            if (!trimmedChallenge.StartsWith(bearer))
                throw new ArgumentException("Challenge is not Bearer", "challenge");

            return trimmedChallenge.Substring(bearer.Length);
        }
    }
}
