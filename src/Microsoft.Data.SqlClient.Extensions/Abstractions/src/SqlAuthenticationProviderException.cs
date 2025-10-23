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
        Exception? causedBy = null)
    : base(message, causedBy)
    {
        Method = SqlAuthenticationMethod.NotSpecified;
        FailureCode = Unknown;
        ShouldRetry = false;
        RetryPeriod = 0;
    }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/ctor2/*'/>
    protected SqlAuthenticationProviderException(
        SqlAuthenticationMethod method,
        string failureCode,
        bool shouldRetry,
        int retryPeriod,
        string message,
        Exception? causedBy = null)
    : base(message, causedBy)
    {
        Method = method;
        FailureCode = failureCode;
        ShouldRetry = shouldRetry;
        RetryPeriod = shouldRetry && retryPeriod > 0? retryPeriod : 0;
    }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/Method/*'/>
    public SqlAuthenticationMethod Method { get; }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/FailureCode/*'/>
    public string FailureCode { get; }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/ShouldRetry/*'/>
    public bool ShouldRetry { get; }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/RetryPeriod/*'/>
    public int RetryPeriod { get; }

    /// <include file='../doc/SqlAuthenticationProviderException.xml' path='docs/members[@name="SqlAuthenticationProviderException"]/ToString/*'/>
    public override string ToString()
    {
        return base.ToString() +
            $" Method={Method} FailureCode={FailureCode}" +
            $" ShouldRetry={ShouldRetry} RetryPeriod={RetryPeriod}";
    }

    private const string Unknown = "Unknown";
}
