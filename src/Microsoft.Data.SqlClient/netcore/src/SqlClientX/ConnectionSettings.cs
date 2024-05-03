namespace simplesqlclient
{
    internal class ConnectionSettings
    {
        internal string WorkstationId { get; set;}

        public string ApplicationName { get; internal set; }
        public bool UseSSPI { get; internal set; }
        public bool ReadOnlyIntent { get; internal set; }
        public int PacketSize { get; internal set;}
    }
}
