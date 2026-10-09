// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Interop.Windows.Advapi32
{
    internal static class Advapi32
    {
        internal static long GetAuthenticationId(SafeAccessTokenHandle token)
        {
            if (!GetTokenInformation(token, TokenInformationClass.TokenStatistics, out TokenStatistics statistics,
                Marshal.SizeOf<TokenStatistics>(), out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return ((long)statistics.AuthenticationId.HighPart << 32) | statistics.AuthenticationId.LowPart;
        }

        internal static bool GetIsTokenRestricted(SafeAccessTokenHandle token)
        {
            // FALSE means either unrestricted or failure. Clear the native last error first,
            // including on .NET Framework, whose P/Invoke marshaller does not clear it.
            Kernel32.Kernel32.SetLastError(0);
            bool restricted = IsTokenRestricted(token);
            int error = Marshal.GetLastWin32Error();
            if (!restricted && error != 0)
            {
                throw new Win32Exception(error);
            }

            return restricted;
        }

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(
            SafeAccessTokenHandle token,
            TokenInformationClass informationClass,
            out TokenStatistics statistics,
            int bufferLength,
            out int returnLength);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsTokenRestricted(SafeAccessTokenHandle token);

        private enum TokenInformationClass
        {
            TokenStatistics = 10
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            internal uint LowPart;
            internal int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenStatistics
        {
            internal Luid TokenId;
            internal Luid AuthenticationId;
            internal long ExpirationTime;
            internal int TokenType;
            internal int ImpersonationLevel;
            internal uint DynamicCharged;
            internal uint DynamicAvailable;
            internal uint GroupCount;
            internal uint PrivilegeCount;
            internal Luid ModifiedId;
        }
    }
}
