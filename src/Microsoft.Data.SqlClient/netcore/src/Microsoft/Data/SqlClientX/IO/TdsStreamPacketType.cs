// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.IO
{
    internal enum TdsStreamPacketType
    {
        None = 0,
        PreLogin = 0x12,
        Login7 = 0x10,
        SqlBatch = 0x01,
        Rpc = 0x03,
        Attention = 0x06,
        BulkLoadBcp = 0x07,
        FederatedAuth = 0x08,
        FedAuthInfo = 0x19,
        SspiMessage = 0x0A,
        TransactionManagerRequest = 0x0E,
    }
}
