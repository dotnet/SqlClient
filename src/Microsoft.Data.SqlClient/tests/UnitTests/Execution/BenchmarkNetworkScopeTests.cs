// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.IO;
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

    /// <summary>Requested latency requires evidence that the runner actually installed that profile.</summary>
    [Theory]
    [InlineData(0, false, "Disabled")]
    [InlineData(100, false, "Applied")]
    [InlineData(100, true, "Failed")]
    [InlineData(101, true, "Applied")]
    public void ReadAppliedRtt_MismatchedEvidence_Throws(int rtt, bool applied, string status)
    {
        // Arrange
        string? previous = Environment.GetEnvironmentVariable(BenchmarkProcessRunner.NetworkVariable);
        string path = Path.GetTempFileName();
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new
        {
            AdditionalRttMilliseconds = rtt, Applied = applied, Status = status
        }));
        Environment.SetEnvironmentVariable(BenchmarkProcessRunner.NetworkVariable, path);
        try
        {
            // Act / Assert
            Assert.Throws<InvalidOperationException>(() =>
                BenchmarkNetworkScope.ReadAppliedRtt(new RunnerJob { AdditionalRttMilliseconds = 100 }));
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable(BenchmarkProcessRunner.NetworkVariable, previous);
            File.Delete(path);
        }
    }
}
#endif
