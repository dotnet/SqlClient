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
    internal sealed class IpAddressVersionComparer : IComparer<IPAddress>
    {
        private readonly AddressFamily _preferredAddressFamily;

        private IpAddressVersionComparer(AddressFamily preferredAddressFamily)
        {
            _preferredAddressFamily = preferredAddressFamily;
        }

        /// <summary>
        /// Gets a singleton instance that ranks IPv4 addresses higher than IPv6 addresses.
        /// </summary>
        public static IpAddressVersionComparer InstanceV4 { get; } =
            new IpAddressVersionComparer(AddressFamily.InterNetwork);

        /// <summary>
        /// Gets a singleton instance that ranks IPv6 addresses higher than IPv4 addresses.
        /// </summary>
        public static IpAddressVersionComparer InstanceV6 { get; } =
            new IpAddressVersionComparer(AddressFamily.InterNetworkV6);

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

            return x.AddressFamily == _preferredAddressFamily ? 1 : -1;
        }
    }
}
