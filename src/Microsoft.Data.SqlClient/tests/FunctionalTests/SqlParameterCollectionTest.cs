// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlParameterCollectionTest
    {
        [Fact]
        public void CollectionAddInvalidRange_Throws()
        {
            SqlCommand command = new SqlCommand();
            SqlParameterCollection collection = command.Parameters;

            Array invalid = null;
            Assert.Throws<ArgumentNullException>(() => collection.AddRange(invalid));
        }

        [Fact]
        public void CollectionAddRange()
        {
            SqlCommand command = new SqlCommand();
            SqlParameterCollection collection = command.Parameters;
            Array sqlParameters = new SqlParameter[] { new("Test1", 1), new("Test2", 2) };

            collection.AddRange(sqlParameters);

            Assert.Equal(2, collection.Count);
            Assert.Equal((SqlParameter)sqlParameters.GetValue(0), collection[0]);
            Assert.Equal((SqlParameter)sqlParameters.GetValue(1), collection[1]);
        }

        [Fact]
        public void CollectionCheckNameInvalid_Throws()
        {
            SqlCommand command = new SqlCommand();
            SqlParameterCollection collection = command.Parameters;
            collection.Add(new SqlParameter("Test1", 1));
            collection.Add(new SqlParameter("Test2", 2));

            IndexOutOfRangeException ex = Assert.Throws<IndexOutOfRangeException>(() => collection.RemoveAt("DoesNotExist"));
            Assert.Contains("DoesNotExist", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConnectionCopyTo()
        {
            SqlCommand command = new SqlCommand();
            SqlParameterCollection collection = command.Parameters;
            collection.Add(new SqlParameter("Test1", 1));
            collection.Add(new SqlParameter("Test2", 2));

            SqlParameter[] copied = new SqlParameter[2];
            collection.CopyTo(copied, 0);

            Assert.Equal(collection[0], copied[0]);
            Assert.Equal(collection[1], copied[1]);
        }

        [Fact]
        public void CollectionIndexOfCaseInsensitive()
        {
            SqlCommand command = new SqlCommand();
            SqlParameterCollection collection = command.Parameters;
            collection.Add(new SqlParameter("TEST1", 1));
            collection.Add(new SqlParameter("Test2", 2));
            collection.Add(new SqlParameter("Test3", 3));

            int index = collection.IndexOf("test1");
            Assert.Equal(0, index);
        }

        [Fact]
        public void CollectionRemove()
        {
            SqlCommand command = new SqlCommand();
            SqlParameterCollection collection = command.Parameters;
            SqlParameter parameter1 = new SqlParameter("Test1", 1);
            collection.Add(parameter1);
            collection.Add(new SqlParameter("Test2", 2));
            collection.Add(new SqlParameter("Test3", 3));

            collection.Remove(parameter1);
            Assert.Equal(2, collection.Count);
            Assert.Equal("Test2", collection[0].ParameterName);
        }

        [Fact]
        public void CollectionSetParameter()
        {
            SqlCommand command = new SqlCommand();
            SqlParameterCollection collection = command.Parameters;
            collection.Add(new SqlParameter("TestOne", 0));
            collection.Add(new SqlParameter("Test2", 2));
            collection.Add(new SqlParameter("Test3", 3));

            collection[0] = new SqlParameter("Test1", 1);
            Assert.Equal("Test1", collection[0].ParameterName);
            Assert.Equal(1, (int)collection[0].Value);
        }

        [Fact]
        public void CollectionValiateNull_Throws()
        {
            SqlCommand command = new SqlCommand();
            SqlParameterCollection collection = command.Parameters;

            Assert.Throws<ArgumentNullException>(() => collection.Add(null));
        }
    }
}
