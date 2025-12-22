// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    public class SqlErrorCollectionTests
    {
        [Fact]
        public void Constructor_PropertiesInitialized()
        {
            // Act
            SqlErrorCollection collection = new();
            
            // Assert
            Assert.Empty(collection);
            
            // - ICollection properties
            ICollection collection2 = collection;
            Assert.Same(collection2, collection2.SyncRoot);
            Assert.False(collection2.IsSynchronized);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(10)]
        public void Add(int itemsToAdd)
        {
            // Arrange
            SqlErrorCollection collection = new();
            SqlError error = GetTestError();

            // Act
            for (int i = 0; i < itemsToAdd; i++)
            {
                collection.Add(error);
            }
            
            // Assert
            Assert.Equal(itemsToAdd, collection.Count);
        }
        
        [Fact]
        public void CopyTo_ObjectArray_ZeroIndex()
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] errors) = GetTestErrorCollection();
            object[] copyDestination = new object[errors.Length];

            // Act
            collection.CopyTo(copyDestination, index: 0);
            
            // Assert
            for (int i = 0; i < errors.Length; i++)
            {
                Assert.Same(errors[i], copyDestination[i]);
            }
        }
        
        [Fact]
        public void CopyTo_ObjectArray_NonZeroIndex()
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] errors) = GetTestErrorCollection();
            object[] copyDestination = new object[errors.Length + 1];

            // Act
            collection.CopyTo(copyDestination, index: 1);
            
            // Assert
            Assert.Null(copyDestination[0]);
            for (int i = 0; i < errors.Length; i++)
            {
                Assert.Same(errors[i], copyDestination[i + 1]);
            }
        }

        [Fact]
        public void CopyTo_ObjectArray_WrongType()
        {
            // Arrange
            (SqlErrorCollection collection, _) = GetTestErrorCollection();
            int[] copyDestination = new int[1];

            // Act
            Action action = () => collection.CopyTo(copyDestination, index: 0);

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void CopyTo_TypedArray_ZeroIndex()
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] errors) = GetTestErrorCollection();
            SqlError[] copyDestination = new SqlError[errors.Length + 1];

            // Act
            collection.CopyTo(copyDestination, index: 0);

            // Assert
            for (int i = 0; i < errors.Length; i++)
            {
                Assert.Same(errors[i], copyDestination[i]);
            }
        }

        [Fact]
        public void CopyTo_TypedArray_NonZeroIndex()
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] errors) = GetTestErrorCollection();
            object[] copyDestination = new object[errors.Length + 1];

            // Act
            collection.CopyTo(copyDestination, index: 1);

            // Assert
            Assert.Null(copyDestination[0]);
            for (int i = 0; i < errors.Length; i++)
            {
                Assert.Same(errors[i], copyDestination[i + 1]);
            }
        }

        [Fact]
        public void GetEnumerator()
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] errors) = GetTestErrorCollection();
            List<SqlError> output = new();

            // Act
            foreach (SqlError error in collection)
            {
                output.Add(error);
            }

            // Assert
            for (int i = 0; i < errors.Length; i++)
            {
                Assert.Same(errors[i], output[i]);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Indexer_InRange(int index)
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] errors) = GetTestErrorCollection();

            // Act
            SqlError result = collection[index];

            // Assert
            Assert.Same(errors[index], result);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(123)]
        public void Indexer_OutOfRange(int index)
        {
            // Arrange
            (SqlErrorCollection collection, _) = GetTestErrorCollection();

            // Act
            Action action = () => _ = collection[index];

            // Assert
            Assert.Throws<ArgumentOutOfRangeException>(action);
        }

        private static SqlError GetTestError()
        {
            return new SqlError(
                infoNumber: 123,
                errorState: 0x02,
                errorClass: 0x03,
                server: "foo",
                errorMessage: "bar",
                procedure: "baz",
                lineNumber: 234,
                exception: new Exception(),
                batchIndex: 345);
        }

        private static (SqlErrorCollection collection, SqlError[] errors) GetTestErrorCollection()
        {
            SqlErrorCollection collection = new();
            SqlError[] errors = new SqlError[3];

            SqlError error1 = GetTestError();
            collection.Add(error1);
            errors[0] = error1;

            SqlError error2 = GetTestError();
            collection.Add(error2);
            errors[1] = error2;

            SqlError error3 = GetTestError();
            collection.Add(error3);
            errors[2] = error3;

            return (collection, errors);
        }
    }
}
