// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.DotNet.XUnitExtensions
{
    internal static class DiscovererHelpers
    {
        internal static bool TestPlatformApplies(TestPlatforms platforms) =>
#if NETCOREAPP
                (platforms.HasFlag(TestPlatforms.FreeBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD"))) ||
                (platforms.HasFlag(TestPlatforms.Linux) && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ||
                (platforms.HasFlag(TestPlatforms.NetBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD"))) ||
                (platforms.HasFlag(TestPlatforms.OSX) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ||
                (platforms.HasFlag(TestPlatforms.Windows) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
# else
                (platforms.HasFlag(TestPlatforms.Windows) && (int)Environment.OSVersion.Platform == 2) ||
                (platforms.HasFlag(TestPlatforms.Linux) && (int)Environment.OSVersion.Platform == 4) ||
                (platforms.HasFlag(TestPlatforms.OSX) && (int)Environment.OSVersion.Platform == 6);
#endif
    }
}
