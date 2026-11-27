// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

internal static class Program
{
    private static async Task<int> Main()
    {
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Microsoft.Data.SqlClient.Connection")
            .AddSource("Microsoft.Data.SqlClient.Command")
            .SetSampler(new AlwaysOnSampler())
            .AddConsoleExporter()
            .Build();
        using MeterProvider meters = Sdk.CreateMeterProviderBuilder()
            .AddMeter("Microsoft.Data.SqlClient")
            .AddConsoleExporter()
            .Build();

        using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
        using TDSServerEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        string connectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:127.0.0.1,{endpoint.ServerEndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectRetryCount = 0,
            ConnectTimeout = 3
        }.ConnectionString;

        Console.WriteLine("Local simulated TDS server; no real SQL Server or credentials.");
        using (SqlConnection connection = new(connectionString))
        {
            connection.Open();
        }
        using (SqlConnection connection = new(connectionString))
        {
            await connection.OpenAsync();
        }

        foreach (uint number in new uint[] { 18456, 4060, 40501 })
        {
            server.SetErrorBehavior(true, number, int.MaxValue, "Not exported");
            using (SqlConnection connection = new(connectionString))
            {
                try
                {
                    connection.Open();
                    Console.Error.WriteLine("Expected a simulated login rejection.");
                    return 1;
                }
                catch (SqlException exception) when (exception.Number == number)
                {
                    Console.WriteLine($"Expected simulated server rejection: {number} (sync).");
                }
            }
            using (SqlConnection connection = new(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    Console.Error.WriteLine("Expected a simulated login rejection.");
                    return 1;
                }
                catch (SqlException exception) when (exception.Number == number)
                {
                    Console.WriteLine($"Expected simulated server rejection: {number} (async).");
                }
            }
        }

        using (SqlConnection connection = new(connectionString))
        {
            try
            {
                await connection.OpenAsync(new CancellationToken(true));
                Console.Error.WriteLine("Expected cancellation.");
                return 1;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Expected cancellation.");
            }
        }

        // Statement spans and operation metrics use a separate server whose error class
        // (16) fails the command without dooming the connection.
        using TransientTdsErrorTdsServer statementServer = new(
            new TransientTdsErrorTdsServerArguments { ErrorClass = 16 });
        using TDSServerEndPoint statementEndpoint = new(statementServer)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        statementEndpoint.Start();
        string statementConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            DataSource = $"tcp:127.0.0.1,{statementEndpoint.ServerEndPoint.Port}"
        }.ConnectionString;
        using (SqlConnection connection = new(statementConnectionString))
        {
            connection.Open();
            using SqlCommand command = new("select 1", connection);
            command.ExecuteNonQuery();
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("Executed two successful commands.");

            statementServer.SetErrorBehavior(true, 208, int.MaxValue, "Not exported");
            try
            {
                await command.ExecuteNonQueryAsync();
                Console.Error.WriteLine("Expected a simulated invalid object error.");
                return 1;
            }
            catch (SqlException exception) when (exception.Number == 208)
            {
                Console.WriteLine("Expected simulated statement error: 208 (async).");
            }
        }

        return provider.ForceFlush() && meters.ForceFlush() ? 0 : 1;
    }
}
