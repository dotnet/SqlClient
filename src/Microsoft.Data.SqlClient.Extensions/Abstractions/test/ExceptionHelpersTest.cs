// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies registration and activation do not mask fatal failures through wrappers.</summary>
public class ExceptionHelpersTest
{
    /// <summary>Fatal runtime failures remain non-recoverable, directly and through reflection.</summary>
    [Theory]
    [InlineData(typeof(OutOfMemoryException))]
    [InlineData(typeof(StackOverflowException))]
    [InlineData(typeof(AccessViolationException))]
    [InlineData(typeof(ThreadAbortException))]
    public void FatalFailuresAreNotRecoverable(Type type)
    {
        var failure = (Exception)Activator.CreateInstance(type, nonPublic: true)!;

        Assert.False(ExceptionHelpers.IsRecoverableException(failure));
        Assert.False(ExceptionHelpers.IsRecoverableException(
            new TargetInvocationException(new InvalidOperationException("Wrapper", failure))));
    }

    /// <summary>All aggregate branches are examined even after traversing ordinary subtrees.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FatalAggregateBranchIsNotRecoverable(bool fatalFirst)
    {
        var fatal = new OutOfMemoryException("Simulated fatal activation failure");
        var ordinary = new AggregateException(new FormatException("Ordinary failure"),
            new ArgumentException("Ordinary failure"));
        var aggregate = fatalFirst
            ? new AggregateException(fatal, ordinary)
            : new AggregateException(ordinary, fatal);

        Assert.False(ExceptionHelpers.IsRecoverableException(new TargetInvocationException(aggregate)));
    }

    /// <summary>Ordinary nested failures and empty aggregates remain recoverable.</summary>
    [Fact]
    public void OrdinaryFailuresAreRecoverable()
    {
        Assert.True(ExceptionHelpers.IsRecoverableException(new AggregateException()));
        Assert.True(ExceptionHelpers.IsRecoverableException(
            new ArgumentException("Wrapper", new FormatException("Ordinary failure"))));
        Assert.True(ExceptionHelpers.IsRecoverableException(new AggregateException(
            new InvalidOperationException("Ordinary failure"),
            new TargetInvocationException(new AggregateException(
                new FormatException("Ordinary failure"))))));
    }
}
