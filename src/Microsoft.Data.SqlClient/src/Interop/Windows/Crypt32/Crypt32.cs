// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Interop.Windows.Crypt32
{
    internal static class Crypt32
    {
        private const string DllName = "crypt32.dll";

        /// <summary>
        /// <a href="https://learn.microsoft.com/windows/win32/api/wincrypt/nf-wincrypt-certduplicatecertificatecontext">CertDuplicateCertificateContext</a>
        /// increments the reference count of a certificate context.
        /// </summary>
        /// <param name="certContext">The certificate context to duplicate.</param>
        /// <returns>
        /// The duplicated certificate context, which the caller must release with
        /// <c>CertFreeCertificateContext</c>, or <see cref="IntPtr.Zero" /> on failure.
        /// </returns>
        [DllImport(DllName, SetLastError = true)]
        internal static extern IntPtr CertDuplicateCertificateContext(IntPtr certContext);
    }
}
