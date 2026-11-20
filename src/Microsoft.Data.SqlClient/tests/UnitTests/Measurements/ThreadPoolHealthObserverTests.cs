// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Threading;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Measurements;

/// <summary>Verifies observer failure and recovery without depending on SQL or ThreadPool load.</summary>
public class ThreadPoolHealthObserverTests
{
    /// <summary>A callback that never runs remains visible after the bounded grace period.</summary>
    [Fact]
    public void Complete_OutstandingProbe_PreservesCensoredSnapshot()
    {
        // Arrange
        using var offered = new ManualResetEventSlim();
        using var observer = new ThreadPoolHealthObserver(_ => offered.Set(), recoveryMilliseconds: 0);
        observer.Start();
        Assert.True(offered.Wait(TimeSpan.FromSeconds(10)));

        // Act
        AsyncHealthWindow result = observer.Complete();

        // Assert
        Assert.True(result.Probes.Outstanding > 0);
        Assert.All(result.Probes.Samples, p => Assert.Null(p.ExecutedMilliseconds));
        Assert.Same(result, observer.Complete());
    }

    /// <summary>A background exception survives as failed evidence rather than an empty green report.</summary>
    [Fact]
    public void Complete_DispatchFault_ExportsObserverFailure()
    {
        // Arrange
        using var offered = new ManualResetEventSlim();
        using var observer = new ThreadPoolHealthObserver(_ =>
        {
            offered.Set();
            throw new InvalidOperationException();
        }, recoveryMilliseconds: 0);
        observer.Start();
        Assert.True(offered.Wait(TimeSpan.FromSeconds(10)));

        // Act
        AsyncHealthWindow result = observer.Complete();

        // Assert
        Assert.Equal(typeof(InvalidOperationException).FullName, result.ObserverError);
        Assert.Equal(1, result.Probes.Rejected);
        Assert.Equal(0, result.Probes.Outstanding);
    }
}
#endif
