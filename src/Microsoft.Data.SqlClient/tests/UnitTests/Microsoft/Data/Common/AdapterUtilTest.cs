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
