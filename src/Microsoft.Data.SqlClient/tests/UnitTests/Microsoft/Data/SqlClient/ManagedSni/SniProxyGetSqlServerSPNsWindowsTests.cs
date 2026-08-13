// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Windows-focused unit tests for managed SNI SPN generation behavior.
    /// </summary>
    public sealed class SniProxyGetSqlServerSPNsWindowsTests
    {
        /// <summary>
        /// Verifies Protocol.None uses the SSRP-resolved port in the generated SPN
        /// for a named instance on Windows.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void GetSqlServerSPNs_ProtocolNone_WithResolvedPort_UsesPort_OnWindows()
        {
            DataSource dataSource = DataSource.ParseServerName(@"localhost\instance");
            Assert.NotNull(dataSource);

            // Mirror post-SSRP state by injecting a resolved TCP port.
            dataSource.ResolvedPort = 12345;

            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            // Protocol.None should resolve to a port-based SPN for named instances.
            Assert.Contains(":12345", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies Protocol.TCP uses the resolved port in the generated SPN
        /// for a named instance on Windows.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void GetSqlServerSPNs_ProtocolTcp_WithResolvedPort_UsesPort_OnWindows()
        {
            DataSource dataSource = DataSource.ParseServerName(@"tcp:localhost\instance");
            Assert.NotNull(dataSource);

            // Mirror post-SSRP state by injecting a resolved TCP port.
            dataSource.ResolvedPort = 54321;

            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            // TCP protocol should use a port postfix and not the instance name.
            Assert.Contains(":54321", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies Protocol.Admin (DAC) uses the resolved port in the generated SPN
        /// for a named instance on Windows.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void GetSqlServerSPNs_ProtocolAdmin_WithResolvedPort_UsesPort_OnWindows()
        {
            DataSource dataSource = DataSource.ParseServerName(@"admin:localhost\instance");
            Assert.NotNull(dataSource);

            // Mirror post-SSRP state by injecting a resolved DAC port.
            dataSource.ResolvedPort = 11111;

            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            // Admin protocol follows TCP-like SPN formatting with a port postfix.
            Assert.Contains(":11111", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, StringComparison.OrdinalIgnoreCase);
        }
    }
}

#endif
