using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace simplesqlclient
{
    internal class TdsConstants
    {
        public const int PACKET_HEADER_SIZE = 8;

        public const int DEFAULT_LOGIN_PACKET_SIZE = 4096;

        public const int HEADER_LEN_FIELD_OFFSET = 2;


        public const int MAX_PRELOGIN_PAYLOAD_LENGTH = 1024;
        public const int GUID_SIZE = 16;
        public const int CharSize = 2;
    }

    internal enum PreLoginOptions
    {
        VERSION,
        ENCRYPT,
        INSTANCE,
        THREADID,
        MARS,
        TRACEID,
        FEDAUTHREQUIRED,
        NUMOPT,
        LASTOPT = 255
    }

    internal enum  PacketFlushMode
    {
        // Flush the packet to the network stream if its full
        // this sets the packet status to 
        SOFTFLUSH,
        HARDFLUSH
    }

    internal enum PacketStatus
    {
        Normal = 0x00,
        EOM = 0x01,
        IGNORE = 0x02,
        BATCH = 0x04,
        RESETCONNECTION = 0x08,
        RESETCONNECTIONSKIPTRAN = 0x10
    }

    internal enum PacketType
    {
        SERVERSTREAM = 0x04,
        LOGIN = 0x10, 
        PRELOGIN = 0x12,
        MT_SQL = 0x01,

    }

    internal enum SqlEncryptionOptions
    {
        OFF,
        ON,
        NOT_SUP,
        REQ,
        LOGIN
    }

    
}
