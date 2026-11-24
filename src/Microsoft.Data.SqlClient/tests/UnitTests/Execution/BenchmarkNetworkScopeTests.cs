// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using Microsoft.Data.SqlClient.PerformanceTests;
using Microsoft.Data.SqlClient.UnitTests.Measurements;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Execution;

/// <summary>Validates endpoint/condition evidence without modifying host networking.</summary>
[Collection(AsyncHealthBenchmarkCollection.Name)]
public class BenchmarkNetworkScopeTests
{
    /// <summary>TCP endpoints retain their explicit port and IPv6 host.</summary>
    [Theory]
    [InlineData("tcp:localhost,1434", "localhost", 1434)]
    [InlineData("localhost", "localhost", 1433)]
    [InlineData("tcp:[::1],1433", "::1", 1433)]
    public void ParseEndpoint_TcpEndpoint_PreservesTarget(string value, string host, int port)
    {
        // Act
        var target = BenchmarkNetworkScope.ParseEndpoint(value);

        // Assert
        Assert.Equal(host, target.Server);
        Assert.Equal(port, target.Port);
    }

    /// <summary>Unsupported transport/discovery cannot become a silently unshaped run.</summary>
    [Theory]
    [InlineData("lpc:localhost")]
    [InlineData("np:localhost")]
    [InlineData("localhost\\instance")]
    [InlineData("localhost,0")]
    [InlineData("localhost,65536")]
    public void ParseEndpoint_UnsupportedEndpoint_Throws(string value)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => BenchmarkNetworkScope.ParseEndpoint(value));
    }

    /// <summary>Missing or mismatched confirmation must not allow a shaped benchmark to run.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("101")]
    public void ReadAppliedRtt_UnconfirmedRequest_Throws(string? confirmed)
    {
        // Arrange
        const string variable = BenchmarkProcessRunner.AppliedRttVariable;
        string? previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, confirmed);
        try
        {
            // Act
            Exception? error = Record.Exception(() =>
                BenchmarkNetworkScope.ReadAppliedRtt(new RunnerJob { AdditionalRttMilliseconds = 100 }));

            // Assert
            Assert.IsType<InvalidOperationException>(error);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }
}
#endif
