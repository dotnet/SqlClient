// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.ProviderBase
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Microsoft.Data.Common;

    // DbBuffer is abstract to require derived class to exist
    // so that when debugging, we can tell the difference between one DbBuffer and another
    internal abstract class DbBuffer : SafeHandle
    {
        internal const int LMEM_FIXED = 0x0000;
        internal const int LMEM_MOVEABLE = 0x0002;
        internal const int LMEM_ZEROINIT = 0x0040;

        private readonly int _bufferLength;

        internal DbBuffer(int initialSize) : base(IntPtr.Zero, true)
        {
            if (initialSize > 0)
            {
                _bufferLength = initialSize;
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                { 
                }
                finally
                {
                    handle = SafeNativeMethods.LocalAlloc(LMEM_ZEROINIT, new IntPtr(initialSize));
                }
                if (handle == IntPtr.Zero)
                {
                    throw new OutOfMemoryException();
                }
            }
        }

        private int BaseOffset => 0;

        public override bool IsInvalid => handle == IntPtr.Zero;

        internal int Length => _bufferLength;

        protected override bool ReleaseHandle()
        {
            // NOTE: The SafeHandle class guarantees this will be called exactly once.
            IntPtr ptr = base.handle;
            base.handle = IntPtr.Zero;
            if (IntPtr.Zero != ptr)
            {
                SafeNativeMethods.LocalFree(ptr);
            }
            return true;
        }

        internal void WriteIntPtr(int offset, IntPtr value)
        {
            offset += BaseOffset;
            ValidateCheck(offset, IntPtr.Size);
            Debug.Assert(0 == offset % IntPtr.Size, "invalid alignment");

            bool mustRelease = false;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                DangerousAddRef(ref mustRelease);

                IntPtr ptr = DangerousGetHandle();
                Marshal.WriteIntPtr(ptr, offset, value);
            }
            finally
            {
                if (mustRelease)
                {
                    DangerousRelease();
                }
            }
        }


        [Conditional("DEBUG")]
        protected void ValidateCheck(int offset, int count)
        {
            if ((offset < 0) || (count < 0) || (Length < checked(offset + count)))
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidBuffer);
            }
        }
    }
}
