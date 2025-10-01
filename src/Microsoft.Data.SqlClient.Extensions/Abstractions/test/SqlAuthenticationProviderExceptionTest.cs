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
            string action,
            string failureCode,
            bool shouldRetry,
            int retryPeriod,
            string message,
            Exception? causedBy)
        : base(action, failureCode, shouldRetry, retryPeriod, message, causedBy)
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

        Assert.Equal("Unknown", ex.Action);
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

        Assert.Equal("Unknown", ex.Action);
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
        var action = "action";
        var failureCode = "failure";
        var shouldRetry = true;
        var retryPeriod = 42;
        var message = "message";

        Error ex = new(
            action,
            failureCode,
            shouldRetry,
            retryPeriod,
            message,
            null);

        Assert.Equal(action, ex.Action);
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
        var action = "action";
        var failureCode = "failure";
        var shouldRetry = true;
        var retryPeriod = 42;
        var message = "message";
        var causedBy = new Exception("causedBy");

        Error ex = new(
            action,
            failureCode,
            shouldRetry,
            retryPeriod,
            message,
            causedBy);

        Assert.Equal(action, ex.Action);
        Assert.Equal(failureCode, ex.FailureCode);
        Assert.Equal(shouldRetry, ex.ShouldRetry);
        Assert.Equal(retryPeriod, ex.RetryPeriod);
        Assert.Equal(message, ex.Message);
        Assert.Equal(causedBy, ex.InnerException);
    }
}
