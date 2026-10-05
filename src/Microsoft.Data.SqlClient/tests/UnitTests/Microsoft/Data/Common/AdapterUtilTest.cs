// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Microsoft.Data.Common.UnitTests;

/// <summary>
/// Class containing tests of the various utility methods in <see cref="ADP"/>.
/// </summary>
public class AdapterUtilTest
{
    /// <summary>
    /// Gets a collection of test data representing the list of server TDS versions
    /// which are expected by the client.
    /// </summary>
    /// <see cref="ValidTdsVersion_ReturnsSuccessfully"/>
    public static TheoryData<uint> ValidTdsVersions => [
        // SQL Server 2005
        TdsEnums.SQL2005_VERSION,
        // SQL Server 2008 R2
        TdsEnums.SQL2008_VERSION,
        // SQL Server 2012+ (TDS 7.x)
        TdsEnums.TDS7X_VERSION,
        // SQL Server 2022+ (TDS 8.0)
        TdsEnums.TDS80_VERSION
        ];

    /// <summary>
    /// Gets a collection of test data representing the list of known server TDS versions
    /// which the client does not expect to receive and should reject.
    /// </summary>
    /// <see cref="InvalidTdsVersion_ThrowsInvalidOperationException"/>
    public static TheoryData<uint> InvalidTdsVersions => [
        // Empty TDS version
        0x00000000,
        // SQL Server 7.0
        0x07000000,
        // SQL Server 2000
        0x07010000,
        // SQL Server 2000 SP1
        0x71000001,
        // SQL Server 2008
        0x730A0003
        ];

    /// <summary>
    /// Combines the test data sets in <see cref="ValidTdsVersions"/> and <see cref="InvalidTdsVersions"/>.
    /// </summary>
    /// <see cref="TdsVersionValidation_AlignsWithLegacyValidation"/>
    public static TheoryData<uint> AllTdsVersions => [.. ValidTdsVersions, .. InvalidTdsVersions];

    /// <summary>
    /// Verifies that the client throws an InvalidOperationException in response to receiving an
    /// unsupported TDS protocol version.
    /// </summary>
    /// <param name="tdsVersion">
    /// The TDS protocol version to validate. Represents an unsupported or invalid version value.
    /// </param>
    [Theory]
    [MemberData(nameof(InvalidTdsVersions))]
    public void InvalidTdsVersion_ThrowsInvalidOperationException(uint tdsVersion)
    {
        Assert.Throws<InvalidOperationException>(() => ADP.ValidateTdsVersion(tdsVersion));
    }

    /// <summary>
    /// Verifies that the client accepts one of the supported TDS protocol versions without throwing
    /// an exception.
    /// </summary>
    /// <param name="tdsVersion">
    /// The TDS protocol version to validate. Represents a supported version value.
    /// </param>
    [Theory]
    [MemberData(nameof(ValidTdsVersions))]
    public void ValidTdsVersion_ReturnsSuccessfully(uint tdsVersion) =>
        ADP.ValidateTdsVersion(tdsVersion);

