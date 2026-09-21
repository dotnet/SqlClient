// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Interop.Windows.Sni
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void SqlAsyncCallbackDelegate(IntPtr m_ConsKey, IntPtr pPacket, uint dwError);
    
    internal delegate IntPtr SqlClientCertificateDelegate(IntPtr pCallbackContext);
}
