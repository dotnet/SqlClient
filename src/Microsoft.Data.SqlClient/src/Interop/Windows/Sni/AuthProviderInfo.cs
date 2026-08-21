// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Interop.Windows.Sni
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct AuthProviderInfo
    {
        public uint flags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool tlsFirst;
        /// <summary>
        /// A <c>PCCERT_CONTEXT</c> reserved by the SNI header. The shipping provider never reads
        /// this field, so a client certificate must be supplied through <see cref="certId" /> or
        /// <see cref="clientCertificateCallback" /> instead.
        /// </summary>
        public IntPtr certContext;
        /// <summary>
        /// The SHA-1 hash or subject name of the client certificate to present, or
        /// <see langword="null" /> to disable certificate authentication. SNI searches the
        /// LocalMachine and CurrentUser personal stores for a match before falling back to
        /// <see cref="clientCertificateCallback" />.
        /// </summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        public string certId;
        /// <summary>
        /// Whether <see cref="certId" /> is a SHA-1 hash rather than a subject name.
        /// </summary>
        [MarshalAs(UnmanagedType.Bool)]
        public bool certHash;
        public IntPtr clientCertificateCallbackContext;
        public SqlClientCertificateDelegate clientCertificateCallback;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string serverCertFileName;
    }
}
