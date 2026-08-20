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
        /// A <c>PCCERT_CONTEXT</c> for the client certificate to present, or
        /// <see cref="IntPtr.Zero" />. SNI duplicates the context, so the caller retains ownership.
        /// </summary>
        public IntPtr certContext;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string certId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool certHash;
        public IntPtr clientCertificateCallbackContext;
        public SqlClientCertificateDelegate clientCertificateCallback;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string serverCertFileName;
    }
}
