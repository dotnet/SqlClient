namespace SqlConnection_OpenAsync_ContinueWith;

using System;
using System.Data;
// <Snippet2>
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

class A 
{
   static void ProductList(IAsyncResult result) { }

   public static void Main() 
   {
      // AsyncCallback productList = new AsyncCallback(ProductList);
      // SqlConnection conn = new SqlConnection("Data Source=(local); Initial Catalog=NorthWind; Integrated Security=SSPI");
      // conn.Open();
      // SqlCommand cmd = new SqlCommand("select top 2 * from orders", conn);
      // IAsyncResult ia = cmd.BeginExecuteReader(productList, cmd);

      AsyncCallback productList = new AsyncCallback(ProductList);
      SqlConnection conn = new SqlConnection("Data Source=(local); Initial Catalog=NorthWind; Integrated Security=SSPI");
      conn.OpenAsync().ContinueWith((task) => {
         SqlCommand cmd = new SqlCommand("select top 2 * from orders", conn);
         IAsyncResult ia = cmd.BeginExecuteReader(productList, cmd);
      }, TaskContinuationOptions.OnlyOnRanToCompletion);
   }
}
// </Snippet2>

class B 
{
    static void ProductList(IAsyncResult result) { }

    public static void Main() 
   {
        // <Snippet1>
        AsyncCallback productList = new AsyncCallback(ProductList);
        SqlConnection conn = new SqlConnection("Data Source=(local); Initial Catalog=NorthWind; Integrated Security=SSPI");
        conn.Open();
        SqlCommand cmd = new SqlCommand("select top 2 * from orders", conn);
        IAsyncResult ia = cmd.BeginExecuteReader(productList, cmd);
        // </Snippet1>
   }
}
