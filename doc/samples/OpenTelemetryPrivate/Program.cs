// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using OpenTelemetry;
using OpenTelemetry.Trace;

using TracerProvider? applicationProvider = args.Contains("--both")
    ? Sdk.CreateTracerProviderBuilder()
        .AddSource("Microsoft.Data.SqlClient.Connection")
        .AddSource("Microsoft.Data.SqlClient.Command")
        .AddConsoleExporter()
        .Build()
    : null;

using LocalCollector collectorA = new();
using LocalCollector collectorB = new();
using TransientTdsErrorTdsServer server = new(new TransientTdsErrorTdsServerArguments());
using TDSServerEndPoint endpoint = new(server)
{
    ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
};
endpoint.Start();

foreach ((LocalCollector collector, uint number) in
    new[] { (collectorA, 18456u), (collectorB, 4060u) })
{
    server.SetErrorBehavior(true, number, int.MaxValue, "Not exported");
    SqlConnectionStringBuilder builder = new()
    {
        DataSource = $"tcp:127.0.0.1,{endpoint.ServerEndPoint.Port}",
        Encrypt = SqlConnectionEncryptOption.Optional,
        Pooling = false,
        ConnectRetryCount = 0,
        ConnectTimeout = 3,
        OtelEndpoint = collector.Endpoint
    };
    using SqlConnection connection = new(builder.ConnectionString);
    try
    {
        if (number == 18456)
        {
            connection.Open();
        }
        else
        {
            await connection.OpenAsync();
        }
        throw new InvalidOperationException("The simulated server should reject the login.");
    }
    catch (SqlException error) when (error.Number == number)
    {
        Console.WriteLine($"Expected simulated server rejection: {number}.");
    }
}

// Statement spans and metrics route to collector A only. The statement server uses
// error class 16 so the command fails without dooming the connection.
using TransientTdsErrorTdsServer statementServer = new(
    new TransientTdsErrorTdsServerArguments { ErrorClass = 16 });
using TDSServerEndPoint statementEndpoint = new(statementServer)
{
    ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
};
statementEndpoint.Start();
SqlConnectionStringBuilder statementBuilder = new()
{
    DataSource = $"tcp:127.0.0.1,{statementEndpoint.ServerEndPoint.Port}",
    Encrypt = SqlConnectionEncryptOption.Optional,
    Pooling = false,
    ConnectRetryCount = 0,
    ConnectTimeout = 3,
    OtelEndpoint = collectorA.Endpoint
};
using (SqlConnection connection = new(statementBuilder.ConnectionString))
{
    await connection.OpenAsync();
    using SqlCommand command = new("select 1", connection);
    await command.ExecuteNonQueryAsync();
    statementServer.SetErrorBehavior(true, 208, int.MaxValue, "Not exported");
    try
    {
        command.ExecuteNonQuery();
        throw new InvalidOperationException("The simulated server should reject the command.");
    }
    catch (SqlException error) when (error.Number == 208)
    {
        Console.WriteLine("Expected simulated statement error: 208.");
    }
}

TimeSpan wait = TimeSpan.FromSeconds(30);
string payloadA = await collectorA.WaitForTracesAsync(
    t => t.Contains("18456") && t.Contains("statement.execute") && t.Contains("208"), wait);
string payloadB = await collectorB.WaitForTracesAsync(t => t.Contains("4060"), wait);
string metricsA = await collectorA.WaitForMetricsAsync(
    m => m.Contains("db.client.operation.count") &&
        m.Contains("db.client.operation.error.count"), wait);
if (payloadA.Contains("4060") ||
    payloadB.Contains("18456") || payloadB.Contains("statement.execute") ||
    collectorB.Metrics.Contains("db.client.operation") ||
    !payloadA.Contains("connection.open.attempt") ||
    !payloadB.Contains("connection.open.attempt") ||
    !payloadA.Contains("connection.physical_open") ||
    !payloadB.Contains("connection.physical_open") ||
    !payloadA.Contains("connection.transport") ||
    !payloadB.Contains("connection.transport") ||
    payloadA.Contains("select 1") ||
    payloadA.Contains("Not exported") || payloadB.Contains("Not exported") ||
    metricsA.Contains("Not exported") ||
    payloadA.Contains(collectorA.Endpoint) || payloadB.Contains(collectorB.Endpoint))
{
    throw new InvalidOperationException("Private collector routing or payload validation failed.");
}

Console.WriteLine("Collector A received only 18456 and the statement spans/metrics; " +
    "collector B received only 4060.");
Console.WriteLine(args.Contains("--both")
    ? "The application SDK also exported the trace trees to the console."
    : "No application-owned SDK was configured.");

// Accepts OTLP/HTTP posts and keeps trace and metric payloads separately by path.
internal sealed class LocalCollector : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly StringBuilder _traces = new();
    private readonly StringBuilder _metrics = new();

    internal LocalCollector()
    {
        using TcpListener port = new(IPAddress.Loopback, 0);
        port.Start();
        int number = ((IPEndPoint)port.LocalEndpoint).Port;
        port.Stop();
        Endpoint = $"http://127.0.0.1:{number}/v1/traces";
        _listener.Prefixes.Add($"http://127.0.0.1:{number}/");
        _listener.Start();
        _ = ReceiveAsync();
    }

    internal string Endpoint { get; }

    internal string Metrics
    {
        get
        {
            lock (_metrics)
            {
                return _metrics.ToString();
            }
        }
    }

    internal Task<string> WaitForTracesAsync(Func<string, bool> done, TimeSpan timeout) =>
        WaitAsync(_traces, done, timeout);

    internal Task<string> WaitForMetricsAsync(Func<string, bool> done, TimeSpan timeout) =>
        WaitAsync(_metrics, done, timeout);

    private static async Task<string> WaitAsync(
        StringBuilder target, Func<string, bool> done, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            string text;
            lock (target)
            {
                text = target.ToString();
            }
            if (done(text))
            {
                return text;
            }
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The local collector did not receive the payload.");
            }
            await Task.Delay(100);
        }
    }

    private async Task ReceiveAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (!_listener.IsListening)
            {
                return;
            }
            using MemoryStream data = new();
            await context.Request.InputStream.CopyToAsync(data);
            StringBuilder target = context.Request.Url?.AbsolutePath == "/v1/metrics"
                ? _metrics
                : _traces;
            lock (target)
            {
                target.Append(Encoding.UTF8.GetString(data.ToArray()));
            }
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/x-protobuf";
            context.Response.ContentLength64 = 0;
            context.Response.Close();
        }
    }

    public void Dispose() => _listener.Close();
}