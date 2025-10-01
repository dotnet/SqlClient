// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/SqlAuthenticationProviderException/*'/>
public abstract class SqlAuthenticationProviderException : Exception
{
    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/ctor1/*'/>
    protected SqlAuthenticationProviderException(
        string message,
        Exception? causedBy)
    : base(message, causedBy)
    {
        Action = Unknown;
        FailureCode = Unknown;
        ShouldRetry = false;
        RetryPeriod = 0;
    }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/ctor2/*'/>
    protected SqlAuthenticationProviderException(
        string action,
        string failureCode,
        bool shouldRetry,
        int retryPeriod,
        string message,
        Exception? causedBy)
    : base(message, causedBy)
    {
        Action = action;
        FailureCode = failureCode;
        ShouldRetry = shouldRetry;
        RetryPeriod = retryPeriod;
    }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/Action/*'/>
    public string Action { get; }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/FailureCode/*'/>
    public string FailureCode { get; }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/ShouldRetry/*'/>
    public bool ShouldRetry { get; }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/RetryPeriod/*'/>
    public int RetryPeriod { get; }

    private const string Unknown = "Unknown";
}
