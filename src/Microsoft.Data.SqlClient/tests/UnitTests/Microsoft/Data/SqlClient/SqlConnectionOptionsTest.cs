// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;
#if NETFRAMEWORK
using SqlConnectionInternal = global::Microsoft.Data.SqlClient.Connection.SqlConnectionInternal;
#endif

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    /// <summary>
    /// Verifies connection-string defaults and explicit overrides of AppContext settings.
    /// </summary>
    [Collection(AppContextSwitchTestCollection.Name)]
    public class SqlConnectionOptionsTest : IDisposable
    {
        // Ensure we restore the original app context switch values after each
        // test.
        private readonly LocalAppContextSwitchesHelper _appContextSwitchHelper = new();

        public void Dispose()
        {
            _appContextSwitchHelper.Dispose();
        }

#if NETFRAMEWORK
        /// <summary>
        /// TNIR is disabled by default, while explicit keywords override either switch setting.
        /// </summary>
        [Theory]
        [InlineData("test.database.windows.net", true, true, true)]
        [InlineData("test.database.windows.net", false, true, false)]
        [InlineData("test.database.windows.net", null, true, false)]
        [InlineData("test.database.windows.net", true, false, true)]
        [InlineData("test.database.windows.net", false, false, false)]
        [InlineData("test.database.windows.net", null, false, true)]
        [InlineData("test.database.windows.net", true, null, true)]
        [InlineData("test.database.windows.net", false, null, false)]
        [InlineData("test.database.windows.net", null, null, false)]
        [InlineData("my.test.server", true, true, true)]
        [InlineData("my.test.server", false, true, false)]
        [InlineData("my.test.server", null, true, false)]
        [InlineData("my.test.server", true, false, true)]
        [InlineData("my.test.server", false, false, false)]
        [InlineData("my.test.server", null, false, true)]
        [InlineData("my.test.server", true, null, true)]
        [InlineData("my.test.server", false, null, false)]
        [InlineData("my.test.server", null, null, false)]
        public void TestDefaultTnir(string dataSource, bool? tnirEnabledInConnString, bool? tnirDisabledAppContext, bool expectedValue)
        {
            // Note: TNIR is only supported on .NET Framework.
            // Note: TNIR is disabled by default for Azure SQL Database servers (i.e. *.database.windows.net)
            // and when using federated auth unless explicitly set in the connection string.
            // However, this evaluation only happens at login time so TNIR behavior may not match
            // the value of TransparentNetworkIPResolution property in SqlConnectionOptions.

            // Arrange
            _appContextSwitchHelper.DisableTnirByDefault = tnirDisabledAppContext;

            // Act
            SqlConnectionStringBuilder builder = new();
            builder.DataSource = dataSource;
            if (tnirEnabledInConnString.HasValue)
            {
#pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                builder.TransparentNetworkIPResolution = tnirEnabledInConnString.Value;
#pragma warning restore 618
            }
            SqlConnectionOptions connectionString = new(builder.ConnectionString);

            // Assert
#pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
            Assert.Equal(expectedValue, builder.TransparentNetworkIPResolution);
#pragma warning restore 618
            Assert.Equal(expectedValue, connectionString.TransparentNetworkIPResolution);
        }

        /// <summary>
        /// Resetting the obsolete TNIR keyword restores the configured default on .NET Framework.
        /// </summary>
        [Theory]
        [InlineData(null, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void TransparentNetworkIPResolution_Reset_RestoresDefault(bool? disabledAppContext, bool expectedValue)
        {
            _appContextSwitchHelper.DisableTnirByDefault = disabledAppContext;
#pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
            SqlConnectionStringBuilder builder = new() { TransparentNetworkIPResolution = !expectedValue };

            Assert.True(builder.Remove("TransparentNetworkIPResolution"));
            Assert.Equal(expectedValue, builder.TransparentNetworkIPResolution);
            Assert.Empty(builder.ConnectionString);

            builder.TransparentNetworkIPResolution = !expectedValue;
            builder.Clear();
            Assert.Equal(expectedValue, builder.TransparentNetworkIPResolution);
#pragma warning restore 618
            Assert.Empty(builder.ConnectionString);
        }

        /// <summary>
        /// TNIR is disabled by default whenever federated authentication is in play, including when
        /// the token is supplied directly through <c>AccessToken</c> or <c>AccessTokenCallback</c>,
        /// unless the user explicitly specified the TNIR keyword.
        /// </summary>
        [Theory]
        // Non-Azure endpoint, no explicit TNIR keyword, no token: TNIR stays enabled.
        [InlineData("my.test.server", false, null, false)]
        // Non-Azure endpoint, no explicit TNIR keyword, token supplied: TNIR is disabled.
        [InlineData("my.test.server", true, null, true)]
        // Azure endpoint always disables TNIR when the keyword is absent.
        [InlineData("test.database.windows.net", false, null, true)]
        [InlineData("test.database.windows.net", true, null, true)]
        // An explicit TNIR keyword always wins, regardless of access token or endpoint. Note that
        // this method only decides whether the default is overridden; an explicit false is honoured
        // by the caller through SqlConnectionOptions.TransparentNetworkIPResolution itself.
        [InlineData("my.test.server", true, true, false)]
        [InlineData("test.database.windows.net", true, true, false)]
        [InlineData("test.database.windows.net", false, true, false)]
        [InlineData("my.test.server", true, false, false)]
        [InlineData("my.test.server", false, false, false)]
        [InlineData("test.database.windows.net", true, false, false)]
        [InlineData("test.database.windows.net", false, false, false)]
        public void TestShouldDisableTnirWithCallerSuppliedToken(
            string dataSource,
            bool isAccessTokenProvided,
            bool? tnirInConnString,
            bool expectedValue)
        {
            SqlConnectionStringBuilder builder = new() { DataSource = dataSource };
            if (tnirInConnString.HasValue)
            {
#pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                builder.TransparentNetworkIPResolution = tnirInConnString.Value;
#pragma warning restore 618
            }

            SqlConnectionOptions connectionOptions = new(builder.ConnectionString);

            Assert.Equal(
                expectedValue,
                SqlConnectionInternal.ShouldDisableTnir(connectionOptions, isAccessTokenProvided));
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
        [InlineData(true, null, true)]
        [InlineData(false, null, false)]
        [InlineData(null, null, true)]
        public void TestDefaultMultiSubnetFailover(bool? msfInConnString, bool? msfEnabledAppContext, bool expectedValue)
        {
            _appContextSwitchHelper.EnableMultiSubnetFailoverByDefault = msfEnabledAppContext;

            SqlConnectionStringBuilder builder = new();
            if (msfInConnString.HasValue)
            {
                builder.MultiSubnetFailover = msfInConnString.Value;
            }
            SqlConnectionOptions connectionString = new(builder.ConnectionString);

            Assert.Equal(expectedValue, builder.MultiSubnetFailover);
            Assert.Equal(expectedValue, connectionString.MultiSubnetFailover);
        }

        /// <summary>
        /// Removing or clearing an explicit value restores the current default without serializing it.
        /// </summary>
        [Theory]
        [InlineData(null, true)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void MultiSubnetFailover_Reset_RestoresDefault(bool? enabledAppContext, bool expectedValue)
        {
            _appContextSwitchHelper.EnableMultiSubnetFailoverByDefault = enabledAppContext;
            SqlConnectionStringBuilder builder = new() { MultiSubnetFailover = !expectedValue };

            Assert.True(builder.Remove("MultiSubnetFailover"));
            Assert.Equal(expectedValue, builder.MultiSubnetFailover);
            Assert.Empty(builder.ConnectionString);

            builder.MultiSubnetFailover = !expectedValue;
            builder.Clear();
            Assert.Equal(expectedValue, builder.MultiSubnetFailover);
            Assert.Empty(builder.ConnectionString);
        }

        /// <summary>
        /// Mirroring requires MSF to be disabled explicitly or through the compatibility opt-out.
        /// </summary>
        [Theory]
        [InlineData(null, null, true)]
        [InlineData(true, null, true)]
        [InlineData(null, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, null, false)]
        [InlineData(false, true, false)]
        [InlineData(null, false, false)]
        public void TestMultiSubnetFailoverWithFailoverPartner(
            bool? msfInConnString, bool? msfEnabledAppContext, bool shouldThrow)
        {
            _appContextSwitchHelper.EnableMultiSubnetFailoverByDefault = msfEnabledAppContext;

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "server",
                FailoverPartner = "partner",
                InitialCatalog = "database"
            };

            if (msfInConnString.HasValue)
            {
                builder.MultiSubnetFailover = msfInConnString.Value;
            }

            if (shouldThrow)
            {
                Assert.Throws<ArgumentException>(() => new SqlConnectionOptions(builder.ConnectionString));
            }
            else
            {
                SqlConnectionOptions options = new(builder.ConnectionString);
                Assert.False(options.MultiSubnetFailover);
                Assert.Equal("partner", options.FailoverPartner);
            }
        }
    }
}
