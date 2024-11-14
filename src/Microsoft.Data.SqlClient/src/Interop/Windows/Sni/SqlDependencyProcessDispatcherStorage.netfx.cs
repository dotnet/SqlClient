// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Interop.Windows.Sni
{
    internal unsafe class SqlDependencyProcessDispatcherStorage
    {
        private static void* s_data;
        private static int s_size;
        private static volatile int s_lock; // Int used for a spin-lock.

        [ResourceExposure(ResourceScope.Process)] // SxS: there is no way to set scope = Instance, using Process which is wider
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        public static byte[] NativeGetData()
        {
            IntPtr ptr = (IntPtr)s_data;
            
            byte[] result = null;
            if (ptr != IntPtr.Zero)
            {
                result = new byte[s_size];
                Marshal.Copy(ptr, result, 0, s_size);
            }

            return result;
        }

        [ResourceExposure(ResourceScope.Process)] // SxS: there is no way to set scope = Instance, using Process which is wider
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        internal static void NativeSetData(byte[] data)
        {
            fixed (byte* pDispatcher = data)
            {
                while (Interlocked.CompareExchange(ref s_lock, 1, 0) != 0)
                {
                    // Spin until we have the lock.
                    Thread.Sleep(50); // Sleep with short-timeout to prevent starvation.
                }
                Trace.Assert(s_lock == 1); // Now that we have the lock, lock should be equal to 1.

                if (s_data == null)
                {
                    s_data = Marshal.AllocHGlobal(data.Length).ToPointer();

                    Trace.Assert(s_data != null);

                    Buffer.MemoryCopy(pDispatcher, s_data, data.Length, data.Length);

                    Trace.Assert(0 == s_size); // Size should still be zero at this point.
                    s_size = data.Length;
                }

                int result = Interlocked.CompareExchange(ref s_lock, 0, 1);
                Trace.Assert(1 == result); // The release of the lock should have been successful.  
            }
        }
    }
}

#endif
