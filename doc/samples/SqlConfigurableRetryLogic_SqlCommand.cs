namespace SqlConfigurableRetryLogic_SqlCommand;

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

class RetryLogicSample
{
// <Snippet1>
/// Detecting retriable exceptions is a vital part of the retry pattern.
/// Before applying retry logic it is important to investigate exceptions and choose a retry provider that best fits your scenario.
/// First, log your exceptions and find transient faults.
/// The purpose of this sample is to illustrate how to use this feature and the condition might not be realistic.

    private const string DefaultDB = "Northwind";
    private const string CnnStringFormat = "Server=localhost; Initial Catalog={0}; Integrated Security=true; pooling=false;";
    private const string DropDatabaseFormat = "DROP DATABASE {0}";
    private const string CreateDatabaseFormat = "CREATE DATABASE {0}";

    // For general use
    private static SqlConnection s_generalConnection = new SqlConnection(string.Format(CnnStringFormat, DefaultDB));

    static void Main(string[] args)
    {
        // 1. Define the retry logic parameters
        var options = new SqlRetryLogicOption()
        {
            NumberOfTries = 5,
            MaxTimeInterval = TimeSpan.FromSeconds(20),
            DeltaTime = TimeSpan.FromSeconds(1),
            AuthorizedSqlCondition = null,
            // error number 3702 : Cannot drop database "xxx" because it is currently in use.
            TransientErrors = new int[] {3702}
        };

        // 2. Create a retry provider
        var provider = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(options);

        // define the retrying event to report execution attempts
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

                // It is not good practice to do time-consuming tasks inside the retrying event which blocks the running task.
                // Use parallel programming patterns to mitigate it.
                if (e.RetryCount == provider.RetryLogic.NumberOfTries - 1)
                {
                    Console.WriteLine("This is the last chance to execute the command before throwing the exception.");
                    Console.WriteLine("Press Enter when you're ready:");
                    Console.ReadLine();
                    Console.WriteLine("continue ...");
                }
            };

        // Open a general connection.
        s_generalConnection.Open();

        try
        {
            // Assume the database is creating and other services are going to connect to it.
            RetryCommand(provider);
        }
        catch
        {
            s_generalConnection.Close();
            // exception is thrown if connecting to the database isn't successful.
            throw;
        }
        s_generalConnection.Close();
    }

    private static void ExecuteCommand(SqlConnection cn, string command)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = command;
        cmd.ExecuteNonQuery();
    }

    private static void FindActiveSessions(SqlConnection cnn, string dbName)
    {
        using var cmd = cnn.CreateCommand();
        cmd.CommandText = "DECLARE @query NVARCHAR(max) = '';" + Environment.NewLine +
            $"SELECT @query = @query + 'KILL ' + CAST(spid as varchar(50)) + ';' FROM sys.sysprocesses WHERE dbid = DB_ID('{dbName}')" + Environment.NewLine +
            "SELECT @query AS Active_sessions;";
        var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($">> Execute the '{reader.GetString(0)}' command in SQL Server to unblock the running task.");
            Console.ResetColor();
        }
        reader.Close();
    }
// </Snippet1>
// <Snippet2>
    private static void RetryCommand(SqlRetryLogicBaseProvider provider)
    {
        // Change this if you already have a database with the same name in your database.
        string dbName = "RetryCommand_TestDatabase";

        // Subscribe a new event on retry event and discover the active sessions on a database
        EventHandler<SqlRetryingEventArgs> retryEvent = (object s, SqlRetryingEventArgs e) =>
        {
            // Run just at first execution
            if (e.RetryCount == 1)
            {
                FindActiveSessions(s_generalConnection, dbName);
                Console.WriteLine($"Before exceeding {provider.RetryLogic.NumberOfTries} attempts.");
            }
        };

        provider.Retrying += retryEvent;

        // Create a new database.
        ExecuteCommand(s_generalConnection, string.Format(CreateDatabaseFormat, dbName));
        Console.WriteLine($"The '{dbName}' database is created.");

        // Open a connection to the newly created database to block it from being dropped.
        using var blockingCnn = new SqlConnection(string.Format(CnnStringFormat, dbName));
        blockingCnn.Open();
        Console.WriteLine($"Established a connection to '{dbName}' to block it from being dropped.");

        Console.WriteLine($"Dropping `{dbName}`...");
        // Try to drop the new database.
        RetryCommandSync(provider, dbName);

        Console.WriteLine("Command executed successfully.");

        provider.Retrying -= retryEvent;
    }

    private static void RetryCommandSync(SqlRetryLogicBaseProvider provider, string dbName)
    {
        using var cmd = s_generalConnection.CreateCommand();
        cmd.CommandText = string.Format(DropDatabaseFormat, dbName);
        // 3. Assign the `provider` to the command
        cmd.RetryLogicProvider = provider;
        Console.WriteLine("The first attempt, before getting into the retry logic.");
        cmd.ExecuteNonQuery();
    }
