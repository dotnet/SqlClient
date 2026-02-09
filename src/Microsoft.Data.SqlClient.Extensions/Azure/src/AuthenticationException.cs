// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure;

/// <summary>
/// This exception is used internally by authentication providers to signal
/// authentication failures. It is not exposed publicly.
/// </summary>
internal class AuthenticationException : SqlAuthenticationProviderException
{
    /// <summary>
    /// Construct with just a method and message.  Other properties are set to
    /// defaults per the base class.
    /// </summary>
    /// <param name="method">The authentication method.</param>
    /// <param name="message">The error message.</param>
    internal AuthenticationException(
        SqlAuthenticationMethod method,
        string message)
    : base($"Failed to acquire access token for {method}: {message}", null)
    {
    }

    /// <summary>
    /// Construct with all properties specified. See the base class for details.
    /// </summary>
    /// <param name="method">The authentication method.</param>
    /// <param name="failureCode">The failure code.</param>
    /// <param name="shouldRetry">Whether the operation should be retried.</param>
    /// <param name="retryPeriod">The retry period.</param>
    /// <param name="message">The error message.</param>
    /// <param name="causedBy">The exception that caused this error.</param>
    internal AuthenticationException(
        SqlAuthenticationMethod method,
        string failureCode,
        bool shouldRetry,
        int retryPeriod,
        string message,
        Exception? causedBy = null)
    : base(
        method,
        failureCode,
        shouldRetry,
        retryPeriod,
        $"Failed to acquire access token for {method}: {message}",
        causedBy)
    {
    }
}
