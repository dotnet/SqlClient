// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;
using System.Reflection;

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
        // ConfigurationManager can wrap fatal errors; classify the cause, not just its wrapper.
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
        // Construct exception objects without triggering the actual fatal runtime conditions.
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
        // Ordinary wrappers must not turn an otherwise recoverable failure into fatal state.
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
        // A fatal entry last, optionally behind another aggregate, must be found in a later branch.
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

    /// <summary>Fatal failures are found after the walk drains an ordinary subtree in another branch.</summary>
    [Theory]
    [InlineData(typeof(OutOfMemoryException))]
    [InlineData(typeof(StackOverflowException))]
    [InlineData(typeof(AccessViolationException))]
    [InlineData(typeof(ThreadAbortException))]
    public void FatalExceptionAfterOrdinarySubtreeIsNotRecoverable(Type exceptionType)
    {
        // The walk examines later aggregate entries first, so placing the fatal entry first forces
        // it to finish the ordinary subtree before reaching the fatal exception.
        Exception failure = (Exception)Activator.CreateInstance(exceptionType, nonPublic: true)!;
        var exception = new AggregateException(
            failure,
            new InvalidOperationException("Wrapper", new AggregateException(
                new FormatException("Ordinary failure"),
                new ArgumentException("Ordinary failure", new InvalidOperationException("Ordinary failure")))));

        Assert.False(ExceptionHelpers.IsRecoverableException(exception));
    }

    /// <summary>Empty aggregates and nested aggregates containing only ordinary failures remain recoverable.</summary>
    [Fact]
    public void OrdinaryAggregateIsRecoverable()
    {
        // Nested and empty aggregates are recoverable when none of their branches is fatal.
        var exception = new AggregateException(
            new InvalidOperationException("Ordinary failure"),
            new TargetInvocationException(new AggregateException(
                new FormatException("Ordinary failure"), new ArgumentException("Ordinary failure"))));

        Assert.True(ExceptionHelpers.IsRecoverableException(exception));
        Assert.True(ExceptionHelpers.IsRecoverableException(new AggregateException()));
    }
}
