// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Microsoft.Data.SqlClientX.Handlers.TransportCreation
{
    /// <summary>
    /// Comparer that sorts IP addresses based on the version of the internet protocol it is using.
    /// This class cannot be instantiated, so to use it, use the singleton instances (doubleton?)
    /// <see cref="InstanceV4"/> and <see cref="InstanceV6"/>.
    /// </summary>
    internal sealed class IpAddressVersionSorter : IComparer<IPAddress>
    {
        private readonly bool _preferV6;

        private IpAddressVersionSorter(bool preferV6)
        {
            _preferV6 = preferV6;
        }

        /// <summary>
        /// Gets a singleton instance that ranks IPv4 addresses higher than IPv6 addresses.
        /// </summary>
        public static IpAddressVersionSorter InstanceV4 { get; } = new IpAddressVersionSorter(false);

        /// <summary>
        /// Gets a singleton instance that ranks IPv6 addresses higher than IPv4 addresses.
        /// </summary>
        public static IpAddressVersionSorter InstanceV6 { get; } = new IpAddressVersionSorter(true);

        /// <inheritdoc />
        public int Compare(IPAddress x, IPAddress y)
        {
            if (x is null) { throw new ArgumentNullException(nameof(x)); }
            if (y is null) { throw new ArgumentNullException(nameof(y)); }

            if (x.AddressFamily == y.AddressFamily)
            {
                // Versions are the same, it's a tie.
                return 0;
            }

            return _preferV6
                ? x.AddressFamily is AddressFamily.InterNetworkV6 ? 1 : -1
                : x.AddressFamily is AddressFamily.InterNetwork ? 1 : -1;
        }
    }
}
