// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Interop.Windows.Sni
{
    internal unsafe class SqlDependencyProcessDispatcherStorage
    {
        static void* data;

        static int size;
        static volatile int thelock; // Int used for a spin-lock.

        public static void* NativeGetData(out int passedSize)
        {
            passedSize = size;
            return data;
        }

        internal static bool NativeSetData(void* passedData, int passedSize)
        {
            bool success = false;

            while (0 != Interlocked.CompareExchange(ref thelock, 1, 0))
            { // Spin until we have the lock.
                Thread.Sleep(50); // Sleep with short-timeout to prevent starvation.
            }
            Trace.Assert(1 == thelock); // Now that we have the lock, lock should be equal to 1.

            if (data == null)
            {
                data = Marshal.AllocHGlobal(passedSize).ToPointer();

                Trace.Assert(data != null);

                System.Buffer.MemoryCopy(passedData, data, passedSize, passedSize);

                Trace.Assert(0 == size); // Size should still be zero at this point.
                size = passedSize;
                success = true;
            }

            int result = Interlocked.CompareExchange(ref thelock, 0, 1);
            Trace.Assert(1 == result); // The release of the lock should have been successful.  

            return success;
        }
    }
}

#endif
