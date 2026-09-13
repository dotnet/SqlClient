// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
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
    /// Verifies that the explicit list of preferences stays in sync with the enum definition.
    /// </summary>
    [Fact]
    public void AllPreferences_MatchesEnumValues()
    {
        SqlConnectionIPAddressPreference[] expected = Enum.GetValues(typeof(SqlConnectionIPAddressPreference))
            .Cast<SqlConnectionIPAddressPreference>()
            .OrderBy(value => value)
            .ToArray();

        Assert.Equal(expected, IpAddressPreferenceUtilities.AllPreferences.OrderBy(value => value));
    }
}
