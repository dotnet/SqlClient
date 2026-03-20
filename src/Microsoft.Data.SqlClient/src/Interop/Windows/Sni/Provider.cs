// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

namespace Interop.Windows.Sni
{
    internal enum Provider
    {
        HTTP_PROV,
        NP_PROV,
        SESSION_PROV,
        SIGN_PROV,
        SM_PROV,
        SMUX_PROV,
        SSL_PROV,
        TCP_PROV,
        VIA_PROV,
        
        #if NETFRAMEWORK
        CTAIP_PROV,
        #endif
        
        MAX_PROVS,
        INVALID_PROV,
    }
}

#endif
