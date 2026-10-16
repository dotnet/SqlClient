// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;
using System.Reflection;
using System.Threading;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies fatal errors are never treated as recoverable, including through exception wrappers.</summary>
public class ExceptionHelpersTest
{
    /// <summary>Configuration wrappers preserve whether their underlying failure is recoverable.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigurationErrorHonorsInnerFailure(bool fatal)
    {
        Exception failure = fatal
            ? new OutOfMemoryException("Test fatal configuration failure")
            : new InvalidOperationException("Ordinary configuration failure");

        Assert.Equal(!fatal, ExceptionHelpers.IsRecoverableException(
            new ConfigurationErrorsException("Invalid configuration", failure)));
    }

    /// <summary>Fatal runtime errors remain non-recoverable even when wrapped by other exceptions.</summary>
    [Theory]
    [InlineData(typeof(OutOfMemoryException), false)]
    [InlineData(typeof(OutOfMemoryException), true)]
    [InlineData(typeof(StackOverflowException), false)]
    [InlineData(typeof(StackOverflowException), true)]
    [InlineData(typeof(AccessViolationException), false)]
    [InlineData(typeof(AccessViolationException), true)]
    [InlineData(typeof(ThreadAbortException), false)]
    [InlineData(typeof(ThreadAbortException), true)]
    public void FatalExceptionIsNotRecoverable(Type exceptionType, bool wrapped)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, nonPublic: true)!;
        if (wrapped)
        {
            exception = new TargetInvocationException(new InvalidOperationException("Wrapper", exception));
        }

        Assert.False(ExceptionHelpers.IsRecoverableException(exception));
    }

    /// <summary>Ordinary failures remain recoverable throughout an inner-exception chain.</summary>
    [Fact]
    public void OrdinaryExceptionChainIsRecoverable()
    {
        var exception = new ArgumentException("Wrapper",
            new InvalidOperationException("Wrapper", new FormatException("Invalid format")));

        Assert.True(ExceptionHelpers.IsRecoverableException(exception));
        Assert.True(ExceptionHelpers.IsRecoverableException(new FormatException("Invalid format")));
    }

    /// <summary>Fatal failures in later aggregate entries remain non-recoverable through nested wrappers.</summary>
    [Theory]
    [InlineData(typeof(OutOfMemoryException), false)]
    [InlineData(typeof(OutOfMemoryException), true)]
    [InlineData(typeof(StackOverflowException), false)]
    [InlineData(typeof(StackOverflowException), true)]
    [InlineData(typeof(AccessViolationException), false)]
    [InlineData(typeof(AccessViolationException), true)]
    [InlineData(typeof(ThreadAbortException), false)]
    [InlineData(typeof(ThreadAbortException), true)]
    public void AggregateWithFatalExceptionIsNotRecoverable(Type exceptionType, bool nested)
    {
        Exception failure = (Exception)Activator.CreateInstance(exceptionType, nonPublic: true)!;
        if (nested)
        {
            failure = new InvalidOperationException("Wrapper",
                new AggregateException(new FormatException("Ordinary failure"), failure));
        }
        var exception = new TargetInvocationException(
            new AggregateException(new InvalidOperationException("Ordinary failure"), failure));

        Assert.False(ExceptionHelpers.IsRecoverableException(exception));
    }

    /// <summary>Empty aggregates and nested aggregates containing only ordinary failures remain recoverable.</summary>
    [Fact]
    public void OrdinaryAggregateIsRecoverable()
    {
        var exception = new AggregateException(
            new InvalidOperationException("Ordinary failure"),
            new TargetInvocationException(new AggregateException(
                new FormatException("Ordinary failure"), new ArgumentException("Ordinary failure"))));

        Assert.True(ExceptionHelpers.IsRecoverableException(exception));
        Assert.True(ExceptionHelpers.IsRecoverableException(new AggregateException()));
    }
}
