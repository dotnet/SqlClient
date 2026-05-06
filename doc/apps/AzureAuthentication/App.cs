using System.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

namespace Microsoft.Data.SqlClient.Samples.AzureAuthentication;

/// <summary>
/// Console application that validates SqlClient connectivity using Entra ID (formerly Azure Active
/// Directory) authentication.
/// </summary>
public class App : IDisposable
{
    // ──────────────────────────────────────────────────────────────────
    #region Construction / Disposal

    /// <inheritdoc />
    public void Dispose()
    {
        _eventListener?.Dispose();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Internal Methods

    /// <summary>
    /// Options for <see cref="Run"/>.
    /// </summary>
    internal class RunOptions
    {
        /// <summary>
        /// The ADO.NET connection string to use.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// When <see langword="true"/>, SqlClient events are emitted to the console.
        /// </summary>
        public bool LogEvents { get; set; } = false;

        /// <summary>
        /// When <see langword="true"/>, execution pauses to allow dotnet-trace attachment.
        /// </summary>
        public bool Trace { get; set; } = false;

        /// <summary>
        /// When <see langword="true"/>, detailed error information is displayed.
        /// </summary>
        public bool Verbose { get; set; } = false;
    }

    /// <summary>
    /// Runs the connectivity test against SQL Server using the specified options.
    /// </summary>
    /// <param name="options">The options controlling the connectivity test.</param>
    /// <returns>0 on success; non-zero on failure.</returns>
    internal int Run(RunOptions options)
    {
        Out($"""
            {AppName}
            ---------------------------

            Packages used:
              SqlClient:     {PackageVersions.MicrosoftDataSqlClient}
              AKV Provider:  {PackageVersions.MicrosoftDataSqlClientAlwaysEncryptedAzureKeyVaultProvider}
              Azure:         {PackageVersions.AzureExtensionsVersion}

            """);

        try
        {
            // Canonicalize the connection string for emission.
            SqlConnectionStringBuilder builder = new(options.ConnectionString);

            Out($"""
                Connection details:
                  Data Source:      {builder.DataSource}
                  Initial Catalog:  {builder.InitialCatalog}
                  Authentication:   {builder.Authentication}

                """);

            if (options.Verbose)
            {
                Out($"""
                    Full connection string:
                      {builder}

                    """);
            }
        }
        catch (Exception ex)
        {
            Err($"""
                Failed to parse connection string:
                  {ex.Message}
                """);

            if (options.Verbose)
            {
                Err($"  {ex}");
            }
            return 1;
        }

        // Enable SqlClient event logging if requested.
        if (options.LogEvents)
        {
            string prefix = "[EVENT]";
            Out($"SqlClient event logging enabled; events will be prefixed with {prefix}");

            _eventListener = new SqlClientEventListener(Out, prefix);
        }

        // Pause for trace attachment if requested.
        if (options.Trace)
        {
            Out($"""
                Execution paused; attach dotnet-trace and press Enter to resume:

                  dotnet-trace collect -p {Process.GetCurrentProcess().Id} --providers Microsoft.Data.SqlClient.EventSource:1FFF:5

                """);
            Console.ReadLine();
        }

        // Touch the AKV Provider type to ensure its assembly is present and loadable.
        _ = typeof(SqlColumnEncryptionAzureKeyVaultProvider);

        try
        {
            Out("Testing connectivity...");

            using SqlConnection connection = new(options.ConnectionString);
            connection.Open();

            Console.ForegroundColor = ConsoleColor.Green;
            Out("Connected successfully!");
            Console.ResetColor();
            Out($"  Server version: {connection.ServerVersion}");

            return 0;
        }
        catch (Exception ex)
        {
            Err($"""
                Connection failed:
                  {ex.Message}
                """);

            if (options.Verbose)
            {
                Err($"  {ex}");
            }

            return 1;
        }
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Private Helpers

    /// <summary>
    /// Writes an informational message to standard output.
    /// </summary>
    /// <param name="message">The message to write.</param>
    internal static void Out(string message)
    {
        Console.Out.WriteLine(message);
    }

    /// <summary>
    /// Writes an error message to standard error in red.
    /// </summary>
    /// <param name="message">The message to write.</param>
    internal static void Err(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Private Fields

    /// <summary>
    /// The display name of the application.
    /// </summary>
    internal const string AppName = "Azure Authentication Tester";

    /// <summary>
    /// The optional event listener used to capture SqlClient diagnostic events.
    /// </summary>
    private SqlClientEventListener? _eventListener;

    #endregion
}