    /// <summary>
    /// Verifies that Azure Synapse Analytics dedicated SQL pool endpoints are recognised, and that
    /// serverless (on-demand) pools and unrelated endpoints are not.
    /// </summary>
    /// <remarks>
    /// Dedicated pools reject SET TRANSACTION ISOLATION LEVEL for every level except
    /// READ UNCOMMITTED, so the session isolation level reset performed on connection checkout skips
    /// them. Serverless pools accept the statement and must not be skipped.
    /// </remarks>
    [Theory]
    // Dedicated pools.
    [InlineData("myworkspace.sql.azuresynapse.net", true)]
    [InlineData("MYWORKSPACE.SQL.AZURESYNAPSE.NET", true)]
    [InlineData("tcp:myworkspace.sql.azuresynapse.net,1433", true)]
    [InlineData("myworkspace.sql.azuresynapse.net\\instance", true)]
    [InlineData("myworkspace.sql.azuresynapse.azure.cn", true)]
    [InlineData("myworkspace.sql.azuresynapse.usgovcloudapi.net", true)]
    [InlineData("myworkspace.privatelink.sql.azuresynapse.net", true)]
    [InlineData("tcp:MYWORKSPACE.privatelink.sql.azuresynapse.azure.cn,1433", true)]
    [InlineData("myworkspace.privatelink.sql.azuresynapse.usgovcloudapi.net\\instance", true)]
    [InlineData(" tcp:myworkspace.sql.azuresynapse.net. ,1433", true)]
    // Synapse is recognised by its host segment, so other cloud suffixes are also covered.
    [InlineData("myworkspace.sql.azuresynapse.cloud.example", true)]
    // Serverless / on-demand pools use the same suffix but carry an "-ondemand" workspace suffix.
    [InlineData("myworkspace-ondemand.sql.azuresynapse.net", false)]
    [InlineData("MYWORKSPACE-ONDEMAND.SQL.AZURESYNAPSE.NET", false)]
    [InlineData("tcp:myworkspace-ondemand.sql.azuresynapse.net,1433", false)]
    [InlineData("myworkspace-ondemand.privatelink.sql.azuresynapse.net", false)]
    [InlineData("tcp:MYWORKSPACE-ONDEMAND.privatelink.sql.azuresynapse.net,1433", false)]
    [InlineData("myworkspace-ondemand.privatelink.sql.azuresynapse.azure.cn", false)]
    [InlineData("myworkspace-ondemand.privatelink.sql.azuresynapse.usgovcloudapi.net\\instance", false)]
    [InlineData("myworkspace-ondemand.sql.azuresynapse.azure.cn", false)]
    [InlineData("myworkspace-ondemand.sql.azuresynapse.usgovcloudapi.net", false)]
    // Unrelated endpoints.
    [InlineData("myserver.database.windows.net", false)]
    [InlineData("myserver-ondemand.database.windows.net", false)]
    [InlineData("sql.azuresynapse.net", false)]
    // Only the host is inspected, and the Synapse segment must directly follow the workspace label.
    [InlineData("np:\\\\myworkspace.sql.azuresynapse.net\\pipe\\sql\\query", true)]
    [InlineData("evil.myworkspace.sql.azuresynapse.net", false)]
    [InlineData("myworkspace.evil.privatelink.sql.azuresynapse.net", false)]
    [InlineData("myserver.contoso.com\\myworkspace.sql.azuresynapse.net", false)]
    [InlineData("tcp:myserver.contoso.com,1433\\x.sql.azuresynapse.net", false)]
    [InlineData("np:\\\\myserver\\pipe\\x.sql.azuresynapse.net\\query", false)]
    [InlineData(".sql.azuresynapse.net", false)]
    [InlineData("localhost", false)]
    [InlineData("", false)]
    public void IsAzureSynapseDedicatedPoolEndpoint_ClassifiesDataSource(string dataSource, bool expected) =>
        Assert.Equal(expected, ADP.IsAzureSynapseDedicatedPoolEndpoint(dataSource));

    /// <summary>
    /// Verifies that <see cref="ADP.GetDataSourceHostRange"/> isolates only the host name from the
    /// supported data source forms.
    /// </summary>
    [Theory]
    [InlineData("myhost", "myhost")]
    [InlineData("  myhost  ", "myhost")]
    [InlineData("tcp:myhost", "myhost")]
    [InlineData("TCP: myhost ,1433", "myhost")]
    [InlineData("np:myhost", "myhost")]
    [InlineData("lpc:myhost", "myhost")]
    [InlineData("admin:myhost", "myhost")]
    [InlineData("myhost,1433", "myhost")]
    [InlineData("myhost\\instance", "myhost")]
    [InlineData("tcp:myhost\\instance,1433", "myhost")]
    [InlineData("np:\\\\myhost\\pipe\\sql\\query", "myhost")]
    [InlineData("myworkspace.sql.azuresynapse.net.", "myworkspace.sql.azuresynapse.net.")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("tcp:", "")]
    [InlineData(",1433", "")]
    [InlineData("\\\\myhost\\instance", "myhost")]
    public void GetDataSourceHostRange_ReturnsHost(string dataSource, string expectedHost)
    {
        ADP.GetDataSourceHostRange(dataSource, out int start, out int end);

        Assert.InRange(start, 0, dataSource.Length);
        Assert.InRange(end, start, dataSource.Length);
        Assert.Equal(expectedHost, dataSource.Substring(start, end - start));
    }

