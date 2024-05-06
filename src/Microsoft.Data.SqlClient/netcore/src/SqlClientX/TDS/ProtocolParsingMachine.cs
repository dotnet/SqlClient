using Microsoft.Data.SqlClient.SqlClientX.Streams;

namespace Microsoft.Data.SqlClient.SqlClientX.TDS
{
    internal class ProtocolParsingMachine
    {

        private TdsWriteStream _writeStream;
        private TdsReadStream _readStream;

        public ProtocolParsingMachine(TdsWriteStream writeStream, TdsReadStream readStream)
        {
            _writeStream = writeStream;
            _readStream = readStream;
        }


    }
}
