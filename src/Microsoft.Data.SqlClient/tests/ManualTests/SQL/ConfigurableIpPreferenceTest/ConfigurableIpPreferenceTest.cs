// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Xunit;

using static Microsoft.Data.SqlClient.ManualTesting.Tests.DataTestUtility;
using static Microsoft.Data.SqlClient.ManualTesting.Tests.DNSCachingTest;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class ConfigurableIpPreferenceTest
    {
        private const string CnnPrefIPv6 = ";IPAddressPreference=IPv6First";
        private const string CnnPrefIPv4 = ";IPAddressPreference=IPv4First";

        [ConditionalTheory(typeof(DataTestUtility), nameof(DoesHostAddressContainBothIPv4AndIPv6))]
        [InlineData(CnnPrefIPv6)]
        [InlineData(CnnPrefIPv4)]
        public void ConfigurableIpPreferenceManagedSni(string ipPreference)
        {
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            TestConfigurableIpPreference(ipPreference);
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", false);
        }

        private void TestConfigurableIpPreference(string ipPreference)
        {
            using (SqlConnection connection = new SqlConnection(DNSCachingConnString + ipPreference))
            {
                // each successful connection updates the dns cache entry for the data source
                connection.Open();
                var SQLFallbackDNSCacheInstance = GetDnsCache();

                // get the dns cache entry with the given key. parameters[1] will be initialized as the entry
                object[] parameters = new object[] { connection.DataSource, null };
                SQLFallbackDNSCacheGetDNSInfo.Invoke(SQLFallbackDNSCacheInstance, parameters);
                var dnsCacheEntry = parameters[1];

                const string AddrIPv4Property = "AddrIPv4";
                const string AddrIPv6Property = "AddrIPv6";
                const string FQDNProperty = "FQDN";

                Assert.Equal(connection.DataSource, GetPropertyValueFromCacheEntry(FQDNProperty, dnsCacheEntry));

                if (ipPreference == CnnPrefIPv4)
                {
                    Assert.NotNull(GetPropertyValueFromCacheEntry(AddrIPv4Property, dnsCacheEntry));
                    Assert.Null(GetPropertyValueFromCacheEntry(AddrIPv6Property, dnsCacheEntry));
                }
                else if (ipPreference == CnnPrefIPv6)
                {
                    string ipv6 = GetPropertyValueFromCacheEntry(AddrIPv6Property, dnsCacheEntry);
                    Assert.NotNull(ipv6);
                    Assert.Null(GetPropertyValueFromCacheEntry(AddrIPv4Property, dnsCacheEntry));
                }
            }

            object GetDnsCache() =>
               SQLFallbackDNSCacheType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);

            string GetPropertyValueFromCacheEntry(string property, object dnsCacheEntry) =>
               (string)SQLDNSInfoType.GetProperty(property).GetValue(dnsCacheEntry);
        }
    }
}
