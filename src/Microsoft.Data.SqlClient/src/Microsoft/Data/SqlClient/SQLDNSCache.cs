// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Net;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SQLDNSCache
    {
        // These should be prime numbers according to MSDN docs. ConcurrentDictionary will be resized if the capacity is reached.
        // SqlBrowserCacheCapacity doesn't need to be very large at all. SSRP writes to this as part of resolving an instance name
        // to a port, so the rest of the connection process doesn't need to 
        private const int FallbackCacheCapacity = 101;
        private const int SqlBrowserCacheCapacity = 11;

        private static readonly SQLDNSCache s_sqlFallbackDNSCache = new(FallbackCacheCapacity);
        private static readonly SQLDNSCache s_sqlBrowserDNSCache = new(SqlBrowserCacheCapacity);

        private readonly ConcurrentDictionary<string, SQLDNSInfo> _dnsInfoCache;

        // singleton instance
        public static SQLDNSCache Instance => s_sqlFallbackDNSCache;

        public static SQLDNSCache InterimInstance => s_sqlFallbackDNSCache;

        private SQLDNSCache(int initialCapacity)
        {
            int level = 4 * Environment.ProcessorCount;
            _dnsInfoCache = new ConcurrentDictionary<string, SQLDNSInfo>(concurrencyLevel: level,
                                                                            capacity: initialCapacity,
                                                                            comparer: StringComparer.OrdinalIgnoreCase);
        }

        internal bool AddDNSInfo(SQLDNSInfo item)
        {
            if (null != item)
            {
#if NET6_0_OR_GREATER || NETSTANDARD2_1
                _dnsInfoCache.AddOrUpdate(item.FQDN, static (key, state) => state, static (key, value, state) => state, item);
#else
                _dnsInfoCache.AddOrUpdate(item.FQDN, item, (key, value) => item);
#endif
                return true;
            }

            return false;
        }

        internal bool DeleteDNSInfo(string FQDN)
        {
            return _dnsInfoCache.TryRemove(FQDN, out _);
        }

        internal bool GetDNSInfo(string FQDN, out SQLDNSInfo result)
        {
            return _dnsInfoCache.TryGetValue(FQDN, out result);
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
