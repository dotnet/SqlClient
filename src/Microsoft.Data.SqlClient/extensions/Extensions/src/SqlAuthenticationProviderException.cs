// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

public sealed class SqlAuthenticationProviderException : Exception
{
    public SqlAuthenticationProviderException(
        string action,
        string failureCode,
        string message,
        bool shouldRetry = false,
        int retryPeriod = 0)
    : this(true, action, failureCode, message, null, shouldRetry, retryPeriod)
    {
    }

    public SqlAuthenticationProviderException(
        string action,
        string failureCode,
        string message,
        Exception causedBy,
        bool shouldRetry = false,
        int retryPeriod = 0)
    : this(true, action, failureCode, message, causedBy, shouldRetry, retryPeriod)
    {
    }

    private SqlAuthenticationProviderException(
        // To differentiate from the public constructor that doesn't allow a
        // null causedBy.
        bool _,
        string action,
        string failureCode,
        string message,
        Exception? causedBy,
        bool shouldRetry,
        int retryPeriod)
    : base(message, causedBy)
    {
        Action = action;
        FailureCode = failureCode;
        ShouldRetry = shouldRetry;
        RetryPeriod = retryPeriod;
    }

    // The name of the authentication action that failed, or "Unknown" if not
    // known.
    public string Action { get; } = Unknown;

    // The failure code, or "Unknown" if not known.
    public string FailureCode { get; } = Unknown;

    // True if the action should be retried, false otherwise.
    public bool ShouldRetry { get; } = false;

    // The period of time, in milliseconds, to wait before retrying the action.
    //
    // Never negative.
    //
    // Undefined if ShouldRestart is false.
    public int RetryPeriod { get; } = 0;

    private const string Unknown = "Unknown";
}
