// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class NoBoxingValueTypes : IDisposable
    {
        private static readonly string _table = DataTestUtility.GetUniqueNameForSqlServer(nameof(NoBoxingValueTypes));
        private const int _count = 5000;
        private static readonly ItemToCopy _item;
        private static readonly IEnumerable<ItemToCopy> _items;
        private static readonly IDataReader _reader;

        private static readonly string _connString = DataTestUtility.TCPConnectionString;

        private class ItemToCopy
        {
            // keeping this data static so the performance of the benchmark is not varied by the data size & shape
            public int IntColumn { get; } = 123456;
            public bool BoolColumn { get; } = true;
        }

        static NoBoxingValueTypes()
        {
            _item = new ItemToCopy();

            _items = Enumerable.Range(0, _count).Select(x => _item).ToArray();

            _reader = new EnumerableDataReaderFactoryBuilder<ItemToCopy>(_table)
                .Add("IntColumn", i => i.IntColumn)
                .Add("BoolColumn", i => i.BoolColumn)
                .BuildFactory()
                .CreateReader(_items)
            ;
        }

        public NoBoxingValueTypes()
        {
            using (var conn = new SqlConnection(_connString))
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                Helpers.TryExecute(cmd, $@"
                    CREATE TABLE {_table} (
                        IntColumn INT NOT NULL,
                        BoolColumn BIT NOT NULL
                    )
                ");
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Should_Not_Box()
        { // in debug mode, the double boxing DOES occur, which causes this test to fail (does the JIT "do" less in debug?)
#if DEBUG
            return;
#endif
            // cannot figure out an easy way to get this to work on all platforms

            //var config = ManualConfig.Create(DefaultConfig.Instance)
            //    .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            //    .AddJob(Job.InProcess.WithLaunchCount(1).WithInvocationCount(1).WithIterationCount(1).WithWarmupCount(0).WithStrategy(RunStrategy.ColdStart))
            //    .AddDiagnoser(MemoryDiagnoser.Default)
            //;

            //var summary = BenchmarkRunner.Run<NoBoxingValueTypesBenchmark>(config);

            //var numValueTypeColumns = 2;
            //var totalBytesWhenBoxed = IntPtr.Size * _count * numValueTypeColumns;

            //var report = summary.Reports.First();

            //Assert.Equal(1, report.AllMeasurements.Count);
            //Assert.True(report.GcStats.BytesAllocatedPerOperation < totalBytesWhenBoxed);
        }

        public class NoBoxingValueTypesBenchmark
        {
            // [Benchmark]
            public void BulkCopy()
            {
                _reader.Close(); // this resets the reader

                using (var bc = new SqlBulkCopy(DataTestUtility.TCPConnectionString, SqlBulkCopyOptions.TableLock))
                {
                    bc.BatchSize = _count;
                    bc.DestinationTableName = _table;
                    bc.BulkCopyTimeout = 60;

                    bc.WriteToServer(_reader);
                }
            }
        }

        public void Dispose()
        {
            using (var conn = new SqlConnection(_connString))
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                Helpers.TryExecute(cmd, $@"
                    DROP TABLE IF EXISTS {_table}
                ");
            }
        }

        //all code here and below is a custom data reader implementation to support the benchmark
        private class EnumerableDataReaderFactoryBuilder<T>
        {
            private readonly List<LambdaExpression> _expressions = new List<LambdaExpression>();
            private readonly List<Func<T, object>> _objExpressions = new List<Func<T, object>>();
            private readonly DataTable _schemaTable;

            public EnumerableDataReaderFactoryBuilder(string tableName)
            {
                Name = tableName;
                _schemaTable = new DataTable();
            }

            private static readonly HashSet<Type> _validTypes = new[]
            {
                typeof(decimal),
                typeof(decimal?),
                typeof(string),
                typeof(int),
                typeof(int?),
                typeof(double),
                typeof(bool),
                typeof(bool?),
                typeof(Guid),
                typeof(DateTime),
            }.ToHashSet();

            public EnumerableDataReaderFactoryBuilder<T> Add<TColumn>(string column, Expression<Func<T, TColumn>> expression)
            {
                var t = typeof(TColumn);

                var func = expression.Compile();

                // don't do any optimizations for boxing bools here to detect boxing occurring properly.
                Expression<Func<T, object>> objExpression= o => func(o);

                _objExpressions.Add(objExpression.Compile());

                if (_validTypes.Contains(t))
                {
                    t = Nullable.GetUnderlyingType(t) ?? t; // data table doesn't accept nullable.
                    _schemaTable.Columns.Add(column, t);
                    _expressions.Add(expression);
                }
                else
                {
                    Console.WriteLine($"Could not matching return type for {Name}.{column} of: {t.Name}");
                    _schemaTable.Columns.Add(column); //add w/o type to force using GetValue

                    _expressions.Add(objExpression);
                }

                return this;
            }

            public EnumerableDataReaderFactory<T> BuildFactory() => new EnumerableDataReaderFactory<T>(_schemaTable, _expressions, _objExpressions);

            public string Name { get; }
        }

        public class EnumerableDataReaderFactory<T>
        {
            public DataTable SchemaTable { get; }
            public Func<T, object>[] ObjectGetters { get; }
            public Func<T, decimal>[] DecimalGetters { get; }
            public Func<T, decimal?>[] NullableDecimalGetters { get; }
            public Func<T, string>[] StringGetters { get; }
            public Func<T, double>[] DoubleGetters { get; }
            public Func<T, int>[] IntGetters { get; }
            public Func<T, int?>[] NullableIntGetters { get; }
            public Func<T, bool>[] BoolGetters { get; }

            public Func<T, bool?>[] NullableBoolGetters { get; }

            public Func<T, Guid>[] GuidGetters { get; }
            public Func<T, DateTime>[] DateTimeGetters { get; }
            public bool[] NullableIndexes { get; }

            public EnumerableDataReaderFactory(DataTable schemaTable, List<LambdaExpression> expressions, List<Func<T, object>> objectGetters)
            {
                SchemaTable = schemaTable;
                DecimalGetters = new Func<T, decimal>[expressions.Count];
                NullableDecimalGetters = new Func<T, decimal?>[expressions.Count];
                StringGetters = new Func<T, string>[expressions.Count];
                DoubleGetters = new Func<T, double>[expressions.Count];
                IntGetters = new Func<T, int>[expressions.Count];
                NullableIntGetters = new Func<T, int?>[expressions.Count];
                BoolGetters = new Func<T, bool>[expressions.Count];
                NullableBoolGetters = new Func<T, bool?>[expressions.Count];
                GuidGetters = new Func<T, Guid>[expressions.Count];
                DateTimeGetters = new Func<T, DateTime>[expressions.Count];
                NullableIndexes = new bool[expressions.Count];

                ObjectGetters = objectGetters.ToArray();

                for (int i = 0; i < expressions.Count; i++)
                {
                    var expression = expressions[i];

                    NullableIndexes[i] = !expression.ReturnType.IsValueType || Nullable.GetUnderlyingType(expression.ReturnType) != null;

                    switch (expression)
                    {
                        case Expression<Func<T, object>> e:
                            break; // do nothing
                        case Expression<Func<T, decimal>> e:
                            DecimalGetters[i] = e.Compile();
                            break;
                        case Expression<Func<T, decimal?>> e:
                            NullableDecimalGetters[i] = e.Compile();
                            break;
                        case Expression<Func<T, string>> e:
                            StringGetters[i] = e.Compile();
                            break;
                        case Expression<Func<T, double>> e:
                            DoubleGetters[i] = e.Compile();
                            break;
                        case Expression<Func<T, int>> e:
                            IntGetters[i] = e.Compile();
                            break;
                        case Expression<Func<T, int?>> e:
                            NullableIntGetters[i] = e.Compile();
                            break;
                        case Expression<Func<T, bool>> e:
                            BoolGetters[i] = e.Compile();
                            break;
                        case Expression<Func<T, bool?>> e:
                            NullableBoolGetters[i] = e.Compile();
                            break;
                        case Expression<Func<T, Guid>> e:
                            GuidGetters[i] = e.Compile();
                            break;
                        case Expression<Func<T, DateTime>> e:
                            DateTimeGetters[i] = e.Compile();
                            break;
                        default:
                            throw new Exception($"Type missing: {expression.GetType().FullName}");
                    }
                }
            }

            public IDataReader CreateReader(IEnumerable<T> items) => new EnumerableDataReader<T>(this, items.GetEnumerator());
        }

        public class EnumerableDataReader<T> : IDataReader
        {
            private readonly IEnumerator<T> _source;
            private readonly EnumerableDataReaderFactory<T> _context;

            public EnumerableDataReader(EnumerableDataReaderFactory<T> context, IEnumerator<T> source)
            {
                _source = source;
                _context = context;
            }

            public object GetValue(int i)
            {
                var v = _context.ObjectGetters[i](_source.Current);
                return v;
            }

            public int FieldCount => _context.ObjectGetters.Length;

            public bool Read() => _source.MoveNext();

            public void Close() => _source.Reset();

            public void Dispose() => this.Close();

            public bool NextResult() => throw new NotImplementedException();

            public int Depth => 0;

            public bool IsClosed => false;

            public int RecordsAffected => -1;

            public DataTable GetSchemaTable() => _context.SchemaTable;

            public object this[string name] => throw new NotImplementedException();

            public object this[int i] => GetValue(i);

            public bool GetBoolean(int i)
            {
                var g = _context.BoolGetters[i];

                if (g != null)
                    return g(_source.Current);

                return _context.NullableBoolGetters[i](_source.Current).Value;
            }

            public byte GetByte(int i) => throw new NotImplementedException();

            public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => throw new NotImplementedException();

            public char GetChar(int i) => throw new NotImplementedException();
            public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => -1;

            public IDataReader GetData(int i) => throw new NotImplementedException();

            public string GetDataTypeName(int i) => throw new NotImplementedException();

            public DateTime GetDateTime(int i) => _context.DateTimeGetters[i](_source.Current);

            public decimal GetDecimal(int i)
            {
                var g = _context.DecimalGetters[i];

                if (g != null)
                    return g(_source.Current);

                return _context.NullableDecimalGetters[i](_source.Current).Value;
            }

            public double GetDouble(int i) => _context.DoubleGetters[i](_source.Current);

            public Type GetFieldType(int i) => _context.SchemaTable.Columns[i].DataType;

            public float GetFloat(int i) => throw new NotImplementedException();

            public Guid GetGuid(int i) => _context.GuidGetters[i](_source.Current);

            public short GetInt16(int i) => throw new NotImplementedException();

            public int GetInt32(int i)
            {
                var g = _context.IntGetters[i];

                if (g != null)
                    return g(_source.Current);

                return _context.NullableIntGetters[i](_source.Current).Value;
            }

            public long GetInt64(int i) => throw new NotImplementedException();

            public string GetName(int i)
            {
                if (_context.SchemaTable.Columns.Count > i)
                {
                    return _context.SchemaTable.Columns[i].ColumnName;
                }
                throw new IndexOutOfRangeException($"No column for index {i}");
            }

            public int GetOrdinal(string name)
            {
                if (_context.SchemaTable.Columns.Count == 0)
                {
                    throw new Exception("Schema table is empty");
                }
                return _context.SchemaTable.Columns.IndexOf(name);
            }

            public string GetString(int i) => _context.StringGetters[i](_source.Current);

            public int GetValues(object[] values) => throw new NotImplementedException();

            public bool IsDBNull(int i)
            {
                // short circuit for non-nullable types
                if (!_context.NullableIndexes[i])
                {
                    return false;
                }

                // otherwise find the first one -- starting w/ most occurring to least

                var ig = _context.NullableIntGetters[i];
                if (ig != null)
                {
                    return ig(_source.Current) == null;
                }

                var sg = _context.StringGetters[i];
                if (sg != null)
                {
                    return sg(_source.Current) == null;
                }

                var bg = _context.NullableBoolGetters[i];
                if (bg != null)
                {
                    return bg(_source.Current) == null;
                }

                var dg = _context.NullableDecimalGetters[i];
                if (dg != null)
                {
                    return dg(_source.Current) == null;
                }

                return false;
            }
        }
    }
}
