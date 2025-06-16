// See https://aka.ms/new-console-template for more information

using Microsoft.Data.SqlClient;

var connectionString = "Server=tcp:malcolm-test.database.windows.net,1433;Initial Catalog=malcolm-test;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Interactive;";
var conn = new SqlConnection(connectionString);
conn.Open();

var conn2 = new SqlConnection(connectionString);
conn2.Open();

Console.WriteLine("hello");
