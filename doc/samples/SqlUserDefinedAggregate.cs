namespace SqlUserDefinedAggregate;

using System;
//<Snippet1>
using Microsoft.SqlServer.Server;
using System.IO;
using System.Data.Sql;
using System.Data.SqlTypes;
using System.Text;

[Serializable]
[Microsoft.SqlServer.Server.SqlUserDefinedAggregate(
   Microsoft.SqlServer.Server.Format.UserDefined,
   IsInvariantToNulls = true,
   IsInvariantToDuplicates = false,
   IsInvariantToOrder = false,
   MaxByteSize = 8000)
        ]
public class Concatenate : Microsoft.SqlServer.Server.IBinarySerialize
{

    public void Read(BinaryReader r)
    {

    }

    public void Write(BinaryWriter w)
    {

    }
}
//</Snippet1>
