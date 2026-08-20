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
        public static Task BitAsync() => RunBenchmarkAsync(s_datatypes.Numerics[n_bit]);

        [Benchmark]
        public static Task IntAsync() => RunBenchmarkAsync(s_datatypes.Numerics[n_int]);

        [Benchmark]
        public static Task TinyIntAsync() => RunBenchmarkAsync(s_datatypes.Numerics[n_tinyint]);

        [Benchmark]
        public static Task SmallIntAsync() => RunBenchmarkAsync(s_datatypes.Numerics[n_smallint]);

        [Benchmark]
        public static Task BigIntAsync() => RunBenchmarkAsync(s_datatypes.Numerics[n_bigint]);

        [Benchmark]
        public static Task MoneyAsync() => RunBenchmarkAsync(s_datatypes.Numerics[n_money]);

        [Benchmark]
        public static Task SmallMoneyAsync() => RunBenchmarkAsync(s_datatypes.Numerics[n_smallmoney]);

        [Benchmark]
        public static Task DecimalAsync() => RunBenchmarkAsync(s_datatypes.Decimals[d_decimal]);

        [Benchmark]
        public static Task NumericAsync() => RunBenchmarkAsync(s_datatypes.Decimals[d_numeric]);

        [Benchmark]
        public static Task FloatAsync() => RunBenchmarkAsync(s_datatypes.Decimals[d_float]);

        [Benchmark]
        public static Task RealAsync() => RunBenchmarkAsync(s_datatypes.Decimals[d_real]);

        [Benchmark]
        public static Task DateAsync() => RunBenchmarkAsync(s_datatypes.DateTimes[t_date]);

        [Benchmark]
        public static Task DatetimeAsync() => RunBenchmarkAsync(s_datatypes.DateTimes[t_datetime]);

        [Benchmark]
        public static Task Datetime2Async() => RunBenchmarkAsync(s_datatypes.DateTimes[t_datetime2]);

        [Benchmark]
        public static Task TimeAsync() => RunBenchmarkAsync(s_datatypes.DateTimes[t_time]);

        [Benchmark]
        public static Task SmallDateTimeAsync() => RunBenchmarkAsync(s_datatypes.DateTimes[t_smalldatetime]);

        [Benchmark]
        public static Task DateTimeOffsetAsync() => RunBenchmarkAsync(s_datatypes.DateTimes[t_datetimeoffset]);

        [Benchmark]
        public static Task CharAsync() => RunBenchmarkAsync(s_datatypes.Characters[c_char]);

        [Benchmark]
        public static Task NCharAsync() => RunBenchmarkAsync(s_datatypes.Characters[c_nchar]);

        [Benchmark]
        public static Task BinaryAsync() => RunBenchmarkAsync(s_datatypes.Binary[b_binary]);

        [Benchmark]
        public static Task VarCharAsync() => RunBenchmarkAsync(s_datatypes.MaxTypes[m_varchar]);

        [Benchmark]
        public static Task NVarCharAsync() => RunBenchmarkAsync(s_datatypes.MaxTypes[m_nvarchar]);

        [Benchmark]
        public static Task VarBinaryAsync() => RunBenchmarkAsync(s_datatypes.MaxTypes[m_varbinary]);

        [Benchmark]
        public static Task UniqueIdentifierAsync() => RunBenchmarkAsync(s_datatypes.Others[o_uniqueidentifier]);

        [Benchmark]
        public static Task XmlAsync() => RunBenchmarkAsync(s_datatypes.Others[o_xml]);
    }
}
