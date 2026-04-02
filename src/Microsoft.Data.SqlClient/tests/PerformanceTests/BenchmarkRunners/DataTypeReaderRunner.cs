// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using static Microsoft.Data.SqlClient.PerformanceTests.Constants;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class DataTypeReaderRunner : BaseRunner
    {
        private SqlConnection _sqlConnection;
        private long _rowCount;

        private string _query(string name) => $"SELECT * FROM {name}";

        [GlobalSetup]
        public void Setup()
        {
            _sqlConnection = new(s_config.ConnectionString);
            _sqlConnection.Open();
            _rowCount = s_config.Benchmarks.DataTypeReaderRunnerConfig.RowCount;
        }

        [GlobalCleanup]
        public void Dispose()
        {
            _sqlConnection.Close();
            SqlConnection.ClearAllPools();
        }

        private void RunBenchmark(DataType type)
        {
            Table t = Table.Build(nameof(SqlCommandRunner))
                .AddColumn(new Column(type))
                .CreateTable(_sqlConnection)
                .InsertBulkRows(_rowCount, _sqlConnection);
            try
            {
                using SqlCommand sqlCommand = new(_query(t.Name), _sqlConnection);
                using SqlDataReader reader = sqlCommand.ExecuteReader();
                while (reader.Read())
                { }
            }
            finally
            {
                t.DropTable(_sqlConnection);
            }
        }

        [Benchmark]
        public void Bit() => RunBenchmark(s_datatypes.Numerics[n_bit]);

        [Benchmark]
        public void Int() => RunBenchmark(s_datatypes.Numerics[n_int]);

        [Benchmark]
        public void TinyInt() => RunBenchmark(s_datatypes.Numerics[n_tinyint]);

        [Benchmark]
        public void SmallInt() => RunBenchmark(s_datatypes.Numerics[n_smallint]);

        [Benchmark]
        public void BigInt() => RunBenchmark(s_datatypes.Numerics[n_bigint]);

        [Benchmark]
        public void Money() => RunBenchmark(s_datatypes.Numerics[n_money]);

        [Benchmark]
        public void SmallMoney() => RunBenchmark(s_datatypes.Numerics[n_smallmoney]);

        [Benchmark]
        public void Decimal() => RunBenchmark(s_datatypes.Decimals[d_decimal]);

        [Benchmark]
        public void Numeric() => RunBenchmark(s_datatypes.Decimals[d_numeric]);

        [Benchmark]
        public void Float() => RunBenchmark(s_datatypes.Decimals[d_float]);

        [Benchmark]
        public void Real() => RunBenchmark(s_datatypes.Decimals[d_real]);

        [Benchmark]
        public void Date() => RunBenchmark(s_datatypes.DateTimes[t_date]);

        [Benchmark]
        public void Datetime() => RunBenchmark(s_datatypes.DateTimes[t_datetime]);

        [Benchmark]
        public void Datetime2() => RunBenchmark(s_datatypes.DateTimes[t_datetime2]);

        [Benchmark]
        public void Time() => RunBenchmark(s_datatypes.DateTimes[t_time]);

        [Benchmark]
        public void SmallDateTime() => RunBenchmark(s_datatypes.DateTimes[t_smalldatetime]);

        [Benchmark]
        public void DateTimeOffset() => RunBenchmark(s_datatypes.DateTimes[t_datetimeoffset]);

        [Benchmark]
        public void Char() => RunBenchmark(s_datatypes.Characters[c_char]);

        [Benchmark]
        public void NChar() => RunBenchmark(s_datatypes.Characters[c_nchar]);

        [Benchmark]
        public void Binary() => RunBenchmark(s_datatypes.Binary[b_binary]);

        [Benchmark]
        public void VarChar() => RunBenchmark(s_datatypes.MaxTypes[m_varchar]);

        [Benchmark]
        public void NVarChar() => RunBenchmark(s_datatypes.MaxTypes[m_nvarchar]);

        [Benchmark]
        public void VarBinary() => RunBenchmark(s_datatypes.MaxTypes[m_varbinary]);

        [Benchmark]
        public void UniqueIdentifier() => RunBenchmark(s_datatypes.Others[o_uniqueidentifier]);

        [Benchmark]
        public void Xml() => RunBenchmark(s_datatypes.Others[o_xml]);
    }
}
