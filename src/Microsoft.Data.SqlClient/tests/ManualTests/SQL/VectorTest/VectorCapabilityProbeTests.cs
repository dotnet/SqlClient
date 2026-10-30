// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

/// <summary>
/// Verifies that optional float16 capability failures leave useful, redacted diagnostics.
/// </summary>
[Trait("Set", "3")]
public sealed class VectorCapabilityProbeTests
{
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
        using StringWriter output = new();
        TextWriter originalOutput = Console.Out;
        bool supported;
        try
        {
            Console.SetOut(output);
            supported = DataTestUtility.CheckVectorFloat16Supported(builder.ConnectionString);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        Assert.False(supported);
        string diagnostic = output.ToString();
        Assert.Contains("Float16 vector support probe", diagnostic);
        Assert.Matches(@"SQL error -?\d+ \(state \d+, class \d+\)", diagnostic);
        Assert.Contains("tests will be skipped", diagnostic);
        Assert.DoesNotContain(marker, diagnostic);
    }
}
