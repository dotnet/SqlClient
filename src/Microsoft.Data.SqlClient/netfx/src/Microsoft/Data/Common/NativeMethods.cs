// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Microsoft.Data.Common
{
    internal static class NativeMethods
    {
        private const string Advapi32 = "advapi32.dll";
        private const string Kernel32 = "kernel32.dll";

        [DllImport(Kernel32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.Machine)]
        static internal extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, int dwDesiredAccess, int dwFileOffsetHigh, int dwFileOffsetLow, IntPtr dwNumberOfBytesToMap);

        // OpenFileMappingA contains a security venerability, in the unicode->ansi conversion 
        // Its possible to spoof the directory and construct ../ sequences,  See FxCop Warning
        // Specify marshaling for pinvoke string arguments
        [DllImport(Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        //        [DllImport(ExternDll.Kernel32, CharSet=System.Runtime.InteropServices.CharSet.Ansi)]
        [ResourceExposure(ResourceScope.Machine)]
        static internal extern IntPtr OpenFileMappingA(int dwDesiredAccess, bool bInheritHandle, [MarshalAs(UnmanagedType.LPStr)] string lpName);

        // CreateFileMappingA contains a security venerability, in the unicode->ansi conversion 
        // Its possible to spoof the directory and construct ../ sequences,  See FxCop Warning
        // Specify marshaling for pinvoke string arguments        
        [DllImport(Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        //        [DllImport(ExternDll.Kernel32, CharSet=System.Runtime.InteropServices.CharSet.Ansi)]
        [ResourceExposure(ResourceScope.Machine)]
        static internal extern IntPtr CreateFileMappingA(IntPtr hFile, IntPtr pAttr, int flProtect, int dwMaximumSizeHigh, int dwMaximumSizeLow, [MarshalAs(UnmanagedType.LPStr)] string lpName);

        [DllImport(Kernel32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.Machine)]
        static internal extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport(Kernel32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        static internal extern bool CloseHandle(IntPtr handle);

        [DllImport(Advapi32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern bool AllocateAndInitializeSid(
            IntPtr pIdentifierAuthority, // authority
            byte nSubAuthorityCount,                        // count of subauthorities
            int dwSubAuthority0,                          // subauthority 0
            int dwSubAuthority1,                          // subauthority 1
            int dwSubAuthority2,                          // subauthority 2
            int dwSubAuthority3,                          // subauthority 3
            int dwSubAuthority4,                          // subauthority 4
            int dwSubAuthority5,                          // subauthority 5
            int dwSubAuthority6,                          // subauthority 6
            int dwSubAuthority7,                          // subauthority 7
            ref IntPtr pSid);                                   // SID


        [DllImport(Advapi32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern int GetLengthSid(
            IntPtr pSid);   // SID to query

        [DllImport(Advapi32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern bool InitializeAcl(
            IntPtr pAcl,            // ACL
            int nAclLength,     // size of ACL
            int dwAclRevision);  // revision level of ACL

        [DllImport(Advapi32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern bool AddAccessDeniedAce(
            IntPtr pAcl,            // access control list
            int dwAceRevision,  // ACL revision level
            int AccessMask,     // access mask
            IntPtr pSid);           // security identifier

        [DllImport(Advapi32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern bool AddAccessAllowedAce(
            IntPtr pAcl,            // access control list
            int dwAceRevision,  // ACL revision level
            uint AccessMask,     // access mask
            IntPtr pSid);           // security identifier

        [DllImport(Advapi32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern bool InitializeSecurityDescriptor(
            IntPtr pSecurityDescriptor, // SD
            int dwRevision);                         // revision level
        [DllImport(Advapi32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern bool SetSecurityDescriptorDacl(
            IntPtr pSecurityDescriptor, // SD
            bool bDaclPresent,                        // DACL presence
            IntPtr pDacl,                               // DACL
            bool bDaclDefaulted);                       // default DACL

        [DllImport(Advapi32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern IntPtr FreeSid(
            IntPtr pSid);   // SID to free
    }
}
