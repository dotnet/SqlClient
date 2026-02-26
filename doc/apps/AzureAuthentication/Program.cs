using System.CommandLine;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

namespace AzureAuthentication;

public class Program
{
    static int Main(string[] args)
    {
        Option<string> connectionStringOption = new("--connection-string", "-c")
        {
            Description =
                "The ADO.NET connection string used to connect to SQL Server. " +
                "Supports SQL, Azure AD, and integrated authentication modes.",
            Required = true
        };

        Option<bool> logOption = new("--log-sqlclient-events", "-l")
        {
            Description = "Enable SqlClient event emission to the console."
        };

        Option<bool> verboseOption = new("--verbose", "-v")
        {
            Description = "Enable verbose output with detailed error information."
        };

        RootCommand rootCommand = new(
            "Validates SqlClient connectivity using EntraID (formerly Azure Active Directory) " +
            "authentication.  Connects to SQL Server using the supplied connection string, " +
            "which must specify the authentication method.")
        {
            connectionStringOption,
            logOption,
            verboseOption
        };

        rootCommand.SetAction(parseResult =>
        {
            string connectionString = parseResult.GetValue(connectionStringOption)!;
            bool verbose = parseResult.GetValue(verboseOption);
            bool logEvents = parseResult.GetValue(logOption);
            return new Program().Run(connectionString, logEvents, verbose);
        });

        return rootCommand.Parse(args).Invoke();
    }

    internal int Run(string connectionString, bool logEvents, bool verbose)
    {
        Out("Azure Authentication Tester");
        Out("---------------------------");
        Out("");
        Out("Packages used:");
        Out($"  SqlClient:     {PackageVersions.MicrosoftDataSqlClient}");
        Out($"  AKV Provider:  {PackageVersions.MicrosoftDataSqlClientAlwaysEncryptedAzureKeyVaultProvider}");
        Out($"  Azure:         {PackageVersions.MicrosoftDataSqlClientExtensionsAzure}");
        Out("");

        try
        {
            // Canonicalize the connection string for emission.
            SqlConnectionStringBuilder builder = new(connectionString);

            Out($"Connection details:");
            Out($"  Data Source:      {builder.DataSource}");
            Out($"  Initial Catalog:  {builder.InitialCatalog}");
            Out($"  Authentication:   {builder.Authentication}");
            Out("");

            if (verbose)
            {
                Out($"Full connection string:");
                Out($"  {builder}");
                Out("");
            }
        }
        catch (Exception ex)
        {
            Err("Failed to parse connection string:");
            Err($"  {ex.Message}");

            if (verbose)
            {
                Err($"  {ex}");
            }
            return 1;
        }

        // Instantiate the AKV Provider to ensure its assembly is present.
        #pragma warning disable CA1806
        new SqlColumnEncryptionAzureKeyVaultProvider(new DefaultAzureCredential(true));
        #pragma warning restore CA1806

        // Enable SqlClient event logging if requested.
        if (logEvents)
        {
            Out("SqlClient event logging enabled; app messages will be prefixed with [APP]");
            OutPrefix = "[APP] ";

            // SqlClientEventSource.LogWriter = Console.Out;
        }

        try
        {
            Out("Testing connectivity...");

            using SqlConnection connection = new(connectionString);
            connection.Open();

            Console.ForegroundColor = ConsoleColor.Green;
            Out("Connected successfully!");
            Console.ResetColor();
            Out($"  Server version: {connection.ServerVersion}");

            return 0;
        }
        catch (Exception ex)
        {
            Err("Connection failed:");
            Err($"  {ex.Message}");

            if (verbose)
            {
                Err($"  {ex}");
            }

            return 1;
        }
    }

    internal string OutPrefix { get; set; } = string.Empty;

    internal void Out(string message)
    {
        Console.Out.WriteLine($"{OutPrefix}{message}");
    }

    internal void Err(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"{OutPrefix}{message}");
        Console.ResetColor();
    }
}
