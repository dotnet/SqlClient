// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{

    public class DNSCachingTest
    {
        public static Assembly systemData = Assembly.GetAssembly(typeof(SqlConnection));
        public static Type SQLFallbackDNSCacheType = systemData.GetType("Microsoft.Data.SqlClient.SQLFallbackDNSCache");
        public static Type SQLDNSInfoType = systemData.GetType("Microsoft.Data.SqlClient.SQLDNSInfo");
        public static MethodInfo SQLFallbackDNSCacheGetDNSInfo = SQLFallbackDNSCacheType.GetMethod("GetDNSInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        
        
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsDNSCachingSetup))]
        public void DNSCachingIsSupportedFlag()
        {
            string expectedDNSCachingSupportedCR = DataTestUtility.IsDNSCachingSupportedCR ? "true" : "false";
            string expectedDNSCachingSupportedTR = DataTestUtility.IsDNSCachingSupportedTR ? "true" : "false";

            using(SqlConnection connection = new SqlConnection(DataTestUtility.DNSCachingConnString))
            {
                connection.Open();

                IDictionary<string, object> dictionary = connection.RetrieveInternalInfo();
                bool ret = dictionary.TryGetValue("SQLDNSCachingSupportedState", out object val);
                ret = dictionary.TryGetValue("SQLDNSCachingSupportedStateBeforeRedirect", out object valBeforeRedirect);
                string isSupportedStateTR = (string)val;
                string isSupportedStateCR = (string)valBeforeRedirect;
                Assert.Equal(expectedDNSCachingSupportedCR, isSupportedStateCR);
                Assert.Equal(expectedDNSCachingSupportedTR, isSupportedStateTR);
            }
        }
        
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsDNSCachingSetup))]
        public void DNSCachingGetDNSInfo()
        {            
            using(SqlConnection connection = new SqlConnection(DataTestUtility.DNSCachingConnString))
            {
                connection.Open();
            }

            var SQLFallbackDNSCacheInstance = SQLFallbackDNSCacheType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            
            var serverList = new List<KeyValuePair<string, bool>>();
            serverList.Add(new KeyValuePair<string, bool>(DataTestUtility.DNSCachingServerCR, DataTestUtility.IsDNSCachingSupportedCR));
            serverList.Add(new KeyValuePair<string, bool>(DataTestUtility.DNSCachingServerTR, DataTestUtility.IsDNSCachingSupportedTR));
            
            foreach(var server in serverList)
            {
                object[] parameters;
                bool ret;

                if (!string.IsNullOrEmpty(server.Key))
                {
                    parameters = new object[] { server.Key, null };
                    ret = (bool)SQLFallbackDNSCacheGetDNSInfo.Invoke(SQLFallbackDNSCacheInstance, parameters);

                    if (server.Value)
                    {
                        Assert.NotNull(parameters[1]);
                        Assert.Equal(server.Key, (string)SQLDNSInfoType.GetProperty("FQDN").GetValue(parameters[1]));
                    }
                    else
                    {
                        Assert.Null(parameters[1]);
                    }
                }
            }
        }
    }
}