    /// <summary>
    /// Verifies that <see cref="ADP.TrimRange"/> trims whitespace only within the given range.
    /// </summary>
    [Theory]
    [InlineData("abc", 0, 3, "abc")]
    [InlineData("  abc  ", 0, 7, "abc")]
    [InlineData(" a b ", 0, 5, "a b")]
    [InlineData("x  abc  y", 1, 8, "abc")]
    [InlineData("    ", 0, 4, "")]
    [InlineData("", 0, 0, "")]
    public void TrimRange_TrimsWhitespaceWithinRange(string value, int start, int end, string expected)
    {
        ADP.TrimRange(value, ref start, ref end);

        Assert.Equal(expected, value.Substring(start, end - start));
    }

    /// <summary>
    /// Verifies that <see cref="ADP.EndsWithOrdinalIgnoreCase"/> compares only the given range.
    /// </summary>
    [Theory]
    [InlineData("myworkspace-ondemand", 0, 20, "-ondemand", true)]
    [InlineData("MYWORKSPACE-ONDEMAND", 0, 20, "-ondemand", true)]
    [InlineData("myworkspace-ondemand.x", 0, 20, "-ondemand", true)]
    [InlineData("myworkspace-ondemand.x", 0, 22, "-ondemand", false)]
    [InlineData("-ondemand", 1, 9, "-ondemand", false)]
    [InlineData("abc", 0, 3, "", true)]
    [InlineData("", 0, 0, "x", false)]
    public void EndsWithOrdinalIgnoreCase_ComparesRange(string value, int start, int end, string suffix, bool expected) =>
        Assert.Equal(expected, ADP.EndsWithOrdinalIgnoreCase(value, start, end, suffix));

    /// <summary>
    /// Verifies that the SqlClient v7.1+ TDS version validation logic implemented in ADP.ValidateTdsVersion
    /// is consistent with the legacy validation logic.
    /// </summary>
    /// <param name="tdsVersion">
    /// The TDS protocol version to validate. Represents a version value to be checked against the legacy
    /// validation logic.
    /// </param>
    [Theory]
    [MemberData(nameof(AllTdsVersions))]
    public void TdsVersionValidation_AlignsWithLegacyValidation(uint tdsVersion)
    {
        Action validation = () => ADP.ValidateTdsVersion(tdsVersion);

        if (LegacyVersionValidation(tdsVersion))
        {
            validation();
        }
        else
        {
            Assert.Throws<InvalidOperationException>(validation);
        }
    }

    private static bool LegacyVersionValidation(uint tdsVersion)
    {
        const int SQL2005_MAJOR = 0x72;
        const int SQL2008_MAJOR = 0x73;
        const int SQL2012_MAJOR = 0x74;
        const int TDS8_MAJOR = 0x08;

        const int SQL2005_INCREMENT = 0x09;
        const int SQL2008_INCREMENT = 0x0b;
        const int SQL2012_INCREMENT = 0x00;
        const int TDS8_INCREMENT = 0x00;

        const int SQL2005_RTM_MINOR = 0x0002;
        const int SQL2008_MINOR = 0x0003;
        const int SQL2012_MINOR = 0x0004;
        const int TDS8_MINOR = 0x00;

        uint majorMinor = tdsVersion & 0xff00ffff;
        uint increment = (tdsVersion >> 16) & 0xff;

        switch (majorMinor)
        {
            case SQL2005_MAJOR << 24 | SQL2005_RTM_MINOR:
                if (increment != SQL2005_INCREMENT)
                {
                    return false;
                }
                return true;
            case SQL2008_MAJOR << 24 | SQL2008_MINOR:
                if (increment != SQL2008_INCREMENT)
                {
                    return false;
                }
                return true;
            case SQL2012_MAJOR << 24 | SQL2012_MINOR:
                if (increment != SQL2012_INCREMENT)
                {
                    return false;
                }
                return true;
            case TDS8_MAJOR << 24 | TDS8_MINOR:
                if (increment != TDS8_INCREMENT)
                {
                    return false;
                }
                return true;
            default:
                return false;
        }
    }
}
