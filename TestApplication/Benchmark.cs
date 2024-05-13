using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SqlClientX;

namespace TestApplication
{
    [MemoryDiagnoser]
    public class Benchmarks
    {
        //const string ConnectionString = "...";
        static string ConnectionString = $"Server=tcp:192.168.1.83;" +
                        $"Min Pool Size=120;Max Pool Size = 200;User Id=sa; pwd={Environment.GetEnvironmentVariable("SQL_PWD")}; " +
                        "Connection Timeout=30;TrustServerCertificate=True;Timeout=0;Encrypt=False;Database=testdatabase;Pooling=False;" +
                        "Application Name=TestAppX"; // pooled
        [Params(CommandBehavior.SequentialAccess)]
        public CommandBehavior Behavior { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            using (var cmd = new SqlCommand("IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TextTable' AND xtype='U') CREATE TABLE [TextTable] ([Text] VARCHAR(MAX))", conn))
                cmd.ExecuteNonQuery();

            using (var cmd = new SqlCommand("INSERT INTO [TextTable] ([Text]) VALUES (@p)", conn))
            {
                cmd.Parameters.AddWithValue("p", new string('x', 1024 * 1024 * 5));
                cmd.ExecuteNonQuery();
            }
        }

        [Benchmark]
        public async ValueTask<int> Async()
        {
            using var conn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("SELECT [Text] FROM [TextTable]", conn);
            await conn.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync(Behavior);
            await reader.ReadAsync();
            return (await reader.GetFieldValueAsync<string>(0)).Length;
        }

        [Benchmark]
        public async ValueTask<int> Sync()
        {
            using var conn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("SELECT [Text] FROM [TextTable]", conn);
            conn.Open();

            using var reader = cmd.ExecuteReader(Behavior);
            reader.Read();
            return reader.GetFieldValue<string>(0).Length;
        }

        [Benchmark]
        public async ValueTask<int> SyncX()
        {
            using var conn = new SqlConnectionX(ConnectionString);
            using var cmd = new SqlCommandX("SELECT [Text] FROM [TextTable]", conn);
            conn.Open();

            using var reader = cmd.ExecuteReader();
            reader.Read();
            return reader.GetFieldValue<string>(0).Length;
        }

        [Benchmark]
        public async ValueTask<int> ASyncX()
        {
            using var conn = new SqlConnectionX(ConnectionString);
            using var cmd = new SqlCommandX("SELECT [Text] FROM [TextTable]", conn);
            await conn.OpenAsync();

            await using SqlDataReaderX reader = await cmd.ExecuteReaderAsync(CancellationToken.None);
            await reader.ReadAsync();
            return (await reader.GetFieldValueAsync<string>(0)).Length;
        }
    }

}
