// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// SMUX packet flags
    /// </summary>
    [Flags]
    internal enum SniSmuxFlags
    {
        // @TODO: Should probably drop the SMUX prefix - it's pretty obvious since it's in the SmuxFlags enum.
        /// <summary>
        /// Begin SMUX connection.
        /// </summary>
        SMUX_SYN = 1,
        
        /// <summary>
        /// Acknowledge SMUX packets.
        /// </summary>
        SMUX_ACK = 2,
        
        /// <summary>
        /// End SMUX connection.
        /// </summary>
        SMUX_FIN = 4,
        
        /// <summary>
        /// SMUX data packet.
        /// </summary>
        SMUX_DATA = 8
    }
}

#endif
