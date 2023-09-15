// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using static Microsoft.Data.SqlClient.PerformanceTests.Constants;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class DataTypeReaderRunner : BaseRunner
    {
        private static SqlConnection s_sqlConnection;
        private static long s_rowCount;

        private static string _query(string name) => $"SELECT * FROM {name}";

        [GlobalSetup]
        public static void Setup()
        {
            s_sqlConnection = new(s_config.ConnectionString);
            s_sqlConnection.Open();
            s_rowCount = s_config.Benchmarks.DataTypeReaderRunnerConfig.RowCount;
        }

        [GlobalCleanup]
        public static void Dispose()
        {
            s_sqlConnection.Close();
            SqlConnection.ClearAllPools();
        }

        private static void RunBenchmark(DataType type)
        {
            Table t = Table.Build(nameof(SqlCommandRunner))
                .AddColumn(new Column(type))
                .CreateTable(s_sqlConnection)
                .InsertBulkRows(s_rowCount, s_sqlConnection);
            try
            {
                using SqlCommand sqlCommand = new(_query(t.Name), s_sqlConnection);
                using SqlDataReader reader = sqlCommand.ExecuteReader();
                while (reader.Read())
                { }
            }
            finally
            {
                t.DropTable(s_sqlConnection);
            }
        }

        [Benchmark]
        public static void Bit() => RunBenchmark(s_datatypes.Numerics[n_bit]);

        [Benchmark]
        public static void Int() => RunBenchmark(s_datatypes.Numerics[n_int]);

        [Benchmark]
        public static void TinyInt() => RunBenchmark(s_datatypes.Numerics[n_tinyint]);

        [Benchmark]
        public static void SmallInt() => RunBenchmark(s_datatypes.Numerics[n_smallint]);

        [Benchmark]
        public static void BigInt() => RunBenchmark(s_datatypes.Numerics[n_bigint]);

        [Benchmark]
        public static void Money() => RunBenchmark(s_datatypes.Numerics[n_money]);

        [Benchmark]
        public static void SmallMoney() => RunBenchmark(s_datatypes.Numerics[n_smallmoney]);

        [Benchmark]
        public static void Decimal() => RunBenchmark(s_datatypes.Decimals[d_decimal]);

        [Benchmark]
        public static void Numeric() => RunBenchmark(s_datatypes.Decimals[d_numeric]);

        [Benchmark]
        public static void Float() => RunBenchmark(s_datatypes.Decimals[d_float]);

        [Benchmark]
        public static void Real() => RunBenchmark(s_datatypes.Decimals[d_real]);

        [Benchmark]
        public static void Date() => RunBenchmark(s_datatypes.DateTimes[t_date]);

        [Benchmark]
        public static void Datetime() => RunBenchmark(s_datatypes.DateTimes[t_datetime]);

        [Benchmark]
        public static void Datetime2() => RunBenchmark(s_datatypes.DateTimes[t_datetime2]);

        [Benchmark]
        public static void Time() => RunBenchmark(s_datatypes.DateTimes[t_time]);

        [Benchmark]
        public static void SmallDateTime() => RunBenchmark(s_datatypes.DateTimes[t_smalldatetime]);

        [Benchmark]
        public static void DateTimeOffset() => RunBenchmark(s_datatypes.DateTimes[t_datetimeoffset]);

        [Benchmark]
        public static void Char() => RunBenchmark(s_datatypes.Characters[c_char]);

        [Benchmark]
        public static void NChar() => RunBenchmark(s_datatypes.Characters[c_nchar]);

        [Benchmark]
        public static void Binary() => RunBenchmark(s_datatypes.Binary[b_binary]);

        [Benchmark]
        public static void VarChar() => RunBenchmark(s_datatypes.MaxTypes[m_varchar]);

        [Benchmark]
        public static void NVarChar() => RunBenchmark(s_datatypes.MaxTypes[m_nvarchar]);

        [Benchmark]
        public static void VarBinary() => RunBenchmark(s_datatypes.MaxTypes[m_varbinary]);

        [Benchmark]
        public static void UniqueIdentifier() => RunBenchmark(s_datatypes.Others[o_uniqueidentifier]);

        [Benchmark]
        public static void Xml() => RunBenchmark(s_datatypes.Others[o_xml]);
    }
}
