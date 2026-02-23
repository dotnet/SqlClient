namespace SqlDataSourceEnumeratorVersionExample;

using System;
//<Snippet1>
using Microsoft.Data.Sql;  
  
class Program  
{  
  static void Main()  
  {  
    // Retrieve the enumerator instance, and  
    // then retrieve the data sources.  
    SqlDataSourceEnumerator instance =  
      SqlDataSourceEnumerator.Instance;  
    System.Data.DataTable table = instance.GetDataSources();  
  
    // Filter the sources to just show SQL Server 2012 instances.  
    System.Data.DataRow[] rows = table.Select("Version LIKE '11%'");  
    foreach (System.Data.DataRow row in rows)  
    {  
      Console.WriteLine(row["ServerName"]);  
    }  
    Console.WriteLine("Press any key to continue.");  
    Console.ReadKey();  
  }  
} 
//</Snippet1>
