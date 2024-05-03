namespace Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets
{
    internal class PacketHeader
    {
        byte Type;
        byte Status;
        ushort Length;
        ushort SPID;
        byte PacketID;
        ushort Window;
    }
}
