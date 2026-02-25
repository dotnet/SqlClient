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
        SqlConnectionStringBuilder builder = new()
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
            Authentication = SqlAuthenticationMethod.ActiveDirectoryInteractive
        };

        var connection = new SqlConnection(builder.ConnectionString);

        try
        {
            connection.Open();
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
