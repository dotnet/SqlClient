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
        private static long s_rowCount;
        private static string _query(string name) => $"SELECT * FROM {name}";

        [GlobalSetup]
        public static void Setup()
        {
            s_rowCount = s_config.Benchmarks.DataTypeReaderRunnerConfig.RowCount;
        }

        [GlobalCleanup]
        public static void Dispose()
        {
            SqlConnection.ClearAllPools();
        }

        [IterationCleanup]
        public static void ResetConnection()
        {
            SqlConnection.ClearAllPools();
        }

        private static async Task RunBenchmarkAsync(DataType type)
        {
            using SqlConnection sqlConnection = new(s_config.ConnectionString);
            sqlConnection.Open();
            Table t = Table.Build(nameof(SqlCommandRunner))
                .AddColumn(new Column(type))
                .CreateTable(sqlConnection)
                .InsertBulkRows(s_rowCount, sqlConnection);
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
        public static async void BitAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_bit]);

        [Benchmark]
        public static async void IntAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_int]);

        [Benchmark]
        public static async void TinyIntAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_tinyint]);

        [Benchmark]
        public static async void SmallIntAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_smallint]);

        [Benchmark]
        public static async void BigIntAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_bigint]);

        [Benchmark]
        public static async void MoneyAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_money]);

        [Benchmark]
        public static async void SmallMoneyAsync() => await RunBenchmarkAsync(s_datatypes.Numerics[n_smallmoney]);

        [Benchmark]
        public static async void DecimalAsync() => await RunBenchmarkAsync(s_datatypes.Decimals[d_decimal]);

        [Benchmark]
        public static async void NumericAsync() => await RunBenchmarkAsync(s_datatypes.Decimals[d_numeric]);

        [Benchmark]
        public static async void FloatAsync() => await RunBenchmarkAsync(s_datatypes.Decimals[d_float]);

        [Benchmark]
        public static async void RealAsync() => await RunBenchmarkAsync(s_datatypes.Decimals[d_real]);

        [Benchmark]
        public static async void DateAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_date]);

        [Benchmark]
        public static async void DatetimeAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_datetime]);

        [Benchmark]
        public static async void Datetime2Async() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_datetime2]);

        [Benchmark]
        public static async void TimeAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_time]);

        [Benchmark]
        public static async void SmallDateTimeAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_smalldatetime]);

        [Benchmark]
        public static async void DateTimeOffsetAsync() => await RunBenchmarkAsync(s_datatypes.DateTimes[t_datetimeoffset]);

        [Benchmark]
        public static async void CharAsync() => await RunBenchmarkAsync(s_datatypes.Characters[c_char]);

        [Benchmark]
        public static async void NCharAsync() => await RunBenchmarkAsync(s_datatypes.Characters[c_nchar]);

        [Benchmark]
        public static async void BinaryAsync() => await RunBenchmarkAsync(s_datatypes.Binary[b_binary]);

        [Benchmark]
        public static async void VarCharAsync() => await RunBenchmarkAsync(s_datatypes.MaxTypes[m_varchar]);

        [Benchmark]
        public static async void NVarCharAsync() => await RunBenchmarkAsync(s_datatypes.MaxTypes[m_nvarchar]);

        [Benchmark]
        public static async void VarBinaryAsync() => await RunBenchmarkAsync(s_datatypes.MaxTypes[m_varbinary]);

        [Benchmark]
        public static async void UniqueIdentifierAsync() => await RunBenchmarkAsync(s_datatypes.Others[o_uniqueidentifier]);

        [Benchmark]
        public static async void XmlAsync() => await RunBenchmarkAsync(s_datatypes.Others[o_xml]);
    }
}
