// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

namespace Interop.Windows.Sni
{
    internal enum SniSslProtocols : uint
    {
        // Protocol versions from native SNI
        SP_PROT_SSL2_SERVER = 0x00000004,
        SP_PROT_SSL2_CLIENT = 0x00000008,
        SP_PROT_SSL3_SERVER = 0x00000010,
        SP_PROT_SSL3_CLIENT = 0x00000020,
        SP_PROT_TLS1_0_SERVER = 0x00000040,
        SP_PROT_TLS1_0_CLIENT = 0x00000080,
        SP_PROT_TLS1_1_SERVER = 0x00000100,
        SP_PROT_TLS1_1_CLIENT = 0x00000200,
        SP_PROT_TLS1_2_SERVER = 0x00000400,
        SP_PROT_TLS1_2_CLIENT = 0x00000800,
        SP_PROT_TLS1_3_SERVER = 0x00001000,
        SP_PROT_TLS1_3_CLIENT = 0x00002000,
        SP_PROT_NONE = 0x0,

        // Combinations for easier use when mapping to SslProtocols
        SP_PROT_SSL2 = SP_PROT_SSL2_SERVER | SP_PROT_SSL2_CLIENT,
        SP_PROT_SSL3 = SP_PROT_SSL3_SERVER | SP_PROT_SSL3_CLIENT,
        SP_PROT_TLS1_0 = SP_PROT_TLS1_0_SERVER | SP_PROT_TLS1_0_CLIENT,
        SP_PROT_TLS1_1 = SP_PROT_TLS1_1_SERVER | SP_PROT_TLS1_1_CLIENT,
        SP_PROT_TLS1_2 = SP_PROT_TLS1_2_SERVER | SP_PROT_TLS1_2_CLIENT,
        SP_PROT_TLS1_3 = SP_PROT_TLS1_3_SERVER | SP_PROT_TLS1_3_CLIENT,
    }
}

#endif
