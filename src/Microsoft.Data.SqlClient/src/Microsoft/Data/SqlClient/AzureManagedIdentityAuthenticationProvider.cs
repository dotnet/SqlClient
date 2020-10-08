// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    internal sealed class AzureManagedIdentityAuthenticationProvider : SqlAuthenticationProvider
    {
        // HttpClient is intended to be instantiated once and re-used throughout the life of an application.
#if NETFRAMEWORK
        private static readonly HttpClient s_httpClient = new HttpClient();
#else
        private static readonly HttpClient s_httpClient = new HttpClient(new HttpClientHandler() { CheckCertificateRevocationList = true });
#endif

        private const string AzureSystemApiVersion = "&api-version=2019-08-01";
        private const string AzureVmImdsApiVersion = "&api-version=2018-02-01";
        private const string AccessToken = "access_token";
        private const string Expiry = "expires_on";
        private const int FileTimeLength = 10;

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

        // Reference: https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/how-to-use-vm-token#get-a-token-using-http
        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            HttpClient httpClient = s_httpClient;

            try
            {
                // Check if App Services MSI is available. If both these environment variables are set, then it is.
                string msiEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
                string msiHeader = Environment.GetEnvironmentVariable("IDENTITY_HEADER");

                var isAppServicesMsiAvailable = !string.IsNullOrWhiteSpace(msiEndpoint) && !string.IsNullOrWhiteSpace(msiHeader);

                // if App Service MSI is not available then Azure VM IMDS may be available, test with a probe request
                if (!isAppServicesMsiAvailable)
                {
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | This environment is not identified as an Azure App Service environment. Proceeding to validate Azure VM IMDS endpoint availability.");
                    using (var internalTokenSource = new CancellationTokenSource())
                    using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(internalTokenSource.Token, default))
                    {
                        HttpRequestMessage imdsProbeRequest = new HttpRequestMessage(HttpMethod.Get, AzureVmImdsEndpoint);

                        try
                        {
                            internalTokenSource.CancelAfter(_azureVmImdsProbeTimeout);
                            await httpClient.SendAsync(imdsProbeRequest, linkedTokenSource.Token).ConfigureAwait(false);
                            SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | The Instance Metadata Service (IMDS) service endpoint is accessible. Proceeding to acquire access token.");
                        }
                        catch (OperationCanceledException)
                        {
                            // request to IMDS timed out (internal cancellation token canceled), neither Azure VM IMDS nor App Services MSI are available
                            if (internalTokenSource.Token.IsCancellationRequested)
                            {
                                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | The Instance Metadata Service (IMDS) service endpoint is not accessible.");
                                // Throw error: Tried to get token using Managed Identity. Unable to connect to the Instance Metadata Service (IMDS). Skipping request to the Managed Service Identity (MSI) token endpoint.
                                throw SQL.Azure_ManagedIdentityException($"{Strings.Azure_ManagedIdentityUsed} {Strings.Azure_MetadataEndpointNotListening}");
                            }

                            throw;
                        }
                    }
                }
                else
                {
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | This environment is identified as an Azure App Service environment. Proceeding to acquire access token from Endpoint URL: {0}", msiEndpoint);
                }

                string objectIdParameter = string.Empty;

                // If user assigned managed identity is specified, include object ID parameter in request
                if (parameters.UserId != default)
                {
                    objectIdParameter = $"&object_id={Uri.EscapeDataString(parameters.UserId)}";
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Identity Object id received and will be used for acquiring access token {0}", parameters.UserId);
                }

                // Craft request as per the MSI protocol
                var requestUrl = isAppServicesMsiAvailable
                    ? $"{msiEndpoint}?resource={parameters.Resource}{objectIdParameter}{AzureSystemApiVersion}"
                    : $"{AzureVmImdsEndpoint}?resource={parameters.Resource}{objectIdParameter}{AzureVmImdsApiVersion}";

                HttpResponseMessage response = null;

                try
                {
                    response = await httpClient.SendAsyncWithRetry(getRequestMessage, _retryTimeoutInSeconds, _maxRetryCount, default).ConfigureAwait(false);
                    HttpRequestMessage getRequestMessage()
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
                    }
                }
                catch (HttpRequestException)
                {
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Failed after 5 retries. Unable to connect to the Managed Service Identity (MSI) endpoint.");
                    // Throw error: Tried to get token using Managed Service Identity. Failed after 5 retries. Unable to connect to the Managed Service Identity (MSI) endpoint. Please check that you are running on an Azure resource that has MSI setup.
                    throw SQL.Azure_ManagedIdentityException($"{Strings.Azure_ManagedIdentityUsed} {Strings.Azure_RetryFailure} {Strings.Azure_IdentityEndpointNotListening}");
                }

                // If the response is successful, it should have JSON response with an access_token field
                if (response.IsSuccessStatusCode)
                {
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Successful response received. Status Code {0}", response.StatusCode);
                    string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    int accessTokenStartIndex = jsonResponse.IndexOf(AccessToken) + AccessToken.Length + 3;
                    var imdsAccessToken = jsonResponse.Substring(accessTokenStartIndex, jsonResponse.IndexOf('"', accessTokenStartIndex) - accessTokenStartIndex);
                    var expiresin = jsonResponse.Substring(jsonResponse.IndexOf(Expiry) + Expiry.Length + 3, FileTimeLength);
                    DateTime expiryTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(long.Parse(expiresin));
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Access Token received. Expiry Time: {0}", expiryTime);
                    return new SqlAuthenticationToken(imdsAccessToken, expiryTime);
                }

                // RetryFailure : Failed after 5 retries.
                // NonRetryableError : Received a non-retryable error.
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Request to acquire access token failed with status code {0}", response.StatusCode);
                string errorStatusDetail = response.IsRetryableStatusCode()
                    ? Strings.Azure_RetryFailure
                    : Strings.Azure_NonRetryableError;

                string errorText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Error occurred while acquiring access token: {0} Identity Response Code: {1}, Response: {2}", errorStatusDetail, response.StatusCode, errorText);
                throw SQL.Azure_ManagedIdentityException($"{errorStatusDetail} Identity Response Code: {response.StatusCode}, Response: {errorText}");
            }
            catch (Exception exp)
            {
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Error occurred while acquiring access token: {0}", exp.Message);
                if (exp is SqlException)
                    throw;
                // Throw error: Access token could not be acquired. {exp.Message}
                throw SQL.Azure_ManagedIdentityException($"{Strings.Azure_ManagedIdentityUsed} {Strings.Azure_GenericErrorMessage} {exp.Message}");
            }
        }

        public override bool IsSupported(SqlAuthenticationMethod authentication)
        {
            return authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                || authentication == SqlAuthenticationMethod.ActiveDirectoryMSI;
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
                        catch (HttpRequestException e)
                        {
                            if (attempts == maxRetryCount)
                            {
                                throw;
                            }
                            SqlClientEventSource.Log.TryTraceEvent("SendAsyncWithRetry | Exception occurred {0} | Attempting retry: {1} of {2}", e.Message, attempts, maxRetryCount);
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
}
