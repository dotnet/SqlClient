// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using static Microsoft.Data.SqlClient.PerformanceTests.Constants;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class DataTypeReaderAsyncRunner : BaseRunner
    {
        private long _rowCount;
        private string _query(string name) => $"SELECT * FROM {name}";

        [GlobalSetup]
        public void Setup()
        {
            _rowCount = s_config.Benchmarks.DataTypeReaderRunnerConfig.RowCount;
        }

        [GlobalCleanup]
        public void Dispose()
        {
            SqlConnection.ClearAllPools();
        }

        [IterationCleanup]
        public void ResetConnection()
        {
            SqlConnection.ClearAllPools();
        }

        private async Task RunBenchmarkAsync(DataType type)
        {
            using SqlConnection sqlConnection = new(s_config.ConnectionString);
            sqlConnection.Open();
            Table t = Table.Build(nameof(SqlCommandRunner))
                .AddColumn(new Column(type))
                .CreateTable(sqlConnection)
                .InsertBulkRows(_rowCount, sqlConnection);
            try
            {
                using SqlCommand sqlCommand = new(_query(t.Name), sqlConnection);
                using SqlDataReader reader = await sqlCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                { }
            }
            finally
            {
                t.DropTable(sqlConnection);
            }
        }

        [Benchmark]
        public async Task BitAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_bit]);

        [Benchmark]
        public async Task IntAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_int]);

        [Benchmark]
        public async Task TinyIntAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_tinyint]);

        [Benchmark]
        public async Task SmallIntAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_smallint]);

        [Benchmark]
        public async Task BigIntAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_bigint]);

        [Benchmark]
        public async Task MoneyAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_money]);

        [Benchmark]
        public async Task SmallMoneyAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_smallmoney]);

        [Benchmark]
        public async Task DecimalAsync() => await RunBenchmarkAsync(s_datatypes.Decimals[d_decimal]);

        [Benchmark]
        public async Task NumericAsync() => await RunBenchmarkAsync(s_datatypes.Decimals[d_numeric]);

        [Benchmark]
        public async Task FloatAsync() => await RunBenchmarkAsync(s_datatypes.Decimals[d_float]);

        [Benchmark]
        public async Task RealAsync() => await RunBenchmarkAsync(s_datatypes.Decimals[d_real]);

        [Benchmark]
        public async Task DateAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_date]);

        [Benchmark]
        public async Task DatetimeAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_datetime]);

        [Benchmark]
        public async Task Datetime2Async() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_datetime2]);

        [Benchmark]
        public async Task TimeAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_time]);

        [Benchmark]
        public async Task SmallDateTimeAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_smalldatetime]);

        [Benchmark]
        public async Task DateTimeOffsetAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_datetimeoffset]);

        [Benchmark]
        public async Task CharAsync() => await RunBenchmarkAsync(s_datatypes.Characters[c_char]);

        [Benchmark]
        public async Task NCharAsync() => await RunBenchmarkAsync(s_datatypes.Characters[c_nchar]);

        [Benchmark]
        public async Task BinaryAsync() => await RunBenchmarkAsync(s_datatypes.Binary[b_binary]);

        [Benchmark]
        public async Task VarCharAsync() => await RunBenchmarkAsync(s_datatypes.MaxTypes[m_varchar]);

        [Benchmark]
        public async Task NVarCharAsync() => await RunBenchmarkAsync(s_datatypes.MaxTypes[m_nvarchar]);

        [Benchmark]
        public async Task VarBinaryAsync() => await RunBenchmarkAsync(s_datatypes.MaxTypes[m_varbinary]);

        [Benchmark]
        public async Task UniqueIdentifierAsync() => await RunBenchmarkAsync(s_datatypes.Others[o_uniqueidentifier]);

        [Benchmark]
        public async Task XmlAsync() => await RunBenchmarkAsync(s_datatypes.Others[o_xml]);
    }
}
