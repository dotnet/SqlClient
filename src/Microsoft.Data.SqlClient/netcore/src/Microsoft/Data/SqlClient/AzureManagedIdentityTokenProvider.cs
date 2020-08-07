// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Newtonsoft.Json.Linq;

namespace Microsoft.Data.SqlClient.Microsoft.Data.SqlClient
{
    /// <summary>
    /// Implementation cloned from https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/mgmtcommon/AppAuthentication/Azure.Services.AppAuthentication/TokenProviders/MsiAccessTokenProvider.cs
    /// </summary>
    internal class AzureManagedIdentityTokenProvider
    {
        // This is for unit testing
        private readonly HttpClient _httpClient;

        // This client ID can be specified in the constructor to specify a specific managed identity to use (e.g. user-assigned identity)
        private readonly string _managedIdentityClientId;

        // HttpClient is intended to be instantiated once and re-used throughout the life of an application. 
#if NETFRAMEWORK
        private static readonly HttpClient s_defaultHttpClient = new HttpClient();
#else
        private static readonly HttpClient s_defaultHttpClient = new HttpClient(new HttpClientHandler() { CheckCertificateRevocationList = true });
#endif

        // Timeout for Azure IMDS probe request
        internal const int AzureVmImdsProbeTimeoutInSeconds = 2;
        internal readonly TimeSpan _azureVmImdsProbeTimeout = TimeSpan.FromSeconds(AzureVmImdsProbeTimeoutInSeconds);

        // Configurable timeout for MSI retry logic
        internal readonly int _retryTimeoutInSeconds = 0;
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

        internal AzureManagedIdentityTokenProvider(int retryTimeoutInSeconds = 0, string managedIdentityClientId = default)
        {
            // require storeLocation if using subject name or thumbprint identifier
            if (retryTimeoutInSeconds < 0)
            {
                throw new ArgumentException(
                    $"MsiRetryTimeout {retryTimeoutInSeconds} is not valid. Valid values are integers greater than or equal to 0.");
            }

            _managedIdentityClientId = managedIdentityClientId;
            _retryTimeoutInSeconds = retryTimeoutInSeconds;
        }

        internal AzureManagedIdentityTokenProvider(HttpClient httpClient, int retryTimeoutInSeconds = 0, string managedIdentityClientId = null) : this(retryTimeoutInSeconds, managedIdentityClientId)
        {
            _httpClient = httpClient;
        }

        public async Task<SqlAuthenticationToken> AcquireTokenAsync(string resource, string authority,
            CancellationToken cancellationToken = default)
        {
            // Use the httpClient specified in the constructor. If it was not specified in the constructor, use the default httpClient. 
            HttpClient httpClient = _httpClient ?? s_defaultHttpClient;

            try
            {
                // Check if App Services MSI is available. If both these environment variables are set, then it is.
                // NOTE: IDENTITY_ENDPOINT is an alias for MSI_ENDPOINT environment variable.
                // NOTE: IDENTITY_HEADER is an alias for MSI_SECRET environment variable
                string idEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
                string idHeader = Environment.GetEnvironmentVariable("IDENTITY_HEADER");
                var isAppServicesMsiAvailable = !string.IsNullOrWhiteSpace(idEndpoint) && !string.IsNullOrWhiteSpace(idHeader);

                // if App Service MSI is not available then Azure VM IMDS may be available, test with a probe request
                if (!isAppServicesMsiAvailable)
                {
                    using (var internalTokenSource = new CancellationTokenSource())
                    using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(internalTokenSource.Token, cancellationToken))
                    {
                        HttpRequestMessage imdsProbeRequest = new HttpRequestMessage(HttpMethod.Get, ActiveDirectoryAuthentication.AZURE_IMDS_REST_URL);

                        try
                        {
                            internalTokenSource.CancelAfter(_azureVmImdsProbeTimeout);
                            await httpClient.SendAsync(imdsProbeRequest, linkedTokenSource.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // request to IMDS timed out (internal cancellation token canceled), neither Azure VM IMDS nor App Services MSI are available
                            if (internalTokenSource.Token.IsCancellationRequested)
                            {
                                //throw new SqlException(ConnectionString, resource, authority,
                                //    $"{AzureServiceTokenProviderException.ManagedServiceIdentityUsed} {AzureServiceTokenProviderException.MsiEndpointNotListening}");
                                throw;
                            }

                            throw;
                        }
                    }
                }

                // If managed identity is specified, include client ID parameter in request
                string clientIdParameter = _managedIdentityClientId != default
                    ? $"&client_id={_managedIdentityClientId}"
                    : string.Empty;

                // Craft request as per the MSI protocol
                var requestUrl = isAppServicesMsiAvailable
                    ? $"{idEndpoint}?resource={resource}{clientIdParameter}&api-version=2019-08-01"
                    : $"{ActiveDirectoryAuthentication.AZURE_IMDS_REST_URL}?resource={resource}{clientIdParameter}&api-version=2018-02-01";

                Func<HttpRequestMessage> getRequestMessage = () =>
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                    if (isAppServicesMsiAvailable)
                    {
                        request.Headers.Add("X-IDENTITY-HEADER", idHeader);
                    }
                    else
                    {
                        request.Headers.Add("Metadata", "true");
                    }

                    return request;
                };

                HttpResponseMessage response;
                try
                {
                    response = await SendAsyncWithRetry(httpClient, getRequestMessage, _retryTimeoutInSeconds, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    //throw new AzureServiceTokenProviderException(ConnectionString, resource, authority,
                    //$"{AzureServiceTokenProviderException.ManagedServiceIdentityUsed} {AzureServiceTokenProviderException.RetryFailure} {AzureServiceTokenProviderException.MsiEndpointNotListening}");
                    throw;
                }

                // If the response is successful, it should have JSON response with an access_token field
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    // Parse the JSON response
                    dynamic json = JValue.Parse(jsonResponse);
                    return new SqlAuthenticationToken(json.AccessToken, DateTime.FromFileTime(json.ExpiresOn));
                }

                string errorStatusDetail = IsRetryableStatusCode()
                    ? AzureServiceTokenProviderException.RetryFailure
                    : AzureServiceTokenProviderException.NonRetryableError;

                string errorText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                throw new Exception($"{errorStatusDetail} MSI ResponseCode: {response.StatusCode}, Response: {errorText}");
            }
            catch (Exception exp)
            {
                if (exp is SqlException)
                    throw;

                throw;
                //throw new AzureServiceTokenProviderException(ConnectionString, resource, authority,
                //    $"{AzureServiceTokenProviderException.ManagedServiceIdentityUsed} {AzureServiceTokenProviderException.GenericErrorMessage} {exp.Message}");
            }
        }

        private async Task<HttpResponseMessage> SendAsyncWithRetry(HttpClient httpClient, Func<HttpRequestMessage> getRequestMessage, int retryTimeoutInSeconds, CancellationToken cancellationToken)
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
