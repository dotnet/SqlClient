// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Diagnostics;

/// <summary>Validates the closed transport step inferred from structured error numbers.</summary>
public class ConnectionErrorAttributePolicyTests
{
    /// <summary>
    /// SNI and native OS error numbers map to a closed transport step; anything not backed by a
    /// known number is reported as unknown.
    /// </summary>
    [Theory]
    [InlineData((int)ConnectionErrorSource.Sni, 26, null, "instance_discovery")]
    [InlineData((int)ConnectionErrorSource.Sni, 31, null, "tls")]
    [InlineData((int)ConnectionErrorSource.Sni, 40, 11001, "name_resolution")]
    [InlineData((int)ConnectionErrorSource.Sni, 40, 10061, "socket_connect")]
    [InlineData((int)ConnectionErrorSource.Sni, 40, unchecked((int)0x80090325), "tls")]
    [InlineData((int)ConnectionErrorSource.OperatingSystem, 11004, null, "name_resolution")]
    [InlineData((int)ConnectionErrorSource.OperatingSystem, 10060, null, "socket_connect")]
    [InlineData((int)ConnectionErrorSource.Sni, 40, 5, "unknown")]
    [InlineData((int)ConnectionErrorSource.SqlServer, 11001, null, "unknown")]
    public void TransportStep_StructuredNumbers_ReturnsClosedStep(
        int source, int code, int? nativeCode, string expected)
    {
        // Arrange
        ConnectionErrorEvidence evidence =
            ConnectionErrorEvidence.Create(
                (ConnectionErrorSource)source, code, nativeCode, "connection.transport");

        // Act
        string step = ConnectionErrorAttributePolicy.TransportStep(evidence);

        // Assert
        Assert.Equal(expected, step);
    }

    /// <summary>Missing evidence cannot infer a transport step.</summary>
    [Fact]
    public void TransportStep_NullEvidence_ReturnsUnknown()
    {
        // Act
        string step = ConnectionErrorAttributePolicy.TransportStep(null);

        // Assert
        Assert.Equal("unknown", step);
    }
}
