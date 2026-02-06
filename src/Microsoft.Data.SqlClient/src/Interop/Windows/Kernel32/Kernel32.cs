// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Win32.SafeHandles;

#if !NETFRAMEWORK && _WINDOWS
using Interop.Windows.Handles;
#endif

namespace Interop.Windows.Kernel32
{
    internal static class Kernel32
    {
        internal const ushort FILE_DEVICE_FILE_SYSTEM = 0x0009;
        internal const uint SEM_FAILCRITICALERRORS = 1;

        #if _WINDOWS && !NETFRAMEWORK
        internal const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        internal const int LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;
        #endif

        private const string DllName = "kernel32.dll";

        /// <summary><a href="https://docs.microsoft.com/en-us/windows-hardware/drivers/kernel/defining-i-o-control-codes">CTL_CODE</a> method.</summary>
        /// <param name="deviceType">Identifies the device type. This value must match the value that is set in the DeviceType member of the driver's DEVICE_OBJECT structure.</param>
        /// <param name="function">Identifies the function to be performed by the driver. Values of less than 0x800 are reserved for Microsoft. Values of 0x800 and higher can be used by vendors.</param>
        /// <param name="method">Indicates how the system will pass data between the caller of DeviceIoControl (or IoBuildDeviceIoControlRequest) and the driver that handles the IRP.</param>
        /// <param name="access">Indicates the type of access that a caller must request when opening the file object that represents the device (see IRP_MJ_CREATE).</param>
        internal static uint CtlCode(ushort deviceType, ushort function, byte method, byte access)
        {
            // MaxFunctionCode specifies the maximum value of the FunctionCode field in the FSCTLs (or IOCTLs for IOCTL tests). The maximum possible value is 4095.
            // See https://docs.microsoft.com/en-us/windows-hardware/drivers/develop/how-to-select-and-configure-the-device-fundamental-tests
            if (function > 4095)
            {
                throw ADP.ArgumentOutOfRange("function");
            }

            return (uint)((deviceType << 16) | (access << 14) | (function << 2) | method);
        }

        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool DeviceIoControl(
            SafeFileHandle fileHandle,
            uint ioControlCode,
            IntPtr inBuffer,
            uint cbInBuffer,
            IntPtr outBuffer,
            uint cbOutBuffer,
            out uint cbBytesReturned,
            IntPtr overlapped);

        #if NET && _WINDOWS
        [DllImport(DllName, ExactSpelling = true, SetLastError = true)]
        public static extern bool FreeLibrary([In] IntPtr hModule);
        #endif

        [DllImport(DllName, SetLastError = true)]
        internal static extern int GetFileType(SafeHandle hFile);

        #if NETFRAMEWORK
        [DllImport(DllName, SetLastError = true, CharSet = CharSet.Unicode)]
        [ResourceExposure(ResourceScope.Machine)]
        internal static extern int GetFullPathName(
            string path,
            int numBufferChars,
            StringBuilder buffer,
            IntPtr lpFilePartOrNull);
        #endif

        #if NET && _WINDOWS
        [DllImport(DllName, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport(DllName, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, string lpProcName);

        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeLibraryHandle LoadLibraryExW([In] string lpwLibFileName, [In] IntPtr hFile, [In] uint dwFlags);
        #endif

        [DllImport(DllName, SetLastError = true, ExactSpelling = true)]
        internal static extern bool SetThreadErrorMode(uint dwNewMode, out uint lpOldMode);
    }
}
