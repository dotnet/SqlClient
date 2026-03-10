using System.CommandLine;

namespace Microsoft.Data.SqlClient.Samples.AzureAuthentication;

/// <summary>
/// Contains the application entry point responsible for parsing command-line arguments and
/// delegating execution to <see cref="App"/>.
/// </summary>
public static class EntryPoint
{
    /// <summary>
    /// Application entry point. Parses command-line arguments and executes the connectivity test.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>0 on success; non-zero on failure.</returns>
    public static int Main(string[] args)
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
            $"""
            {App.AppName}
            -----------------------------------------

            Validates SqlClient connectivity using EntraID (formerly Azure Active Directory)
            authentication.  Connects to SQL Server using the supplied connection string,
            which must specify the authentication method.

            Supply specific package versions when building to test different versions of the
            SqlClient suite, for example:

              -p:SqlClientVersion=7.0.0-preview4
              -p:AkvProviderVersion=7.0.1-preview2
              -p:AzureVersion=1.0.0-preview1

            Current package versions:
              SqlClient:     {PackageVersions.MicrosoftDataSqlClient}
              AKV Provider:  {PackageVersions.MicrosoftDataSqlClientAlwaysEncryptedAzureKeyVaultProvider}
              Azure:         {PackageVersions.AzureExtensionsVersion}
            """)
        {
            connectionStringOption,
            logOption,
            traceOption,
            verboseOption
        };

        int exitCode = 0;
        rootCommand.SetAction(parseResult =>
        {
            App.RunOptions options = new()
            {
                ConnectionString = parseResult.GetValue(connectionStringOption)!,
                LogEvents = parseResult.GetValue(logOption),
                Trace = parseResult.GetValue(traceOption),
                Verbose = parseResult.GetValue(verboseOption)
            };

            using App app = new();
            exitCode = app.Run(options);
        });

        rootCommand.Parse(args).Invoke();
        return exitCode;
    }
}
