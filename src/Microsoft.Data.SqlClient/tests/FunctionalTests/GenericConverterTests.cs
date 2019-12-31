using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class GenericConverterTests
    {
        [Fact]
        public void ObjectConversionSuccessful()
        {
            object test = 0m;

            decimal converted = GenericConverter.Convert<object, decimal>(test);

            Assert.Equal(test, converted);
        }

        [Fact]
        public void SelfConversionSuccessful()
        {
            decimal test = 0m;

            decimal converted = GenericConverter.Convert<decimal, decimal>(test);

            Assert.Equal(test, converted);
        }

        [Fact]
        public void AssignableConversionSuccessful()
        {
            SqlDecimal test = 0m;

            decimal converted = GenericConverter.Convert<SqlDecimal, decimal>(test);

            Assert.Equal(test, converted);
        }
    }
}
