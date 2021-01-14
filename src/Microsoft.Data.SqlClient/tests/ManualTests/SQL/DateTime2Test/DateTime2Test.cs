// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.DateTime2Test
{
    public class DateTime2Test
    {
        private static readonly DateTime TestDate = new DateTime(2020, 12, 17, 18, 33, 12, 123).AddTicks(4567);

        private const string SqlDateTimeFormat = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fff";
        private const string SqlDateTime2Format = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fffffff";

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void NullDefinedAsDateTime2()
        {
            var value = GetValueFromReader(@"SELECT CAST(NULL AS DATETIME2)", reader => reader.GetSqlDateTime2(0));
            Assert.True(value.IsNull);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void DateTimeAsNullDateTime2()
        {
            var value = GetValueFromReader(@"SELECT CAST(NULL AS DATETIME)", reader => reader.GetSqlDateTime2(0));
            Assert.True(value.IsNull);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void DateAsNullDateTime2()
        {
            var value = GetValueFromReader(@"SELECT CAST(NULL AS DATE)", reader => reader.GetSqlDateTime2(0));
            Assert.True(value.IsNull);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void DateTimeAsDateTime2()
        {
            var value = GetValueFromReader($"SELECT CAST(CAST('{TestDate.ToString(SqlDateTime2Format)}' AS DATETIME2) as DATETIME)", reader => reader.GetSqlDateTime2(0));
            Assert.False(value.IsNull);
            //DATETIME has reduced precision, so we only get the first 3 digits of time
            var expected = new DateTime(TestDate.Year, TestDate.Month, TestDate.Day, TestDate.Hour, TestDate.Minute, TestDate.Second, 123);
            Assert.Equal(expected, value.Value);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void DateAsDateTime2()
        {
            var value = GetValueFromReader($"SELECT CAST(CAST('{TestDate.ToString(SqlDateTimeFormat)}' AS DATETIME) as DATE)", reader => reader.GetSqlDateTime2(0));
            Assert.False(value.IsNull);
            Assert.Equal(TestDate.Date, value.Value.Date);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void DateTime2AsDateTime2DecreasedPrecision()
        {
            var value = GetValueFromReader($"SELECT CAST('{TestDate.ToString(SqlDateTime2Format)}' AS DATETIME2(3))", reader => reader.GetSqlDateTime2(0));
            Assert.False(value.IsNull);
            var expected = new DateTime(TestDate.Year, TestDate.Month, TestDate.Day, TestDate.Hour, TestDate.Minute, TestDate.Second, 123);
            Assert.Equal(expected, value.Value);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void DateTime2AsDateTime2FullPrecision()
        {
            var value = GetValueFromReader($"SELECT CAST('{TestDate.ToString(SqlDateTime2Format)}' AS DATETIME2(7))", reader => reader.GetSqlDateTime2(0));
            Assert.False(value.IsNull);
            
            Assert.Equal(TestDate, value.Value);
        }

        private static T GetValueFromReader<T>(string sql, Func<SqlDataReader, T> extractor)
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.CommandType = System.Data.CommandType.Text;

                    using (var reader = command.ExecuteReader())
                    {
                        reader.Read();
                        return extractor(reader);
                    }
                }
            }
        }
    }
}
