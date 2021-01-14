using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlDateTime2Test
    {
        [Fact]
        public void ConvertFromDBNull()
        {
            var value = DBNull.Value;

            SqlDateTime2 sqlDateTime2 = (SqlDateTime2)value;
            Assert.Equal(SqlDateTime2.Null, sqlDateTime2);
        }

        [Fact]
        public void ConvertFromDateTime()
        {
            var value = new DateTime(2020, 12, 17, 14, 06, 10, 123, DateTimeKind.Utc);

            SqlDateTime2 expected = new SqlDateTime2(value.Ticks);
            SqlDateTime2 sqlDateTime2 = (SqlDateTime2)value;

            Assert.Equal(expected, sqlDateTime2);
        }

        [Fact]
        public void ConvertToDateTime()
        {
            var value = new DateTime(2020, 12, 17, 14, 06, 10, 123, DateTimeKind.Utc);

            SqlDateTime2 expected = new SqlDateTime2(value.Ticks);
            DateTime converted = (DateTime)expected;

            Assert.Equal(value, converted);
        }

        [Fact]
        public void TwoSameShouldBeEquals()
        {
            Assert.Equal(SqlDateTime2.Null, SqlDateTime2.Null);
        }

        [Fact]
        public void NullShouldBeLessThanSomething()
        {
            var result = SqlDateTime2.Null.CompareTo(new SqlDateTime2(1));
            Assert.Equal(-1, result);
        }

        [Fact]
        public void ZeroShouldBeLessThanOne()
        {
            var result = new SqlDateTime2(0).CompareTo(new SqlDateTime2(1));
            Assert.Equal(-1, result);
        }

        [Fact]
        public void OutOfLowRangeTicksTest()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlDateTime2(-1));
        }

        [Fact]
        public void OutOfHighRangeTicksTest()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlDateTime2(long.MaxValue));
        }
        [Fact]
        public void OutOfHighRangeTicksTest2()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlDateTime2(DateTime.MaxValue.Ticks+1));
        }
    }
}
