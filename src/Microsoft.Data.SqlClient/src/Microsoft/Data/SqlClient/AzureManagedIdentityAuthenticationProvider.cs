using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    ///
    /// </summary>
    public sealed class AzureManagedIdentityAuthenticationProvider : SqlAuthenticationProvider
    {
        // This is for unit testing
        private readonly HttpClient _httpClient;

        // HttpClient is intended to be instantiated once and re-used throughout the life of an application.
#if NETFRAMEWORK
        private static readonly HttpClient DefaultHttpClient = new HttpClient();
#else
        private static readonly HttpClient DefaultHttpClient = new HttpClient(new HttpClientHandler() { CheckCertificateRevocationList = true });
#endif

        // Retry acquiring access token upto 20 times due to possible IMDS upgrade (Applies to VM only)
        private const int AZURE_IMDS_MAX_RETRY = 20;
        private const string AzureSystemApiVersion = "&api-version=2019-08-01";
        private const string AzureVmImdsApiVersion = "&api-version=2018-02-01";

        // Azure Instance Metadata Service (IMDS) endpoint
        private const string AzureVmImdsEndpoint = "http://169.254.169.254/metadata/identity/oauth2/token";

        // Timeout for Azure IMDS probe request
        internal const int AzureVmImdsProbeTimeoutInSeconds = 2;
        internal readonly TimeSpan AzureVmImdsProbeTimeout = TimeSpan.FromSeconds(AzureVmImdsProbeTimeoutInSeconds);

        // Configurable timeout for MSI retry logic
        internal readonly int _retryTimeoutInSeconds = 0;

        /// <summary>
        ///
        /// </summary>
        /// <param name="retryTimeoutInSeconds"></param>
        /// <param name="httpClient"></param>
        public AzureManagedIdentityAuthenticationProvider(int retryTimeoutInSeconds = 0, HttpClient httpClient = null)
        {
            _retryTimeoutInSeconds = retryTimeoutInSeconds;
            _httpClient = httpClient;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            // Use the httpClient specified in the constructor. If it was not specified in the constructor, use the default httpClient.
            HttpClient httpClient = _httpClient ?? DefaultHttpClient;

            try
            {
                // Check if App Services MSI is available. If both these environment variables are set, then it is.
                string msiEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
                string msiHeader = Environment.GetEnvironmentVariable("IDENTITY_HEADER");

                var isAppServicesMsiAvailable = !string.IsNullOrWhiteSpace(msiEndpoint) && !string.IsNullOrWhiteSpace(msiHeader);

                // if App Service MSI is not available then Azure VM IMDS may be available, test with a probe request
                if (!isAppServicesMsiAvailable)
                {
                    using (var internalTokenSource = new CancellationTokenSource())
                    using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(internalTokenSource.Token, default))
                    {
                        HttpRequestMessage imdsProbeRequest = new HttpRequestMessage(HttpMethod.Get, AzureVmImdsEndpoint);

                        try
                        {
                            internalTokenSource.CancelAfter(AzureVmImdsProbeTimeout);
                            await httpClient.SendAsync(imdsProbeRequest, linkedTokenSource.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // request to IMDS timed out (internal cancellation token canceled), neither Azure VM IMDS nor App Services MSI are available
                            if (internalTokenSource.Token.IsCancellationRequested)
                            {
                                //throw new AzureServiceTokenProviderException(ConnectionString, parameters.Resource, parameters.Authority,
                                //    $"{AzureServiceTokenProviderException.ManagedServiceIdentityUsed} {AzureServiceTokenProviderException.MsiEndpointNotListening}");
                            }

                            throw;
                        }
                    }
                }

                // If managed identity is specified, include object ID parameter in request
                string clientIdParameter = parameters.UserId != default
                    ? $"&object_id={parameters.UserId}"
                    : string.Empty;

                // Craft request as per the MSI protocol
                var requestUrl = isAppServicesMsiAvailable
                    ? $"{msiEndpoint}?resource={parameters.Resource}{clientIdParameter}{AzureSystemApiVersion}"
                    : $"{AzureVmImdsEndpoint}?resource={parameters.Resource}{clientIdParameter}{AzureVmImdsApiVersion}";

                Func<HttpRequestMessage> getRequestMessage = () =>
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                    if (isAppServicesMsiAvailable)
                    {
                        request.Headers.Add("X-IDENTITY-HEADER", msiHeader);
                    }
                    else
                    {
                        request.Headers.Add("Metadata", "true");
                    }

                    return request;
                };

                HttpResponseMessage response = null;
                try
                {
                    response = await httpClient.SendAsyncWithRetry(getRequestMessage, _retryTimeoutInSeconds, default).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    //throw new AzureServiceTokenProviderException(ConnectionString, resource, authority,
                    //    $"{AzureServiceTokenProviderException.ManagedServiceIdentityUsed} {AzureServiceTokenProviderException.RetryFailure} {AzureServiceTokenProviderException.MsiEndpointNotListening}");
                }

                // If the response is successful, it should have JSON response with an access_token field
                if (response.IsSuccessStatusCode)
                {
                    //PrincipalUsed.IsAuthenticated = true;

                    //string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    //// Parse the JSON response
                    //TokenResponse tokenResponse = TokenResponse.Parse(jsonResponse);

                    //AccessToken token = AccessToken.Parse(tokenResponse.AccessToken);

                    //// If token is null, then there has been a parsing issue, which means the access token format has changed
                    //if (token != null)
                    //{
                    //    PrincipalUsed.AppId = token.AppId;
                    //    PrincipalUsed.TenantId = token.TenantId;
                    //}

                    //return AppAuthenticationResult.Create(tokenResponse);
                    return null;
                }

                //string errorStatusDetail = response.IsRetryableStatusCode()
                //    ? AzureServiceTokenProviderException.RetryFailure
                //    : AzureServiceTokenProviderException.NonRetryableError;

                //string errorText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                //throw new Exception($"{errorStatusDetail} MSI ResponseCode: {response.StatusCode}, Response: {errorText}");
            }
            catch (Exception exp)
            {
                if (exp is SqlException)
                    throw;

                //throw new AzureServiceTokenProviderException(ConnectionString, resource, authority,
                //    $"{AzureServiceTokenProviderException.ManagedServiceIdentityUsed} {AzureServiceTokenProviderException.GenericErrorMessage} {exp.Message}");
            }

            //Cheena: Remove later
            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="authentication"></param>
        /// <returns></returns>
        public override bool IsSupported(SqlAuthenticationMethod authentication)
        {
            return authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
        }
    }

    internal static class SqlManagedIdentityRetryHelper
    {
        internal const int MaxRetries = 5;
        internal const int DeltaBackOffInSeconds = 2;
        internal const string RetryTimeoutError = "Reached retry timeout limit set by MsiRetryTimeout parameter in connection string.";

        // for unit test purposes
        internal static bool WaitBeforeRetry = true;

        internal static bool IsRetryableStatusCode(this HttpResponseMessage response)
        {
            // 404 NotFound, 429 TooManyRequests, and 5XX server error status codes are retryable
            return Regex.IsMatch(((int)response.StatusCode).ToString(), @"404|429|5\d{2}");
        }

        /// <summary>
        /// Implements recommended retry guidance here: https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-to-use-vm-token#retry-guidance
        /// </summary>
        internal static async Task<HttpResponseMessage> SendAsyncWithRetry(this HttpClient httpClient, Func<HttpRequestMessage> getRequest, int retryTimeoutInSeconds, CancellationToken cancellationToken)
        {
            using (var timeoutTokenSource = new CancellationTokenSource())
            using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken))
            {
                try
                {
                    // if retry timeout is configured, configure cancellation after timeout period elapses
                    if (retryTimeoutInSeconds > 0)
                    {
                        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(retryTimeoutInSeconds));
                    }

                    var attempts = 0;
                    var backoffTimeInSecs = 0;
                    HttpResponseMessage response;

                    while (true)
                    {
                        attempts++;

                        try
                        {
                            response = await httpClient.SendAsync(getRequest(), linkedTokenSource.Token).ConfigureAwait(false);

                            if (response.IsSuccessStatusCode || !response.IsRetryableStatusCode() || attempts == MaxRetries)
                            {
                                break;
                            }
                        }
                        catch (HttpRequestException)
                        {
                            if (attempts == MaxRetries)
                                throw;
                        }

                        if (WaitBeforeRetry)
                        {
                            // use recommended exponential backoff strategy, and use linked token wait handle so caller or retry timeout is still able to cancel
                            backoffTimeInSecs = backoffTimeInSecs + (int)Math.Pow(DeltaBackOffInSeconds, attempts);
                            linkedTokenSource.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(backoffTimeInSecs));
                            linkedTokenSource.Token.ThrowIfCancellationRequested();
                        }
                    }

                    return response;
                }
                catch (OperationCanceledException)
                {
                    if (timeoutTokenSource.IsCancellationRequested)
                    {
                        throw new TimeoutException(RetryTimeoutError);
                    }

                    throw;
                }
            }
        }
    }
}
