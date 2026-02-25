// Licensed to the .NET Foundation under one or more agreements.  The .NET Foundation licenses this
// file to you under the MIT license.  See the LICENSE file in the project root for more
// information.

using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

public class ActiveDirectoryInteractiveTests
{
    private readonly ITestOutputHelper _output;

    public ActiveDirectoryInteractiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Interactive")]
    public async Task TestConnection()
    {
        string connectionString = new SqlConnectionStringBuilder()
        {
            // This is an Azure SQL database accessible via MSFT-AzVPN.
            //
            // To successfully login, you must add your EntraID identity to the database using
            // commands like this:
            //
            //   use [Northwind];
            //   create user [<you>@microsoft.com] from external provider;
            //   alter role db_datareader add member [paulmedynski@microsoft.com];
            //
            // You must connect to the database as an admin in order run these commands, for example
            // as the testodbc@microsoft.com user via SQL username/password authentication.
            //
            DataSource = "adotest.database.windows.net",
            InitialCatalog = "Northwind",
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 180,
            // Force each Open() call below to establish a new connection.
            Pooling = false,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryInteractive
        }.ConnectionString;

        try
        {
            // Establish a connection using all of our Open() variants.
            //
            // Note that this won't necessarily prompt the user for interactive authentication due
            // to caching within the Azure package and/or the Microsoft Authentication Library
            // (MSAL), but it will verify that at least one interaction is required.
            //
            {
                using SqlConnection connection = new(connectionString);
                connection.Open();
            }
            {
                using SqlConnection connection = new(connectionString);
                connection.Open(SqlConnectionOverrides.OpenWithoutRetry);
            }
            {
                using SqlConnection connection = new(connectionString);
                await connection.OpenAsync(CancellationToken.None);
            }
            {
                using SqlConnection connection = new(connectionString);
                await connection.OpenAsync(
                    SqlConnectionOverrides.OpenWithoutRetry,
                    CancellationToken.None);
            }
        }
        catch (SqlException ex)
        {
            _output.WriteLine($"Exception: {ex}");

            // SqlException doesn't emit all of its errors via its ToString(), so we must do that
            // ourselves if we want to see everything.
            //
            // SqlErrorCollection doesn't support foreach, so we have to iterate by hand to get
            // properly typed SqlError instances.
            //
            for (int i = 0; i < ex.Errors.Count; i++)
            {
                _output.WriteLine($"Error[{i}]: {ex.Errors[i].ToString()}");
            }

            // Re-throw to fail the test.
            throw;
        }
    }
}
