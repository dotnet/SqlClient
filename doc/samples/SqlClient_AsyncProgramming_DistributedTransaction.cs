namespace SqlClient_AsyncProgramming_DistributedTransaction;

using System;
// <Snippet1>
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using System.Transactions;

class Program
{
   public static void Main()
   {
      SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
      // replace these with your own values
      // create two tables RegionTable1 and RegionTable2
      // and add a constraint in one of these tables 
      // to avoid duplicate RegionID
      builder.DataSource = "localhost";
      builder.InitialCatalog = "Northwind";
      builder.IntegratedSecurity = true;

      Task task = ExecuteDistributedTransaction(builder.ConnectionString, builder.ConnectionString);
      task.Wait();
   }

   static async Task ExecuteDistributedTransaction(string connectionString1, string connectionString2)
   {
      using (SqlConnection connection1 = new SqlConnection(connectionString1))
      using (SqlConnection connection2 = new SqlConnection(connectionString2))
      {
            using (CommittableTransaction transaction = new CommittableTransaction())
            {
               await connection1.OpenAsync();
               connection1.EnlistTransaction(transaction);

               await connection2.OpenAsync();
               connection2.EnlistTransaction(transaction);

               try
               {
                  SqlCommand command1 = connection1.CreateCommand();
                  command1.CommandText = "Insert into RegionTable1 (RegionID, RegionDescription) VALUES (100, 'Description')";
                  await command1.ExecuteNonQueryAsync();

                  SqlCommand command2 = connection2.CreateCommand();
                  command2.CommandText = "Insert into RegionTable2 (RegionID, RegionDescription) VALUES (100, 'Description')";
                  await command2.ExecuteNonQueryAsync();

                  transaction.Commit();
               }
               catch (Exception ex)
               {
                  Console.WriteLine("Exception Type: {0}", ex.GetType());
                  Console.WriteLine("  Message: {0}", ex.Message);

                  try
                  {
                        transaction.Rollback();
                  }
                  catch (Exception ex2)
                  {
                        Console.WriteLine("Rollback Exception Type: {0}", ex2.GetType());
                        Console.WriteLine("  Message: {0}", ex2.Message);
                  }
               }
            }
      }
   }
}
// </Snippet1>
