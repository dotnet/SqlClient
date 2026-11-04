// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

/// <summary>
/// Verifies that optional float16 capability failures leave useful, redacted diagnostics.
/// </summary>
[Trait("Set", "3")]
public sealed class VectorCapabilityProbeTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Sends probe diagnostics to per-test output retained in the published TRX artifacts.
    /// </summary>
    /// <param name="output">The xUnit output sink for the current test.</param>
    public VectorCapabilityProbeTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Records the cached outcome used to select float16 tests, including a failure captured
    /// during discovery. This does not rerun the probe or require optional float16 support.
    /// </summary>
    [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup))]
    public void ConfiguredServerOutcomeIsRecorded()
    {
        bool supported = DataTestUtility.IsSqlVectorFloat16Supported;
        _output.WriteLine($"Configured server cached float16 support: {supported}");
        if (!supported)
        {
            _output.WriteLine(DataTestUtility.VectorFloat16ProbeDiagnostic
                ?? "Float16 probe was not attempted because vector prerequisites were unavailable.");
        }
    }

    /// <summary>
    /// Reports SQL failure details without exposing the connection string or changing the
    /// unavailable-capability result. The nonexistent database avoids modifying server objects.
    /// </summary>
    [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup))]
    public void SqlFailureReportsRedactedReason()
    {
        string marker = "VectorProbeMissing_" + Guid.NewGuid().ToString("N");
        SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
        {
            InitialCatalog = marker,
            ApplicationName = marker,
            ConnectRetryCount = 0,
            Pooling = false
        };
        string configuredDiagnostic = DataTestUtility.VectorFloat16ProbeDiagnostic;
        using StringWriter output = new();
        bool supported = DataTestUtility.CheckVectorFloat16Supported(
            builder.ConnectionString,
            message =>
            {
                output.WriteLine(message);
                _output.WriteLine(message);
            });

        Assert.False(supported);
        string diagnostic = output.ToString();
        Assert.Contains("Float16 vector support probe", diagnostic);
        Assert.Matches(@"SQL error -?\d+ \(state \d+, class \d+\)", diagnostic);
        Assert.Contains("tests will be skipped", diagnostic);
        Assert.DoesNotContain(marker, diagnostic);
        Assert.Equal(configuredDiagnostic, DataTestUtility.VectorFloat16ProbeDiagnostic);
    }
}
