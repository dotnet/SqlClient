// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Interop.Windows.AdvApi32
{
    internal class AdvApi32
    {
        private const string DllName = "advapi32.dll";

        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool AddAccessAllowedAce(IntPtr pAcl, int dwAceRevision, uint AccessMask, IntPtr pSid);

        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool AddAccessDeniedAce(IntPtr pAcl, int dwAceRevision, int AccessMask, IntPtr pSid);

        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool AllocateAndInitializeSid(
            IntPtr pIdentifierAuthority,
            byte nSubAuthorityCount,
            int dwSubAuthority0,
            int dwSubAuthority1,
            int dwSubAuthority2,
            int dwSubAuthority3,
            int dwSubAuthority4,
            int dwSubAuthority5,
            int dwSubAuthority6,
            int dwSubAuthority7,
            ref IntPtr pSid);


        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern IntPtr FreeSid(IntPtr pSid);

        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern int GetLengthSid(IntPtr pSid);

        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool InitializeAcl(IntPtr pAcl, int nAclLength, int dwAclRevision);

        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool InitializeSecurityDescriptor(IntPtr pSecurityDescriptor, int dwRevision);

        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool SetSecurityDescriptorDacl(
            IntPtr pSecurityDescriptor,
            bool bDaclPresent,
            IntPtr pDacl,
            bool bDaclDefaulted);
    }
}
