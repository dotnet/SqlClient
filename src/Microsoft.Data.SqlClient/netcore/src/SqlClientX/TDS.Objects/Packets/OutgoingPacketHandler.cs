namespace Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets
{
    internal abstract class OutgoingPacketHandler
    {
        protected abstract byte PacketHeaderType { get;  }
    }
}
