#if NETFRAMEWORK
namespace SqlDataRecordCS;

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Server;

public sealed partial class SqlDataRecordTester
{
   private SqlDataRecordTester()
   {
   }

//<Snippet1>
//using System;
//using System.Collections.Generic;
//using System.Data;
//using Microsoft.Data.SqlClient.Server;

// Stream rows to SQL Server as a table-valued parameter.
public static IEnumerable<SqlDataRecord> GetRecords()
{
    // Re-use a single SqlDataRecord instance rather than allocating a new one for each row.
    // Each row's values are read before SqlCommand advances to the next one.
    SqlDataRecord record;
      
    // Create a new record with the column metadata. The constructor is 
    // able to accept a variable number of parameters. 
    record = new SqlDataRecord(new SqlMetaData[] { new SqlMetaData("Column1", SqlDbType.NVarChar, 12), 
                                                  new SqlMetaData("Column2", SqlDbType.Int), 
                                                  new SqlMetaData("Column3", SqlDbType.DateTime) });

    // Set the record fields.
    record.SetString(0, "Hello World!");
    record.SetInt32(1, 42);
    record.SetDateTime(2, DateTime.Now);

    // Set the fields of the first record and stream it to SQL Server.
    yield return record;

    // Set the fields of the second record and stream it to SQL Server.
    record.SetInt32(1, 0);
    yield return record;
}
//</Snippet1>

public static void CreateNewRecord()
{

//<Snippet2>
//using Microsoft.Data.SqlClient.Server;

// Variables.
SqlMetaData column1Info;
SqlMetaData column2Info;
SqlDataRecord record;

// Create the column metadata.
column1Info = new SqlMetaData("Column1", SqlDbType.NVarChar, 12);
column2Info = new SqlMetaData("Column2", SqlDbType.Int);

// Create a new record with the column metadata.      
record = new SqlDataRecord(new SqlMetaData[] { column1Info, 
                                                  column2Info });
// Set the record fields.
record.SetString(0, "Hello World!");
record.SetInt32(1, 42);

//</Snippet2>
}
}
#endif
