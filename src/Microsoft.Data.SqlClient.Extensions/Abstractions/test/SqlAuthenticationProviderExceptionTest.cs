namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SqlAuthenticationProviderExceptionTest
{
    // Derive from SqlAuthenticationProviderException to test the abstract
    // class' functionality.
    private class Error : SqlAuthenticationProviderException
    {
        public Error(
            string message,
            Exception? causedBy)
        : base(message, causedBy)
        {
        }

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

    // Verify that the minimal properties are set correctly, and defaults are
    // used otherwise.  The causedBy argument is null.
    [Fact]
    public void Constructor_Minimal_WithoutCausedBy()
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

    // Verify that the minimal properties are set correctly, and defaults are
    // used otherwise.  The causedBy argument is not null.
    [Fact]
    public void Constructor_Minimal_WithCausedBy()
    {
        var message = "message";
        var causedBy = new Exception("causedBy");

        Error ex = new(message, causedBy);

        Assert.Equal(SqlAuthenticationMethod.NotSpecified, ex.Method);
        Assert.Equal("Unknown", ex.FailureCode);
        Assert.False(ex.ShouldRetry);
        Assert.Equal(0, ex.RetryPeriod);
        Assert.Equal(message, ex.Message);
        Assert.Equal(causedBy, ex.InnerException);
    }

    // Verify that all properties are set correctly.  The causedBy argument is
    // null.
    [Fact]
    public void Constructor_All_WithoutCausedBy()
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
            null);

        Assert.Equal(method, ex.Method);
        Assert.Equal(failureCode, ex.FailureCode);
        Assert.Equal(shouldRetry, ex.ShouldRetry);
        Assert.Equal(retryPeriod, ex.RetryPeriod);
        Assert.Equal(message, ex.Message);
        Assert.Null(ex.InnerException);
    }

    // Verify that all properties are set correctly.  The causedBy argument is
    // not null.
    [Fact]
    public void Constructor_All_WithCausedBy()
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
        Assert.Equal(causedBy, ex.InnerException);
    }
}
