// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// SNI Asynchronous callback
    /// </summary>
    /// <param name="packet">SNI packet</param>
    /// <param name="sniErrorCode">SNI error code</param>
    internal delegate void SniAsyncCallback(SniPacket packet, uint sniErrorCode);
}

#endif
