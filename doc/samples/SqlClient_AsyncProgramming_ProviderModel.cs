namespace SqlClient_AsyncProgramming_ProviderModel;

using System;
// <Snippet1>
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

class Program
{
   static async Task PerformDBOperationsUsingProviderModel(string connectionString)
   {
      using (DbConnection connection = SqlClientFactory.Instance.CreateConnection())
      {
            connection.ConnectionString = connectionString;
            await connection.OpenAsync();

            DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM AUTHORS";

            using (DbDataReader reader = await command.ExecuteReaderAsync())
            {
               while (await reader.ReadAsync())
               {
                  for (int i = 0; i < reader.FieldCount; i++)
                  {
                        // Process each column as appropriate
                        object obj = await reader.GetFieldValueAsync<object>(i);
                        Console.WriteLine(obj);
                  }
               }
            }
      }
   }

   public static void Main()
   {
      SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
      // replace these with your own values
      builder.DataSource = "localhost";
      builder.InitialCatalog = "pubs";
      builder.IntegratedSecurity = true;

      Task task = PerformDBOperationsUsingProviderModel(builder.ConnectionString);
      task.Wait();
   }
}
// </Snippet1>
