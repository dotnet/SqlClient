// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SqlAuthenticationProviderExceptionTest
{
    #region Tests

    /// <summary>
    /// Verify that the minimal properties are set correctly, and defaults are
    /// used otherwise.  The causedBy argument is null.
    /// </summary>
    [Fact]
    public void Constructor_MinimalInfo_WithoutCausedBy()
    {
        var message = "message";

        Error ex = new(message, null);

        Assert.Equal(SqlAuthenticationMethod.NotSpecified, ex.Method);
        Assert.Equal("Unknown", ex.FailureCode);
        Assert.False(ex.ShouldRetry);
        Assert.Equal(0, ex.RetryPeriod);
        Assert.Equal(message, ex.Message);
        Assert.Null(ex.InnerException);
    }

    /// <summary>
    /// Verify that the minimal properties are set correctly, and defaults are
    /// used otherwise.  The causedBy argument is not null.
    /// </summary>
    [Fact]
    public void Constructor_MinimalInfo_WithCausedBy()
    {
        var message = "message";
        var causedBy = new Exception("causedBy");

        Error ex = new(message, causedBy);

        Assert.Equal(SqlAuthenticationMethod.NotSpecified, ex.Method);
        Assert.Equal("Unknown", ex.FailureCode);
        Assert.False(ex.ShouldRetry);
        Assert.Equal(0, ex.RetryPeriod);
        Assert.Equal(message, ex.Message);
        Assert.Same(causedBy, ex.InnerException);
    }

    /// <summary>
    /// Verify that all properties are set correctly.  The causedBy argument is
    /// null.
    /// </summary>
    [Fact]
    public void Constructor_AllInfo_WithoutCausedBy()
    {
        var method = SqlAuthenticationMethod.ActiveDirectoryIntegrated;
        var failureCode = "failure";
        var shouldRetry = true;
        var retryPeriod = 42;
        var message = "message";

        Error ex = new(
            method,
            failureCode,
            shouldRetry,
            retryPeriod,
            message,
            causedBy: null);

        Assert.Equal(method, ex.Method);
        Assert.Equal(failureCode, ex.FailureCode);
        Assert.Equal(shouldRetry, ex.ShouldRetry);
        Assert.Equal(retryPeriod, ex.RetryPeriod);
        Assert.Equal(message, ex.Message);
        Assert.Null(ex.InnerException);
    }

    /// <summary>
    /// Verify that all properties are set correctly.  The causedBy argument is
    /// not null.
    /// </summary>
    [Fact]
    public void Constructor_AllInfo_WithCausedBy()
    {
        var method = SqlAuthenticationMethod.ActiveDirectoryServicePrincipal;
        var failureCode = "failure";
        var shouldRetry = true;
        var retryPeriod = 42;
        var message = "message";
        var causedBy = new Exception("causedBy");

        Error ex = new(
            method,
            failureCode,
            shouldRetry,
            retryPeriod,
            message,
            causedBy);

        Assert.Equal(method, ex.Method);
        Assert.Equal(failureCode, ex.FailureCode);
        Assert.Equal(shouldRetry, ex.ShouldRetry);
        Assert.Equal(retryPeriod, ex.RetryPeriod);
        Assert.Equal(message, ex.Message);
        Assert.Same(causedBy, ex.InnerException);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Derive from SqlAuthenticationProviderException to test the abstract
    /// class' functionality.
    /// </summary>
    private class Error : SqlAuthenticationProviderException
    {
        /// <summary>
        /// Construct with minimal information.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="causedBy">The exception that caused this exception, or null if none.</param>
        public Error(string message, Exception? causedBy)
        : base(message, causedBy)
        {
        }

        /// <summary>
        /// Construct with all information..
        /// </summary>
        /// <param name="method">The authentication method.</param>
        /// <param name="failureCode">The failure code.</param>
        /// <param name="shouldRetry">Whether the operation should be retried.</param>
        /// <param name="retryPeriod">The retry period.</param>
        /// <param name="message">The exception message.</param>
        /// <param name="causedBy">The exception that caused this exception, or null if none.</param>
        public Error(
            SqlAuthenticationMethod method,
            string failureCode,
            bool shouldRetry,
            int retryPeriod,
            string message,
            Exception? causedBy)
        : base(method, failureCode, shouldRetry, retryPeriod, message, causedBy)
        {
        }
    }

    #endregion
}
