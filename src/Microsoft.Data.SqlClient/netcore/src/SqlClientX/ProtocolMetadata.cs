using System.Text;
using Microsoft.Data.SqlClient;

namespace simplesqlclient
{
    internal class ProtocolMetadata
    {
        public ProtocolMetadata()
        {
        }

        public SqlCollation Collation { get; internal set; }

        public int DefaultCodePage { get; internal set; }

        public Encoding DefaultEncoding { get; internal set; }
    }
}