// </Snippet2>
private class RetryCommandSample2
{
// <Snippet3>
    private static void RetryCommand(SqlRetryLogicBaseProvider provider)
    {
        // Change this if you already have a database with the same name in your database.
        string dbName = "RetryCommand_TestDatabase";

        // Subscribe to the retry event and discover active sessions in a database
        EventHandler<SqlRetryingEventArgs> retryEvent = (object s, SqlRetryingEventArgs e) =>
        {
            // Run just at first execution
            if (e.RetryCount == 1)
            {
                FindActiveSessions(s_generalConnection, dbName);
                Console.WriteLine($"Before exceeding {provider.RetryLogic.NumberOfTries} attempts.");
            }
        };

        provider.Retrying += retryEvent;

        // Create a new database.
        ExecuteCommand(s_generalConnection, string.Format(CreateDatabaseFormat, dbName));
        Console.WriteLine($"The '{dbName}' database is created.");

        // Open a connection to the newly created database to block it from being dropped.
        using var blockingCnn = new SqlConnection(string.Format(CnnStringFormat, dbName));
        blockingCnn.Open();
        Console.WriteLine($"Established a connection to '{dbName}' to block it from being dropped.");

        Console.WriteLine("Dropping the database...");
        // Try to drop the new database.
        RetryCommandAsync(provider, dbName).Wait();

        Console.WriteLine("Command executed successfully.");

        provider.Retrying -= retryEvent;
    }

    private static async Task RetryCommandAsync(SqlRetryLogicBaseProvider provider, string dbName)
    {
        using var cmd = s_generalConnection.CreateCommand();
        cmd.CommandText = string.Format(DropDatabaseFormat, dbName);
        // 3. Assign the `provider` to the command
        cmd.RetryLogicProvider = provider;
        Console.WriteLine("The first attempt, before getting into the retry logic.");
        await cmd.ExecuteNonQueryAsync();
    }
// </Snippet3>
}
private class RetryCommandSample3
{ 
// <Snippet4>
    private static void RetryCommand(SqlRetryLogicBaseProvider provider)
    {
        // Change this if you already have a database with the same name in your database.
        string dbName = "RetryCommand_TestDatabase";

        // Subscribe to the retry event and discover the active sessions in a database
        EventHandler<SqlRetryingEventArgs> retryEvent = (object s, SqlRetryingEventArgs e) =>
        {
            // Run just at first execution
            if (e.RetryCount == 1)
            {
                FindActiveSessions(s_generalConnection, dbName);
                Console.WriteLine($"Before exceeding {provider.RetryLogic.NumberOfTries} attempts.");
            }
        };

        provider.Retrying += retryEvent;

        // Create a new database.
        ExecuteCommand(s_generalConnection, string.Format(CreateDatabaseFormat, dbName));
        Console.WriteLine($"The '{dbName}' database is created.");

        // Open a connection to the newly created database to block it from being dropped.
        using var blockingCnn = new SqlConnection(string.Format(CnnStringFormat, dbName));
        blockingCnn.Open();
        Console.WriteLine($"Established a connection to '{dbName}' to block it from being dropped.");

        Console.WriteLine("Dropping the database...");
        // Try to drop the new database.
        RetryCommandBeginExecuteAsync(provider, dbName).Wait();

        Console.WriteLine("Command executed successfully.");

        provider.Retrying -= retryEvent;
    }

    private static async Task RetryCommandBeginExecuteAsync(SqlRetryLogicBaseProvider provider, string dbName)
    {
        using var cmd = s_generalConnection.CreateCommand();
        cmd.CommandText = string.Format(DropDatabaseFormat, dbName);
        // Execute the BeginExecuteXXX and EndExecuteXXX functions by using Task.Factory.FromAsync().
        // Apply the retry logic by using the ExecuteAsync function of the configurable retry logic provider.
        Console.WriteLine("The first attempt, before getting into the retry logic.");
        await provider.ExecuteAsync(cmd, () => Task.Factory.FromAsync(cmd.BeginExecuteNonQuery(), cmd.EndExecuteNonQuery));
    }
// </Snippet4>
}
}
