#if NETFRAMEWORK
namespace SqlMetaDataCS;

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Server;

public sealed partial class SqlMetaDataTester
{
    private SqlMetaDataTester()
    {
    }

    //<Snippet1>
    // using System;
    // using System.Collections.Generic;
    // using System.Data;
    // using Microsoft.Data.SqlClient.Server;

    public static IEnumerable<SqlDataRecord> ReturnNewRecords()
    {
        // Variables.
        SqlMetaData column1Info;
        SqlMetaData column2Info;
        SqlMetaData column3Info;
        SqlDataRecord record;

        // Create the column metadata.
        column1Info = new SqlMetaData("Column1", SqlDbType.NVarChar, 12);
        column2Info = new SqlMetaData("Column2", SqlDbType.Int);
        column3Info = new SqlMetaData("Column3", SqlDbType.DateTime);

        // Create a new record with the column metadata.      
        record = new SqlDataRecord(new SqlMetaData[] { column1Info,
                                                  column2Info,
                                                  column3Info });

        // Set the fields of the first record and stream it to SQL Server.
        record.SetString(0, "Hello World!");
        record.SetInt32(1, 42);
        record.SetDateTime(2, DateTime.Now);
        yield return record;

        // Set the fields of the second record and stream it to SQL Server.
        record.SetInt32(1, 0);
        yield return record;
    }
    //</Snippet1>

    //<Snippet2>
    // using Microsoft.Data.SqlClient.Server;

    public static void CreateSqlMetaData1()
    {
        SqlMetaData columnInfo;
        columnInfo = new SqlMetaData("Column1", SqlDbType.NVarChar, 12);
    }
    //</Snippet2>

    //<Snippet3>
    // using Microsoft.Data.SqlClient.Server;

    public static void CreateSqlMetaData2()
    {
        SqlMetaData columnInfo;
        columnInfo = new SqlMetaData("Column2", SqlDbType.Int);
    }
    //</Snippet3>
}
#endif
