// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Interop.Windows.Sni
{
    internal class SqlDependencyProcessDispatcherStorage
    {
        private static readonly object s_lockObj = new();
        private static IntPtr s_data;
        private static int s_size;

        [ResourceExposure(ResourceScope.Process)] // SxS: there is no way to set scope = Instance, using Process which is wider
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        public static byte[] NativeGetData()
        {
            byte[] result = null;
            if (s_data != IntPtr.Zero)
            {
                result = new byte[s_size];
                Marshal.Copy(s_data, result, 0, s_size);
            }

            return result;
        }

        [ResourceExposure(ResourceScope.Process)] // SxS: there is no way to set scope = Instance, using Process which is wider
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        internal static void NativeSetData(byte[] data)
        {
            lock (s_lockObj)
            {
                if (s_data == IntPtr.Zero)
                {
                    s_data = Marshal.AllocHGlobal(data.Length);
                    Trace.Assert(s_data != IntPtr.Zero);
                    
                    Marshal.Copy(data, 0, s_data, data.Length);
                    
                    Trace.Assert(s_size == 0); // Size should still be zero at this point
                    s_size = data.Length;
                }
            }
        }
    }
}

#endif
