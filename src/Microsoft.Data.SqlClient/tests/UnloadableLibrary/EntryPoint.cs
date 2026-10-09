// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.Tests.Common.ConfigurableRetryLogic;

namespace Microsoft.Data.SqlClient.UnloadableLibrary;

/// <summary>
/// Entry point and hooks for the unloadable assembly load context test.
/// </summary>
/// <remarks>
/// This is organised into a separate class and assembly to provide isolation from the ManualTesting
/// project. Loading an assembly loads all of its dependencies, so this isolation means that if the
/// test fails, we can be confident that the failure is due to SqlClient itself rather than to another
/// assembly that was loaded into the same context.
/// </remarks>
public sealed class EntryPoint : IDisposable
{
    private readonly LocalAppContextSwitchesHelper _appContextSwitchesHelper;

    public static string? AssemblyLoadContextName =>
        #if NET
        System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(SqlConnection).Assembly)?.Name;
        #else
        "Default";
        #endif

    public string ConnectionString { get; }

    /// <summary>
    /// Instantiates the entry point, specifying the connection string to use.
    /// </summary>
    /// <remarks>
    /// This sets up the AppContext switch interface and forces the configurable retry logic loader
    /// to be created, triggering the loading of SqlClient into the current AssemblyLoadContext.
    /// </remarks>
    /// <param name="connectionString">Connection string to use for the test.</param>
    public EntryPoint(string connectionString)
    {
        _appContextSwitchesHelper = new LocalAppContextSwitchesHelper();
        #if NET
        _appContextSwitchesHelper.UseManagedNetworking = true;
        #endif

        ConnectionString = connectionString;

        InitializeRetryProviders();
    }

    private void InitializeRetryProviders()
    {
        RetryLogicConfigs connectionConfigs = new()
        {
            DeltaTime = TimeSpan.FromSeconds(1),
            MaxTimeInterval = TimeSpan.FromSeconds(30),
            MinTimeInterval = TimeSpan.FromSeconds(1),
            NumberOfTries = 1,
            RetryLogicType = typeof(SqlConfigurableRetryFactory).FullName,
            RetryMethodName = nameof(SqlConfigurableRetryFactory.CreateNoneRetryProvider)
        };
        RetryLogicConfigs commandConfigs = RetryLogicLoaderHelper.CreateRandomConfig(method: null, authorizedSqlCondition: null);

        RetryLogicLoaderHelper.CreateLoader(connectionConfigs, commandConfigs);
    }

    /// <summary>
    /// Executes a simple SQL command to retrieve the current date and time from the database.
    /// </summary>
    /// <seealso cref="GetDateAsync"/>
    public void GetDate()
    {
        using SqlConnection conn = new(ConnectionString);
        using SqlCommand cmd = new("SELECT GETDATE()", conn);

        conn.Open();
        cmd.ExecuteScalar();
    }

    /// <summary>
    /// Asynchronously executes a simple SQL command to retrieve the current date and time from the database.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <seealso cref="GetDate"/>
    public async Task GetDateAsync()
    {
        using SqlConnection conn = new(ConnectionString);
        using SqlCommand cmd = new("SELECT GETDATE()", conn);

        await conn.OpenAsync().ConfigureAwait(false);
        await cmd.ExecuteScalarAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes of the resources used by the entry point, including the AppContext switch helper.
    /// </summary>
    public void Dispose()
    {
        _appContextSwitchesHelper.Dispose();
    }
}
