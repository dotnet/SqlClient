// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class LocalAppContextSwitchesTests
    {
        [Theory]
        [InlineData("SuppressInsecureTLSWarning", false)]
        [InlineData("LegacyRowVersionNullBehavior", false)]
        [InlineData("MakeReadAsyncBlocking", false)]
        [InlineData("UseMinimumLoginTimeout", true)]
        [InlineData("EnableMultiSubnetFailoverByDefault", false)]
        public void DefaultSwitchValue(string property, bool expectedDefaultValue)
        {
            var switchesType = typeof(SqlCommand).Assembly.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");

            var switchValue = (bool)switchesType.GetProperty(property, BindingFlags.Public | BindingFlags.Static).GetValue(null);

            Assert.Equal(expectedDefaultValue, switchValue);
        }
    }
}
