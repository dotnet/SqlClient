namespace SqlConnectionStringBuilder_InjectionAttack;

using System;
using System.Data;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        // <Snippet1>
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
        builder.DataSource = "(local)";
        builder.IntegratedSecurity = true;
        builder.InitialCatalog = "AdventureWorks;NewValue=Bad";
        Console.WriteLine(builder.ConnectionString);
        // </Snippet1>

        Console.WriteLine("Press Enter to continue.");
        Console.ReadLine();
    }
}
