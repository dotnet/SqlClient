// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals;
using Xunit;

using static Microsoft.Data.SqlClient.ManualTesting.Tests.DataTestUtility;
using static Microsoft.Data.SqlClient.ManualTesting.Tests.DNSCachingTest;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class ConfigurableIpPreferenceTest
    {
        private const string CnnPrefIPv6 = ";IPAddressPreference=IPv6First";
        private const string CnnPrefIPv4 = ";IPAddressPreference=IPv4First";
        private const string LocalHost = "localhost";

        private static bool IsTCPConnectionStringSetup() => !string.IsNullOrEmpty(TCPConnectionString);
        private static bool IsValidDataSource()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(TCPConnectionString);
            int startIdx = builder.DataSource.IndexOf(':') + 1;
            int endIdx = builder.DataSource.IndexOf(',');
            string serverName;
            if (endIdx == -1)
            {
                serverName = builder.DataSource.Substring(startIdx);
            }
            else
            {
                serverName = builder.DataSource.Substring(startIdx, endIdx - startIdx);
            }
#if NET
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                LocalHost.Equals(serverName, StringComparison.OrdinalIgnoreCase))
            {
                // Skip the test for localhost on macOS as Docker (hosting SQL Server) doesn't support IPv6
                return false;
            }
#endif
            List<IPAddress> ipAddresses = Dns.GetHostAddresses(serverName).ToList();
            return ipAddresses.Exists(ip => ip.AddressFamily == AddressFamily.InterNetwork) &&
                    ipAddresses.Exists(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);
        }

        [ConditionalTheory(nameof(IsTCPConnectionStringSetup), nameof(IsValidDataSource))]
        [InlineData(CnnPrefIPv6)]
        [InlineData(CnnPrefIPv4)]
        [InlineData(";IPAddressPreference=UsePlatformDefault")]
        public void ConfigurableIpPreference(string ipPreference)
        {
            using (SqlConnection connection = new SqlConnection(TCPConnectionString + ipPreference
#if NETFRAMEWORK
                + ";TransparentNetworkIPResolution=false"   // doesn't support in .NET Core
#endif
                ))
            {
                connection.Open();
                Assert.Equal(ConnectionState.Open, connection.State);
                Tuple<string, string, string, string> DNSInfo = connection.GetSQLDNSInfo();
                if(ipPreference == CnnPrefIPv4)
                {
                    Assert.NotNull(DNSInfo.Item2); //IPv4
                    Assert.Null(DNSInfo.Item3); //IPv6
                }
                else if(ipPreference == CnnPrefIPv6)
                {
                    Assert.Null(DNSInfo.Item2);
                    Assert.NotNull(DNSInfo.Item3);
                }
                else
                {
                    Assert.True((DNSInfo.Item2 != null && DNSInfo.Item3 == null) || (DNSInfo.Item2 == null && DNSInfo.Item3 != null));
                }
            }
        }

        // Azure SQL Server doesn't support dual-stack IPv4 and IPv6 that is going to be supported by end of 2021.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DoesHostAddressContainBothIPv4AndIPv6), nameof(IsUsingManagedSNI))]
        [InlineData(CnnPrefIPv6)]
        [InlineData(CnnPrefIPv4)]
        public void ConfigurableIpPreferenceManagedSni(string ipPreference)
            => TestCachedConfigurableIpPreference(ipPreference, DNSCachingConnString);

        private void TestCachedConfigurableIpPreference(string ipPreference, string cnnString)
        {
            using (SqlConnection connection = new SqlConnection(cnnString + ipPreference))
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

                Assert.NotNull(dnsCacheEntry);
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
