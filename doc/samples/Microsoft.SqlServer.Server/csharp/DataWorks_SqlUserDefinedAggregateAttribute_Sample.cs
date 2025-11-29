namespace DataWorks_SqlUserDefinedAggregateAttribute_Sample;

//<Snippet1>
using System;
using System.IO;
using Microsoft.SqlServer.Server;

[Serializable]
[SqlUserDefinedAggregate(Microsoft.SqlServer.Server.Format.UserDefined,
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
