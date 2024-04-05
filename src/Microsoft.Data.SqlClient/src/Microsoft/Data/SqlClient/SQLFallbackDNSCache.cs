// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Net;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SQLFallbackDNSCache
    {
        private static readonly SQLFallbackDNSCache _SQLFallbackDNSCache = new SQLFallbackDNSCache();
        private static readonly int initialCapacity = 101;   // give some prime number here according to MSDN docs. It will be resized if reached capacity. 
        private ConcurrentDictionary<string, SQLDNSInfo> DNSInfoCache;

        // singleton instance
        public static SQLFallbackDNSCache Instance { get { return _SQLFallbackDNSCache; } }

        private SQLFallbackDNSCache()
        {
            int level = 4 * Environment.ProcessorCount;
            DNSInfoCache = new ConcurrentDictionary<string, SQLDNSInfo>(concurrencyLevel: level,
                                                                            capacity: initialCapacity,
                                                                            comparer: StringComparer.OrdinalIgnoreCase);
        }

        internal bool AddDNSInfo(SQLDNSInfo item)
        {
            if (null != item)
            {
#if NET6_0_OR_GREATER || NETSTANDARD2_1
                DNSInfoCache.AddOrUpdate(item.FQDN, static (key, state) => state, static (key, value, state) => state, item);
#else
                DNSInfoCache.AddOrUpdate(item.FQDN, item, (key, value) => item);
#endif
                return true;
            }

            return false;
        }

        internal bool DeleteDNSInfo(string FQDN)
        {
            return DNSInfoCache.TryRemove(FQDN, out _);
        }

        internal bool GetDNSInfo(string FQDN, out SQLDNSInfo result)
        {
            return DNSInfoCache.TryGetValue(FQDN, out result);
        }

        internal bool IsDuplicate(SQLDNSInfo newItem)
        {
            if (null != newItem)
            {
                SQLDNSInfo oldItem;
                if (GetDNSInfo(newItem.FQDN, out oldItem))
                {
                    return (newItem.CachedIPv4Address == oldItem.CachedIPv4Address &&
                            newItem.CachedIPv6Address == oldItem.CachedIPv6Address &&
                            newItem.Port == oldItem.Port);
                }
            }

            return false;
        }
    }

    internal sealed class SQLDNSInfo
    {
        public string FQDN { get; set; }
        public IPAddress CachedIPv4Address { get; set; }
        public IPAddress CachedIPv6Address { get; set; }
        public int Port { get; set; }
        public IPAddress[] SpeculativeIPAddresses { get; set; }

        internal SQLDNSInfo(string fqdn, IPAddress ipv4, IPAddress ipv6, int port)
        {
            FQDN = fqdn;
            CachedIPv4Address = ipv4;
            CachedIPv6Address = ipv6;
            Port = port;
        }

        internal SQLDNSInfo(string fqdn, IPAddress[] speculativeIPAddresses)
        {
            FQDN = fqdn;
            SpeculativeIPAddresses = speculativeIPAddresses;
        }
    }
}
