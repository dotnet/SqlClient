// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
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
        // commenting out until i receive feedback for how to implement tests w/o linking file
        //[Theory]
        //[ClassData(typeof(DecimalTheoryData))]
        //public void ObjectToDecimal_ConversionSuccessful(decimal val)
        //{
        //    object objVal = val;
        //    decimal converted = GenericConverter.Convert<object, decimal>(objVal);
        //    Assert.Equal(val, converted);
        //}

        //[Fact]
        //public void ObjectToString_ConversionSuccessful()
        //{
        //    string testVal = "abcd1234";
        //    string converted = GenericConverter.Convert<object, string>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Fact]
        //public void ObjectToInt_ConversionSuccessful()
        //{
        //    int testVal = 123;
        //    int converted = GenericConverter.Convert<object, int>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Fact]
        //public void ObjectToDouble_ConversionSuccessful()
        //{
        //    double testVal = 123d;
        //    double converted = GenericConverter.Convert<object, double>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Fact]
        //public void ObjectToFloat_ConversionSuccessful()
        //{
        //    float testVal = 123f;
        //    float converted = GenericConverter.Convert<object, float>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Fact]
        //public void ObjectToGuid_ConversionSuccessful()
        //{
        //    Guid testVal = Guid.NewGuid();
        //    Guid converted = GenericConverter.Convert<object, Guid>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Fact]
        //public void ObjectToDateTime_ConversionSuccessful()
        //{
        //    DateTime testVal = DateTime.Now;
        //    DateTime converted = GenericConverter.Convert<object, DateTime>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Fact]
        //public void ObjectToBool_ConversionSuccessful()
        //{
        //    bool testVal = false;
        //    bool converted = GenericConverter.Convert<object, bool>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Fact]
        //public void ObjectToShort_ConversionSuccessful()
        //{
        //    short testVal = 123;
        //    short converted = GenericConverter.Convert<object, short>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Fact]
        //public void ObjectToByte_ConversionSuccessful()
        //{
        //    byte testVal = new byte();
        //    byte converted = GenericConverter.Convert<object, byte>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Fact]
        //public void ObjectToChar_ConversionSuccessful()
        //{
        //    char testVal = 'a';
        //    char converted = GenericConverter.Convert<object, char>(testVal);
        //    Assert.Equal(testVal, converted);
        //}

        //[Theory]
        //[ClassData(typeof(DecimalTheoryData))]
        //public void SelfConversionSuccessful(decimal val)
        //{
        //    decimal converted = GenericConverter.Convert<decimal, decimal>(val);
        //    Assert.Equal(val, converted);
        //}

        //[Theory]
        //[ClassData(typeof(DecimalTheoryData))]
        //public void AssignableConversionSuccessful(decimal val)
        //{
        //    SqlDecimal sqlVal = new SqlDecimal(val);
        //    decimal converted = GenericConverter.Convert<SqlDecimal, decimal>(sqlVal);
        //    Assert.Equal(val, converted);
        //}
    }
}
