

using simplesqlclient;
using System;

class Program
{
    static void Main()
    {
        Console.WriteLine("Hello World");
        AuthDetails authDetails = new AuthDetails();
        authDetails.UserName = "sa";
        // read pass word from env var SQL_PASSWORD
        string password = Environment.GetEnvironmentVariable("SQL_PASSWORD");
        if (password == null)
        {
            throw new Exception("Environment variable SQL_PASSWORD is not set.");
        }
        authDetails.Password = password;

    }
}
