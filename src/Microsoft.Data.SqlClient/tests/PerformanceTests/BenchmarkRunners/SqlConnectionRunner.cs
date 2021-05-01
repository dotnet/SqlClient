// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class SqlConnectionRunner : BaseRunner
    {
        [GlobalCleanup]
        public static void Dispose() => SqlConnection.ClearAllPools();

        [Benchmark]
        public static void OpenPooledConnection()
        {
            using var sqlConnection = new SqlConnection(s_config.ConnectionString + ";Pooling=yes;");
            sqlConnection.Open();
        }

        [Benchmark]
        public static void OpenNonPooledConnection()
        {
            using var sqlConnection = new SqlConnection(s_config.ConnectionString + ";Pooling=no;");
            sqlConnection.Open();
        }

        [Benchmark]
        public static async Task OpenAsyncPooledConnection()
        {
            using var sqlConnection = new SqlConnection(s_config.ConnectionString + ";Pooling=yes;");
            await sqlConnection.OpenAsync();
        }

        [Benchmark]
        public static async Task OpenAsyncNonPooledConnection()
        {
            using var sqlConnection = new SqlConnection(s_config.ConnectionString + ";Pooling=no;");
            await sqlConnection.OpenAsync();
        }
    }
}
