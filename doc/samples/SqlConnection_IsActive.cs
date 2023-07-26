using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        using (var connection = new SqlConnection(@"Data Source=(local);Initial Catalog=AdventureWorks2012;Integrated Security=SSPI"))
        {
            bool IsAlive = connection.IsActive();
            Console.WriteLine("Connection is isActive = {0} ", IsAlive);
            connection.Open();
            IsAlive = connection.IsActive();
            Console.WriteLine("Connection is isActive = {0} ", IsAlive);
        }
    }
}
// </Snippet1>
