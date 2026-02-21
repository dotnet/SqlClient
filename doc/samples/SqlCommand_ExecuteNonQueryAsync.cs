namespace SqlCommand_ExecuteNonQueryAsync;

using System;
// <Snippet1>
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

class A {
   public static void Main() 
   {
      using (SqlConnection conn = new SqlConnection("Data Source=(local); Initial Catalog=NorthWind; Integrated Security=SSPI"))
      {
         SqlCommand command = new SqlCommand("SELECT TOP 2 * FROM dbo.Orders", conn);

         int result = A.Method(conn, command).Result;

         SqlDataReader reader = command.ExecuteReader();
         while (reader.Read())
            Console.WriteLine(reader[0]);
      }
   }

   static async Task<int> Method(SqlConnection conn, SqlCommand cmd) {
      await conn.OpenAsync();
      await cmd.ExecuteNonQueryAsync();
      return 1;
   }
}
// </Snippet1>
