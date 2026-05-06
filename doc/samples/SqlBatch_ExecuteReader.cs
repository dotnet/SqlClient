#if NET
namespace SqlBatch_ExecuteReader;

using System;
using System.Collections.Generic;

// <Snippet1>
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string str = "Data Source=(local);Initial Catalog=Northwind;"
        + "Integrated Security=SSPI;Encrypt=False";
        RunBatch(str);
    }

    static void RunBatch(string connString)
    {
        using var connection = new SqlConnection(connString);
        connection.Open();

        using var batch = new SqlBatch(connection);

        const int count = 10;
        const string parameterName = "parameter";
        for (int i = 0; i < count; i++)
        {
            var batchCommand = new SqlBatchCommand($"SELECT @{parameterName} as value");
            batchCommand.Parameters.Add(new SqlParameter(parameterName, i));
            batch.BatchCommands.Add(batchCommand);
        }

        var results = new List<int>(count);
        using (SqlDataReader reader = batch.ExecuteReader())
        {
            do
            {
                while (reader.Read())
                {
                    results.Add(reader.GetFieldValue<int>(0));
                }
            } while (reader.NextResult());
        }
        Console.WriteLine(string.Join(", ", results));
    }
}
// </Snippet1>
#endif
