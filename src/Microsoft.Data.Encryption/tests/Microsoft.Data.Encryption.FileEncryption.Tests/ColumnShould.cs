using Microsoft.Data.Encryption.FileEncryption;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.FileEncryption.Tests
{
    public class ColumnShould
    {
        [Fact]
        public void ReturnTheAproperiateDataType()
        {
            Column<int> intColumn = new Column<int>(new List<int>() { 1 });
            Assert.Equal(typeof(int), intColumn.DataType);

            Column<string> stringColumn = new Column<string>(new List<string>() { "string" });
            Assert.Equal(typeof(string), stringColumn.DataType);

            Column<double> doubleColumn = new Column<double>(new List<double>() { Math.PI });
            Assert.Equal(typeof(double), doubleColumn.DataType);

            Column<Guid> guidColumn = new Column<Guid>(new List<Guid>() { Guid.NewGuid() });
            Assert.Equal(typeof(Guid), guidColumn.DataType);
        }

        [Fact]
        public void ReturnConstructorSuppliedDataWhenCallingDataProperty()
        {
            List<int> expectedIntData = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
            Column<int> intColumn = new Column<int>(expectedIntData);
            Assert.Equal(expectedIntData, intColumn.Data);

            List<string> expectedStringData = new List<string>() { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
            Column<string> stringColumn = new Column<string>(expectedStringData);
            Assert.Equal(expectedStringData, stringColumn.Data);

            List<double> expectedDoubleData = new List<double>() { Math.PI, 0.00001, 5.5, 0, 1, 100, 55555.999999999 };
            Column<double> doubleColumn = new Column<double>(expectedDoubleData);
            Assert.Equal(expectedDoubleData, doubleColumn.Data);

            List<Guid> expectedGuidData = Enumerable.Repeat(new Guid(), 10).ToList();
            Column<Guid> guidColumn = new Column<Guid>(expectedGuidData);
            Assert.Equal(expectedGuidData, guidColumn.Data);
        }
    }
}
