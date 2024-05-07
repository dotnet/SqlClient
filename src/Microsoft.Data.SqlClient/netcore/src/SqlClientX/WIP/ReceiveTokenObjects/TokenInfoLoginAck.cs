using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;

namespace Microsoft.Data.SqlClient.SqlClientX.WIP
{
    internal class TokenInfoLoginAck
    {
        internal byte majorVersion;
        internal byte minorVersion;
        internal short buildNum;
        internal uint tdsVersion;

        internal static async TokenInfoLoginAck CreateAsync(TdsReadStream stream, bool isAsync)
        {
            TokenInfoLoginAck token = new TokenInfoLoginAck();
            token.majorVersion = await stream.ReadByteAsync();
            token.minorVersion = await stream.ReadByteAsync();
            token.buildNum = stream.ReadInt16();
            token.tdsVersion = stream.ReadUInt32();
            return token;
        }
    }
}
