// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Microsoft.Data.Common.UnitTests;

/// <summary>
/// Tests for <see cref="IpAddressPreferenceUtilities"/>.
/// </summary>
public class IpAddressPreferenceUtilitiesTest
{
    /// <summary>
    /// Verifies that each enum value's name converts back to that value, so the explicitly listed names
    /// stay in sync with the enum definition.
    /// </summary>
    [Fact]
    public void TryConvertToIPAddressPreference_AcceptsEveryEnumValueName()
    {
        foreach (SqlConnectionIPAddressPreference value in Enum.GetValues(typeof(SqlConnectionIPAddressPreference)))
        {
            Assert.True(IpAddressPreferenceUtilities.TryConvertToIPAddressPreference(value.ToString(), out SqlConnectionIPAddressPreference result), $"{value} is missing.");
            Assert.Equal(value, result);
        }
    }
}
