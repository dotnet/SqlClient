// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure;

// This exception is used internally by authentication providers to signal
// authentication failures. It is not exposed publicly.
internal class AuthenticationException : SqlAuthenticationProviderException
{
    // Construct with just a method and message.  Other properties are set to
    // defaults per the base class.
    internal AuthenticationException(
        SqlAuthenticationMethod method,
        string message)
    : base($"Failed to acquire access token for {method}: {message}", null)
    {
    }

    // Construct with all properties specified. See the base class for details.
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
