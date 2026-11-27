// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.Data.SqlClient;

// Drives the local SQL Server container through the private OTel Endpoint route. The password
// is read from the process environment only and is never printed.
string password = Environment.GetEnvironmentVariable("SQLCLIENT_DEMO_PASSWORD")
    ?? throw new InvalidOperationException("Run through run.ps1.");
string endpoint = Environment.GetEnvironmentVariable("SQLCLIENT_DEMO_OTEL_ENDPOINT")
    ?? "http://127.0.0.1:4318/v1/traces";
TimeSpan duration = TimeSpan.FromSeconds(args.Length > 0 ? int.Parse(args[0]) : 180);

string Build(string user, string database) => new SqlConnectionStringBuilder
{
    DataSource = "tcp:127.0.0.1,14333",
    UserID = user,
    Password = password,
    InitialCatalog = database,
    TrustServerCertificate = true,
    Pooling = false,
    ConnectRetryCount = 0,
    ConnectTimeout = 5,
    OtelEndpoint = endpoint
}.ConnectionString;

string good = Build("sa", "master");
string badLogin = Build("sqlclient_demo_missing_login", "master");
string badDatabase = Build("sa", "sqlclient_demo_missing_db");
Dictionary<string, int> counts = new();
void Count(string name) => counts[name] = counts.GetValueOrDefault(name) + 1;

async Task Expect(string name, Func<Task> work)
{
    try
    {
        await work();
        Count(name + ":ok");
    }
    catch (SqlException error)
    {
        Count($"{name}:{error.Number}");
    }
}

Stopwatch clock = Stopwatch.StartNew();
for (int i = 0; clock.Elapsed < duration; i++)
{
    await Expect("open+select", async () =>
    {
        using SqlConnection connection = new(good);
        await connection.OpenAsync();
        using SqlCommand command = new("SELECT 1", connection);
        await command.ExecuteScalarAsync();
    });
    if (i % 3 == 0)
    {
        await Expect("waitfor", () =>
        {
            using SqlConnection connection = new(good);
            connection.Open();
            using SqlCommand command = new("WAITFOR DELAY '00:00:02'", connection);
            command.ExecuteNonQuery();
            return Task.CompletedTask;
        });
        await Expect("syntax", async () =>
        {
            using SqlConnection connection = new(good);
            await connection.OpenAsync();
            using SqlCommand command = new("SELECT * FROM", connection);
            await command.ExecuteNonQueryAsync();
        });
    }
    if (i % 4 == 0)
    {
        await Expect("bad-login", async () =>
        {
            using SqlConnection connection = new(badLogin);
            await connection.OpenAsync();
        });
        await Expect("bad-database", () =>
        {
            using SqlConnection connection = new(badDatabase);
            connection.Open();
            return Task.CompletedTask;
        });
    }
    await Task.Delay(500);
}

foreach (KeyValuePair<string, int> entry in counts.OrderBy(e => e.Key))
{
    Console.WriteLine($"{entry.Key} = {entry.Value}");
}
// Leave time for the 10 s metric reader and the batch span exporter before process exit.
await Task.Delay(TimeSpan.FromSeconds(12));
