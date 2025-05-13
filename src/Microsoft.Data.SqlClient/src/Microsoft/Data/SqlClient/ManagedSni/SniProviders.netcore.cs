// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// SNI provider identifiers
    /// </summary>
    internal enum SniProviders
    {
        // @TODO: Rename to follow enum naming conventions, remove PROV suffix - we get it, it's in the Providers enum.
        HTTP_PROV = 0, // HTTP Provider
        NP_PROV = 1, // Named Pipes Provider
        SESSION_PROV = 2, // Session Provider
        SIGN_PROV = 3, // Sign Provider
        SM_PROV = 4, // Shared Memory Provider
        SMUX_PROV = 5, // SMUX Provider
        SSL_PROV = 6, // SSL Provider
        TCP_PROV = 7, // TCP Provider
        VIA_PROV = 8, // Virtual Interface Architecture Provider
        CTAIP_PROV = 9,
        MAX_PROVS = 10, // Number of providers
        INVALID_PROV = 11 // SQL Network Interfaces
    }
}

#endif
