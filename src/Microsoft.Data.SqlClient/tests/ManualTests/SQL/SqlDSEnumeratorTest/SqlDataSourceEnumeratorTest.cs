// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Data.Sql;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [Trait("Set", "2")]
    [PlatformSpecific(TestPlatforms.Windows)]
    public class SqlDataSourceEnumeratorTest
    {
        private static bool IsEnvironmentAvailable()
        {
            ServiceController[] services = ServiceController.GetServices(Environment.MachineName);
            ServiceController service = services.FirstOrDefault(s => s.ServiceName == "SQLBrowser");

            return DataTestUtility.IsNotManagedInstance() &&
                DataTestUtility.IsUsingNativeSNI() &&
                service != null &&
                service.Status == ServiceControllerStatus.Running;
        }

        // Managed Instance endpoints are resolved directly through DNS and cannot be discovered
        // through the SQL Browser service running on the test agent.
        [ConditionalFact(nameof(IsEnvironmentAvailable))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void SqlDataSourceEnumerator_NativeSNI()
        {
            // The returned rows depends on the running services which could be zero or more.
            int count = GetDSEnumerator().GetDataSources().Rows.Count;
            Assert.InRange(count, 0, 65536);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        // This validates client-side managed-SNI enumeration behavior and the agent's SQL Browser
        // environment; it does not connect to or discover the configured Managed Instance.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsUsingManagedSNI), nameof(DataTestUtility.IsNotManagedInstance))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void SqlDataSourceEnumerator_ManagedSNI()
        {
            // After adding the managed SNI support, this test should have the same result as SqlDataSourceEnumerator_NativeSNI
            Assert.Throws<NotImplementedException>(() => GetDSEnumerator().GetDataSources());
        }

        // This test validates behavior of SqlDataSourceConverter used to present instance names in PropertyGrid
        // with the SqlConnectionStringBuilder object presented in the control underneath.
        // Managed Instance endpoints are not enumerated through the test agent's SQL Browser service.
        [ConditionalFact(nameof(IsEnvironmentAvailable))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestDataSourceConverterGetStandardValues()
        {
            PropertyDescriptorCollection csbDescriptor = TypeDescriptor.GetProperties(typeof(SqlConnectionStringBuilder));
            PropertyDescriptor dataSourceDescriptor = csbDescriptor.Find(nameof(SqlConnectionStringBuilder.DataSource), false);
            TypeConverter converter = dataSourceDescriptor?.Converter;

            Assert.NotNull(dataSourceDescriptor);
            Assert.NotNull(converter);

            Assert.True(converter.GetStandardValuesSupported());
            Assert.False(converter.GetStandardValuesExclusive());

            System.Collections.ICollection dataSources = converter.GetStandardValues();
            int count = dataSources.Count;

            Assert.InRange(count, 0, 65536);
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
