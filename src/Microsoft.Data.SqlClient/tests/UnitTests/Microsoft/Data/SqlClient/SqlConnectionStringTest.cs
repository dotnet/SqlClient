// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;
using static Microsoft.Data.SqlClient.Tests.Common.LocalAppContextSwitchesHelper;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    public class SqlConnectionStringTest : IDisposable
    {
        private LocalAppContextSwitchesHelper _appContextSwitchHelper;
        public SqlConnectionStringTest()
        {
            // Ensure that the app context switch is set to the default value
            _appContextSwitchHelper = new LocalAppContextSwitchesHelper();
        }

#if NETFRAMEWORK
        [Theory]
        [InlineData("test.database.windows.net", true, Tristate.True, true)]
        [InlineData("test.database.windows.net", false, Tristate.True, false)]
        [InlineData("test.database.windows.net", null, Tristate.True, false)]
        [InlineData("test.database.windows.net", true, Tristate.False, true)]
        [InlineData("test.database.windows.net", false, Tristate.False, false)]
        [InlineData("test.database.windows.net", null, Tristate.False, true)]
        [InlineData("test.database.windows.net", true, Tristate.NotInitialized, true)]
        [InlineData("test.database.windows.net", false, Tristate.NotInitialized, false)]
        [InlineData("test.database.windows.net", null, Tristate.NotInitialized, true)]
        [InlineData("my.test.server", true, Tristate.True, true)]
        [InlineData("my.test.server", false, Tristate.True, false)]
        [InlineData("my.test.server", null, Tristate.True, false)]
        [InlineData("my.test.server", true, Tristate.False, true)]
        [InlineData("my.test.server", false, Tristate.False, false)]
        [InlineData("my.test.server", null, Tristate.False, true)]
        [InlineData("my.test.server", true, Tristate.NotInitialized, true)]
        [InlineData("my.test.server", false, Tristate.NotInitialized, false)]
        [InlineData("my.test.server", null, Tristate.NotInitialized, true)]
        public void TestDefaultTnir(string dataSource, bool? tnirEnabledInConnString, Tristate tnirDisabledAppContext, bool expectedValue)
        {
            // Note: TNIR is only supported on .NET Framework.
            // Note: TNIR is disabled by default for Azure SQL Database servers (i.e. *.database.windows.net)
            // and when using federated auth unless explicitly set in the connection string.
            // However, this evaluation only happens at login time so TNIR behavior may not match
            // the value of TransparentNetworkIPResolution property in SqlConnectionString.

            // Arrange
            _appContextSwitchHelper.DisableTnirByDefaultField = tnirDisabledAppContext;

            // Act
            SqlConnectionStringBuilder builder = new();
            builder.DataSource = dataSource;
            if (tnirEnabledInConnString.HasValue)
            {
                builder.TransparentNetworkIPResolution = tnirEnabledInConnString.Value;
            }
            SqlConnectionString connectionString = new(builder.ConnectionString);

            // Assert
            Assert.Equal(expectedValue, connectionString.TransparentNetworkIPResolution);
        }
#endif

        public void Dispose()
        {
            // Clean up any resources if necessary
            _appContextSwitchHelper.Dispose();
        }
    }
}
