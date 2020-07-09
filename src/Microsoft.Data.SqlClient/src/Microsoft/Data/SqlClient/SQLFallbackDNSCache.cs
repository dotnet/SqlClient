// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;

namespace Microsoft.Data.SqlClient
{
    internal class SQLFallbackDNSCache
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
                if (DNSInfoCache.ContainsKey(item.FQDN))
                {

                    DeleteDNSInfo(item.FQDN);
                }

                return DNSInfoCache.TryAdd(item.FQDN, item);
            }

            return false;
        }

        internal bool DeleteDNSInfo(string FQDN)
        {
            SQLDNSInfo value;
            return DNSInfoCache.TryRemove(FQDN, out value);
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
                    return (newItem.AddrIPv4 == oldItem.AddrIPv4 &&
                            newItem.AddrIPv6 == oldItem.AddrIPv6 &&
                            newItem.Port == oldItem.Port);
                }
            }

            return false;
        }
    }

    internal class SQLDNSInfo
    {
        public string FQDN { get; set; }
        public string AddrIPv4 { get; set; }
        public string AddrIPv6 { get; set; }
        public string Port { get; set; }

        internal SQLDNSInfo(string FQDN, string ipv4, string ipv6, string port)
        {
            this.FQDN = FQDN;
            AddrIPv4 = ipv4;
            AddrIPv6 = ipv6;
            Port = port;
        }
    }
}
