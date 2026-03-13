namespace SqlConfigurableRetryLogic_OpenConnection;

using System;
// <Snippet1>
using Microsoft.Data.SqlClient;

/// Detecting retriable exceptions is a vital part of the retry pattern.
/// Before applying retry logic it is important to investigate exceptions and choose a retry provider that best fits your scenario.
/// First, log your exceptions and find transient faults.
/// The purpose of this sample is to illustrate how to use this feature and the condition might not be realistic.
class RetryLogicSample
{
    private const string DefaultDB = "Northwind";
    private const string CnnStringFormat = "Server=localhost; Initial Catalog={0}; Integrated Security=true; pooling=false;";
    private const string DropDatabaseFormat = "DROP DATABASE {0}";

    // For general use
    private static SqlConnection s_generalConnection = new SqlConnection(string.Format(CnnStringFormat, DefaultDB));

    static void Main(string[] args)
    {
        // 1. Define the retry logic parameters
        var options = new SqlRetryLogicOption()
        {
            NumberOfTries = 5,
            MaxTimeInterval = TimeSpan.FromSeconds(20),
            DeltaTime = TimeSpan.FromSeconds(1)
        };

        // 2. Create a retry provider
        var provider = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(options);

        // define the retrying event to report the execution attempts
        provider.Retrying += (object s, SqlRetryingEventArgs e) =>
            {
                int attempts = e.RetryCount + 1;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"attempt {attempts} - current delay time:{e.Delay} \n");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                if (e.Exceptions[e.Exceptions.Count - 1] is SqlException ex)
                {
                    Console.WriteLine($"{ex.Number}-{ex.Message}\n");
                }
                else
                {
                    Console.WriteLine($"{e.Exceptions[e.Exceptions.Count - 1].Message}\n");
                }

                // It is not a good practice to do time-consuming tasks inside the retrying event which blocks the running task.
                // Use parallel programming patterns to mitigate it.
                if (e.RetryCount == provider.RetryLogic.NumberOfTries - 1)
                {
                    Console.WriteLine("This is the last chance to execute the command before throwing the exception.");
                    Console.WriteLine("Press Enter when you're ready:");
                    Console.ReadLine();
                    Console.WriteLine("continue ...");
                }
            };

        // Open the general connection.
        s_generalConnection.Open();

        try
        {
            // Assume the database is being created and other services are going to connect to it.
            RetryConnection(provider);
        }
        catch
        {
            // exception is thrown if connecting to the database isn't successful.
            throw;
        }
    }

    private static void ExecuteCommand(SqlConnection cn, string command)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = command;
        cmd.ExecuteNonQuery();
    }

    private static void RetryConnection(SqlRetryLogicBaseProvider provider)
    {
        // Change this if you already have a database with the same name in your database.
        string dbName = "Invalid_DB_Open";

        // Create a connection to an invalid database.
        using var cnn = new SqlConnection(string.Format(CnnStringFormat, dbName));
        // 3. Assign the `provider` to the connection
        cnn.RetryLogicProvider = provider;
        Console.WriteLine($"Connecting to the [{dbName}] ...");
        // Manually execute the following command in SSMS to create the invalid database while the SqlConnection is attempting to connect to it.
        // >> CREATE DATABASE Invalid_DB_Open;
        Console.WriteLine($"Manually, run the 'CREATE DATABASE {dbName};' in the SQL Server before exceeding the {provider.RetryLogic.NumberOfTries} attempts.");
        // the connection tries to connect to the database 5 times
        Console.WriteLine("The first attempt, before getting into the retry logic.");
        cnn.Open();
        Console.WriteLine($"Connected to the [{dbName}] successfully.");

        cnn.Close();

        // Drop it after test
        ExecuteCommand(s_generalConnection, string.Format(DropDatabaseFormat, dbName));
        Console.WriteLine($"The [{dbName}] is removed.");
    }
}
// </Snippet1>
