using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class DecimalTheoryData: TheoryData<decimal>
    {
        public DecimalTheoryData()
        {
            Add(-100);
            Add(0);
            Add(.75m);
            Add(525);
        }
    }
    public class GenericConverterTests
    {
        [Theory]
        [ClassData(typeof(DecimalTheoryData))]
        public void ObjectToDecimal_ConversionSuccessful(decimal val)
        {
            object objVal = val;
            decimal converted = GenericConverter.Convert<object, decimal>(objVal);
            Assert.Equal(val, converted);
        }

        [Fact]
        public void ObjectToString_ConversionSuccessful()
        {
            string testVal = "abcd1234";
            string converted = GenericConverter.Convert<object, string>(testVal);
            Assert.Equal(testVal, converted);
        }

        [Theory]
        [ClassData(typeof(DecimalTheoryData))]
        public void SelfConversionSuccessful(decimal val)
        {
            decimal converted = GenericConverter.Convert<decimal, decimal>(val);
            Assert.Equal(val, converted);
        }

        [Theory]
        [ClassData(typeof(DecimalTheoryData))]
        public void AssignableConversionSuccessful(decimal val)
        {
            SqlDecimal sqlVal = new SqlDecimal(val);
            decimal converted = GenericConverter.Convert<SqlDecimal, decimal>(sqlVal);
            Assert.Equal(val, converted);
        }
    }
}
