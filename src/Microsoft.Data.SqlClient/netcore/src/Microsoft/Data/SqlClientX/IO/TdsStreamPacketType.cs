namespace Microsoft.Data.SqlClientX.IO
{
    internal enum TdsStreamPacketType
    {
        PreLogin = 0x12,
        Login7 = 0x10,
        SQLBatch = 0x01,
        RPC = 0x03,
        Attention = 0x06,
        BulkLoadBCP = 0x07,
        FederatedAuth = 0x08,
        FedAuthInfo = 0x19,
        SSPIMessage = 0x0A,
        TransactionManagerRequest = 0x0E,
    }
}
