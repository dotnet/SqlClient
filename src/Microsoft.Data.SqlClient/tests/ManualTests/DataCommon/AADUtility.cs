// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class AADUtility
    {
        public static async Task<string> AzureActiveDirectoryAuthenticationCallback(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);
            if (result == null)
            {
                throw new Exception($"Failed to retrieve an access token for {resource}");
            }

            return result.AccessToken;
        }

        public static async Task<string> GetManagedIdentityToken(string objectId = null) =>
            await new MockManagedIdentityTokenProvider().AcquireTokenAsync(objectId).ConfigureAwait(false);

    }

    #region Mock Managed Identity Token Provider
    internal class MockManagedIdentityTokenProvider
    {
        // HttpClient is intended to be instantiated once and re-used throughout the life of an application.
#if NETFRAMEWORK
        private static readonly HttpClient s_defaultHttpClient = new HttpClient();
#else
        private static readonly HttpClient s_defaultHttpClient = new HttpClient(new HttpClientHandler() { CheckCertificateRevocationList = true });
#endif

        private const string AzureVmImdsApiVersion = "&api-version=2018-02-01";
        private const string AccessToken = "access_token";
        private const string Resource = "https://database.windows.net/";

        private const int DefaultRetryTimeout = 0;
        private const int DefaultMaxRetryCount = 5;

        // Azure Instance Metadata Service (IMDS) endpoint
        private const string AzureVmImdsEndpoint = "http://169.254.169.254/metadata/identity/oauth2/token";

        // Timeout for Azure IMDS probe request
        internal const int AzureVmImdsProbeTimeoutInSeconds = 2;
        internal readonly TimeSpan _azureVmImdsProbeTimeout = TimeSpan.FromSeconds(AzureVmImdsProbeTimeoutInSeconds);

        // Configurable timeout for MSI retry logic
        internal readonly int _retryTimeoutInSeconds = DefaultRetryTimeout;
        internal readonly int _maxRetryCount = DefaultMaxRetryCount;

        public async Task<string> AcquireTokenAsync(string objectId = null)
        {
            // Use the httpClient specified in the constructor. If it was not specified in the constructor, use the default httpClient.
            HttpClient httpClient = s_defaultHttpClient;

            try
            {
                // If user assigned managed identity is specified, include object ID parameter in request
                string objectIdParameter = objectId != null
                    ? $"&object_id={objectId}"
                    : string.Empty;

                // Craft request as per the MSI protocol
                var requestUrl = $"{AzureVmImdsEndpoint}?resource={Resource}{objectIdParameter}{AzureVmImdsApiVersion}";

                HttpResponseMessage response = null;

                try
                {
                    response = await httpClient.SendAsyncWithRetry(getRequestMessage, _retryTimeoutInSeconds, _maxRetryCount, default).ConfigureAwait(false);
                    HttpRequestMessage getRequestMessage()
                    {
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                        request.Headers.Add("Metadata", "true");
                        return request;
                    }
                }
                catch (HttpRequestException)
                {
                    // Not throwing exception if Access Token cannot be fetched. Tests will be disabled.
                    return null;
                }

                // If the response is successful, it should have JSON response with an access_token field
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    int accessTokenStartIndex = jsonResponse.IndexOf(AccessToken) + AccessToken.Length + 3;
                    return jsonResponse.Substring(accessTokenStartIndex, jsonResponse.IndexOf('"', accessTokenStartIndex) - accessTokenStartIndex);
                }

                // RetryFailure : Failed after 5 retries.
                // NonRetryableError : Received a non-retryable error.
                string errorStatusDetail = response.IsRetryableStatusCode()
                    ? "Failed after 5 retries"
                    : "Received a non-retryable error.";

                string errorText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Not throwing exception if Access Token cannot be fetched. Tests will be disabled.
                return null;
            }
            catch (Exception)
            {
                // Not throwing exception if Access Token cannot be fetched. Tests will be disabled.
                return null;
            }
        }
    }

    #region IMDS Retry Helper
    internal static class SqlManagedIdentityRetryHelper
    {
        internal const int DeltaBackOffInSeconds = 2;
        internal const string RetryTimeoutError = "Reached retry timeout limit set by MsiRetryTimeout parameter in connection string.";

        internal static bool IsRetryableStatusCode(this HttpResponseMessage response)
        {
            // 404 NotFound, 429 TooManyRequests, and 5XX server error status codes are retryable
            return Regex.IsMatch(((int)response.StatusCode).ToString(), @"404|429|5\d{2}");
        }

        /// <summary>
        /// Implements recommended retry guidance here: https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-to-use-vm-token#retry-guidance
        /// </summary>
        internal static async Task<HttpResponseMessage> SendAsyncWithRetry(this HttpClient httpClient, Func<HttpRequestMessage> getRequest, int retryTimeoutInSeconds, int maxRetryCount, CancellationToken cancellationToken)
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

                            if (response.IsSuccessStatusCode || !response.IsRetryableStatusCode() || attempts == maxRetryCount)
                            {
                                break;
                            }
                        }
                        catch (HttpRequestException)
                        {
                            if (attempts == maxRetryCount)
                            {
                                throw;
                            }
                        }

                        // use recommended exponential backoff strategy, and use linked token wait handle so caller or retry timeout is still able to cancel
                        backoffTimeInSecs += (int)Math.Pow(DeltaBackOffInSeconds, attempts);
                        linkedTokenSource.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(backoffTimeInSecs));
                        linkedTokenSource.Token.ThrowIfCancellationRequested();
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
    #endregion
    #endregion
}

