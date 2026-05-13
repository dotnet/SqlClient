// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient;

internal static partial class Interop
{
    internal static partial class Kernel32
    {
        /// <summary>
        /// Retrieves the window handle used by the console associated with the calling process.
        /// </summary>
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();
    }
}
