using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class ValueTypeConverterTests
    {
        [Fact]
        public void ObjectConversionSuccessful()
        {
            object test = 0m;

            decimal converted = ValueTypeConverter.Convert<object, decimal>(test);

            Assert.Equal(test, converted);
        }

        [Fact]
        public void SelfConversionSuccessful()
        {
            decimal test = 0m;

            decimal converted = ValueTypeConverter.Convert<decimal, decimal>(test);

            Assert.Equal(test, converted);
        }

        [Fact]
        public void AssignableConversionSuccessful()
        {
            SqlDecimal test = 0m;

            decimal converted = ValueTypeConverter.Convert<SqlDecimal, decimal>(test);

            Assert.Equal(test, converted);
        }
    }
}
