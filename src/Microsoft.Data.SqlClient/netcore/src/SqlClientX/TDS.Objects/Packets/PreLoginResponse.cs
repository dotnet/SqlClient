namespace Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets
{
    internal class PreLoginResponse
    {
        internal bool IsMarsEnabled { get; set; }
        internal bool ServerSupportsFedAuth { get; set; }
    }
}
