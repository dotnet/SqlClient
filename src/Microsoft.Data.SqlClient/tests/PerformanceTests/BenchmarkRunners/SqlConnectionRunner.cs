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

        /// <summary>
        /// Whether MARS is enabled or disabled on connection string
        /// </summary>
        [Params(true, false)]
        public bool MARS { get; set; }

        /// <summary>
        /// Whether Connection Pooling is enabled or disabled on connection string
        /// </summary>
        [Params(true, false)]
        public bool Pooling { get; set; }

        [Benchmark]
        public void OpenConnection()
        {
            using var sqlConnection = new SqlConnection(s_config.ConnectionString + $";Pooling={Pooling};MultipleActiveResultSets={MARS}");
            sqlConnection.Open();
        }

        [Benchmark]
        public async Task OpenAsyncConnection()
        {
            using var sqlConnection = new SqlConnection(s_config.ConnectionString + $";Pooling={Pooling};MultipleActiveResultSets={MARS}");
            await sqlConnection.OpenAsync();
        }
    }
}
