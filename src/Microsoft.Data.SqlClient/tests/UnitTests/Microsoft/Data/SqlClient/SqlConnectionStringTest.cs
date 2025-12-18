using System;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    public class SqlConnectionStringTest : IDisposable
    {
        // Ensure we restore the original app context switch values after each
        // test.
        private LocalAppContextSwitchesHelper _appContextSwitchHelper = new();

        public void Dispose()
        {
            _appContextSwitchHelper.Dispose();
        }

#if NETFRAMEWORK
        [Theory]
        [InlineData("test.database.windows.net", true, true, true)]
        [InlineData("test.database.windows.net", false, true, false)]
        [InlineData("test.database.windows.net", null, true, false)]
        [InlineData("test.database.windows.net", true, false, true)]
        [InlineData("test.database.windows.net", false, false, false)]
        [InlineData("test.database.windows.net", null, false, true)]
        [InlineData("test.database.windows.net", true, null, true)]
        [InlineData("test.database.windows.net", false, null, false)]
        [InlineData("test.database.windows.net", null, null, true)]
        [InlineData("my.test.server", true, true, true)]
        [InlineData("my.test.server", false, true, false)]
        [InlineData("my.test.server", null, true, false)]
        [InlineData("my.test.server", true, false, true)]
        [InlineData("my.test.server", false, false, false)]
        [InlineData("my.test.server", null, false, true)]
        [InlineData("my.test.server", true, null, true)]
        [InlineData("my.test.server", false, null, false)]
        [InlineData("my.test.server", null, null, true)]
        public void TestDefaultTnir(string dataSource, bool? tnirEnabledInConnString, bool? tnirDisabledAppContext, bool expectedValue)
        {
            // Note: TNIR is only supported on .NET Framework.
            // Note: TNIR is disabled by default for Azure SQL Database servers (i.e. *.database.windows.net)
            // and when using federated auth unless explicitly set in the connection string.
            // However, this evaluation only happens at login time so TNIR behavior may not match
            // the value of TransparentNetworkIPResolution property in SqlConnectionString.

            // Arrange
            _appContextSwitchHelper.DisableTnirByDefault = tnirDisabledAppContext;

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
        /// <summary>
        /// Test MSF values when set through connection string and through app context switch.
        /// </summary>
        [Theory]
        [InlineData(true, true, true)]
        [InlineData(false, true, false)]
        [InlineData(null, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, false, false)]
        [InlineData(null, false, false)]
        [InlineData(null, null, false)]
        public void TestDefaultMultiSubnetFailover(bool? msfInConnString, bool? msfEnabledAppContext, bool expectedValue)
        {
            _appContextSwitchHelper.EnableMultiSubnetFailoverByDefault = msfEnabledAppContext;

            SqlConnectionStringBuilder builder = new();
            if (msfInConnString.HasValue)
            {
                builder.MultiSubnetFailover = msfInConnString.Value;
            }
            SqlConnectionString connectionString = new(builder.ConnectionString);

            Assert.Equal(expectedValue, connectionString.MultiSubnetFailover);
        }

        /// <summary>
        /// Tests that MultiSubnetFailover=true cannot be used with FailoverPartner.
        /// </summary>
        [Fact]
        public void TestMultiSubnetFailoverWithFailoverPartnerThrows()
        {
            _appContextSwitchHelper.EnableMultiSubnetFailoverByDefault = true;

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "server",
                FailoverPartner = "partner",
                InitialCatalog = "database"
            };

            Assert.Throws<ArgumentException>(() => new SqlConnectionString(builder.ConnectionString));
        }
    }
}
