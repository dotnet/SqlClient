// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Threading;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies fatal errors are never treated as recoverable, including through exception wrappers.</summary>
public class ExceptionHelpersTest
{
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
}
