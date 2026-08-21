// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Interop.Windows.Sni
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void SqlAsyncCallbackDelegate(IntPtr m_ConsKey, IntPtr pPacket, uint dwError);

    /// <summary>
    /// Supplies a client certificate to SNI when neither the LocalMachine nor the CurrentUser
    /// personal store holds a certificate matching <paramref name="certId" />.
    /// </summary>
    /// <param name="callbackContext">The context supplied in <see cref="AuthProviderInfo.clientCertificateCallbackContext" />.</param>
    /// <param name="certHash">Whether <paramref name="certId" /> is a SHA-1 hash rather than a subject name.</param>
    /// <param name="certId">The certificate identifier that the store lookup failed to resolve.</param>
    /// <param name="certContext">
    /// Receives a <c>PCCERT_CONTEXT</c> that SNI releases with <c>CertFreeCertificateContext</c>.
    /// </param>
    /// <param name="keyContainerFlags">Receives the flags used to delete a temporary key container.</param>
    /// <param name="keyContainerLength">The capacity, in characters, of <paramref name="keyContainer" />.</param>
    /// <param name="keyContainer">
    /// A caller-allocated buffer that receives the name of a temporary key container to delete when
    /// the connection closes. Leaving it empty tells SNI that there is nothing to clean up.
    /// </param>
    /// <returns>A Windows error code, where zero indicates success.</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    internal delegate uint SqlClientCertificateDelegate(
        IntPtr callbackContext,
        [MarshalAs(UnmanagedType.Bool)] bool certHash,
        [MarshalAs(UnmanagedType.LPWStr)] string certId,
        out IntPtr certContext,
        out uint keyContainerFlags,
        uint keyContainerLength,
        IntPtr keyContainer);
}
