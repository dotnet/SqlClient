using System;
//<Snippet1>
using Microsoft.Data.SqlClient.Server;
using System.IO;
using System.Data.Sql;
using System.Data.SqlTypes;
using System.Text;

[Serializable]
[Microsoft.Data.SqlClient.Server.SqlUserDefinedAggregate(
   Microsoft.Data.SqlClient.Server.Format.UserDefined,
   IsInvariantToNulls = true,
   IsInvariantToDuplicates = false,
   IsInvariantToOrder = false,
   MaxByteSize = 8000)
        ]
public class Concatenate : Microsoft.Data.SqlClient.Server.IBinarySerialize
{

    public void Read(BinaryReader r)
    {

    }

    public void Write(BinaryWriter w)
    {

    }
}
//</Snippet1>
