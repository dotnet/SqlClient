// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Data.Sql;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
#if NET6_0_OR_GREATER
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    public class SqlDataSourceEnumeratorTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsNotUsingManagedSNIOnWindows))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void SqlDataSourceEnumerator_NativeSNI()
        {
            // The returned rows depends on the running services which could be zero or more.
            int count = GetDSEnumerator().GetDataSources().Rows.Count;
            Assert.InRange(count, 0, 65536);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsUsingManagedSNI))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void SqlDataSourceEnumerator_ManagedSNI()
        {
            // After adding the managed SNI support, this test should have the same result as SqlDataSourceEnumerator_NativeSNI
            Assert.Throws<NotImplementedException>(() => GetDSEnumerator().GetDataSources());
        }

        private SqlDataSourceEnumerator GetDSEnumerator()
        {
            // SQL Server Browser runs as a Windows service.
            // TODO: This assessment can be done on CI.
            ServiceController[] services = ServiceController.GetServices(Environment.MachineName);
            ServiceController service = services.FirstOrDefault(s => s.ServiceName == "SQLBrowser");
            if (service != null)
            {
                Assert.Equal(ServiceControllerStatus.Running, service.Status);
            }
            return SqlDataSourceEnumerator.Instance;
        }
    }
}
