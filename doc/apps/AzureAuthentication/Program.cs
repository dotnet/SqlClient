using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.Tracing;

using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

namespace AzureAuthentication;

public class Program : IDisposable
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

        Option<bool> logOption = new("--log-events", "-l")
        {
            Description = "Enable SqlClient event emission to the console."
        };

        Option<bool> traceOption = new("--trace", "-t")
        {
            Description = "Pauses execution to allow dotnet-trace to be attached."
        };

        Option<bool> verboseOption = new("--verbose", "-v")
        {
            Description = "Enable verbose output with detailed error information."
        };

        RootCommand rootCommand = new(
            AppName + Environment.NewLine +
            "---------------------------" + Environment.NewLine +
            Environment.NewLine +
            "Validates SqlClient connectivity using EntraID (formerly Azure Active Directory) " +
            "authentication.  Connects to SQL Server using the supplied connection string, " +
            "which must specify the authentication method." + Environment.NewLine +
            Environment.NewLine +
            "Supply specific package versions when building to test different versions of the " +
            "SqlClient suite, for example:" + Environment.NewLine +
            Environment.NewLine +
            "  -p:SqlClientVersion=7.0.0.preview4" + Environment.NewLine +
            "  -p:AkvProviderVersion=7.0.1-preview2" + Environment.NewLine +
            "  -p:AzureVersion=1.0.0-preview1")
        {
            connectionStringOption,
            logOption,
            traceOption,
            verboseOption
        };

        rootCommand.SetAction(parseResult =>
        {
            string connectionString = parseResult.GetValue(connectionStringOption)!;
            bool logEvents = parseResult.GetValue(logOption);
            bool trace = parseResult.GetValue(traceOption);
            bool verbose = parseResult.GetValue(verboseOption);

            using Program app = new();
            return app.Run(connectionString, logEvents, trace, verbose);
        });

        return rootCommand.Parse(args).Invoke();
    }

    public void Dispose()
    {
        _eventListener?.Dispose();
    }

    internal int Run(string connectionString, bool logEvents, bool trace, bool verbose)
    {
        Out(AppName);
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

        // Enable SqlClient event logging if requested.
        if (logEvents)
        {
            Out("SqlClient event logging enabled; events will be prefixed with " +
                SqlClientEventListener.Prefix);

            _eventListener = new SqlClientEventListener(Out);
        }

        // Pause for trace attachment if requested.
        if (trace)
        {
            Out("Execution paused; attach dotnet-trace and press Enter to resume:");
            Out("");
            Out($"  dotnet-trace collect -p {Process.GetCurrentProcess().Id} " +
                "--providers Microsoft.Data.SqlClient.EventSource:1FFF:5");
            Out("");
            Console.ReadLine();
        }

        // Instantiate the AKV Provider to ensure its assembly is present.
        #pragma warning disable CA1806
        new SqlColumnEncryptionAzureKeyVaultProvider(new DefaultAzureCredential(true));
        #pragma warning restore CA1806

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

    private const string AppName = "Azure Authentication Tester";

    private SqlClientEventListener? _eventListener;

    internal static void Out(string message)
    {
        Console.Out.WriteLine(message);
    }

    internal static void Err(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }

    /// <summary>
    /// Listens for events from <c>Microsoft.Data.SqlClient.EventSource</c> and
    /// emits them via the supplied output function.
    /// </summary>
    private sealed class SqlClientEventListener : EventListener
    {
        public const string Prefix = "[EVENT]";

        private readonly Action<string> _out;

        internal SqlClientEventListener(Action<string> output)
        {
            _out = output;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.Equals("Microsoft.Data.SqlClient.EventSource", StringComparison.Ordinal))
            {
                // Enable all keywords at all levels.
                EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            _out($"[EVENT] {eventData.EventName}: " +
                (eventData.Payload != null && eventData.Payload.Count > 0
                ? eventData.Payload[0]
                : string.Empty));
        }
    }
}
