// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Data.SqlClient
{
    public class SqlErrorCollectionTests
    {
        private const int ErrorsInTestCollection = 3;
        private static readonly Exception ReusableException = new();

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
                var chain = collection.Add(error);
                Assert.Same(collection, chain);
            }

            // Assert
            Assert.Equal(itemsToAdd, collection.Count);
        }

        [Theory]
        [InlineData(ErrorsInTestCollection, 0)]      // Destination just right size
        [InlineData(ErrorsInTestCollection + 10, 0)] // Null elements at end
        [InlineData(ErrorsInTestCollection + 2, 2)]  // Null elements at beginning
        [InlineData(ErrorsInTestCollection + 10, 1)] // Null elements at beginning and end
        public void CopyTo_SqlErrorArray_WithinRange(int destinationSize, int offset)
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] errors) = GetTestErrorCollection();
            SqlError[] copyDestination = new SqlError[destinationSize];

            // Act
            // - Uses SqlErrorCollection.CopyTo
            collection.CopyTo(copyDestination, offset);

            // Assert
            AssertCopiedCollection(errors, copyDestination, offset);
        }

        [Theory]
        [InlineData(ErrorsInTestCollection, -1)]    // Offset is negative
        [InlineData(ErrorsInTestCollection - 1, 0)] // Destination is too small
        [InlineData(ErrorsInTestCollection, 1)]     // Destination is big enough, but offset pushes it over edge
        public void CopyTo_SqlErrorArray_OutOfRange(int destinationSize, int offset)
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] _) = GetTestErrorCollection();
            SqlError[] copyDestination = new SqlError[destinationSize];

            // Act
            // - Uses SqlErrorCollection.CopyTo
            Action action = () => collection.CopyTo(copyDestination, offset);

            // Assert
            Assert.ThrowsAny<ArgumentException>(action);
        }

        [Theory]
        [InlineData(ErrorsInTestCollection, 0)]      // Destination just right size
        [InlineData(ErrorsInTestCollection + 10, 0)] // Null elements at end
        [InlineData(ErrorsInTestCollection + 2, 2)]  // Null elements at beginning
        [InlineData(ErrorsInTestCollection + 10, 1)] // Null elements at beginning and end
        public void CopyTo_ObjectArray_WithinRange(int destinationSize, int offset)
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] errors) = GetTestErrorCollection();
            object[] copyDestination = new object[destinationSize];

            // Act
            // - Uses ICollection.CopyTo
            collection.CopyTo(copyDestination, offset);

            // Assert
            AssertCopiedCollection(errors, copyDestination, offset);
        }

        [Theory]
        [InlineData(ErrorsInTestCollection, -1)]    // Offset is negative
        [InlineData(ErrorsInTestCollection - 1, 0)] // Destination is too small
        [InlineData(ErrorsInTestCollection, 1)]     // Destination is big enough, but offset pushes it over edge
        public void CopyTo_ObjectArray_OutOfRange(int destinationSize, int offset)
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] _) = GetTestErrorCollection();
            object[] copyDestination = new object[destinationSize];

            // Act
            // - Uses ICollection.CopyTo
            Action action = () => collection.CopyTo(copyDestination, offset);

            // Assert
            Assert.ThrowsAny<ArgumentException>(action);
        }

        [Fact]
        public void CopyTo_ObjectArray_WrongType()
        {
            // Arrange
            (SqlErrorCollection collection, SqlError[] errors) = GetTestErrorCollection();
            int[] destination = new int[errors.Length];

            // Act
            Action action = () => collection.CopyTo(destination, 0);

            // Assert
            Assert.Throws<InvalidCastException>(action);
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

        private static void AssertCopiedCollection(SqlError[] source, IReadOnlyList<object> destination, int offset)
        {
            for (int i = 0; i < destination.Count; i++)
            {
                if (i < offset)
                {
                    // - Elements before the offset should be null
                    Assert.Null(destination[i]);
                }
                else if (i >= offset && i < source.Length + offset)
                {
                    // - Elements after the offset but within the range of original elements should match
                    Assert.Same(source[i - offset], destination[i]);
                }
                else
                {
                    // - Elements after the offset and original elements should be null
                    Assert.Null(destination[i]);
                }
            }
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
                exception: ReusableException,
                batchIndex: 345);
        }

        private static (SqlErrorCollection collection, SqlError[] errors) GetTestErrorCollection()
        {
            SqlErrorCollection collection = new();
            SqlError[] errors = new SqlError[ErrorsInTestCollection];

            for (int i = 0; i < ErrorsInTestCollection; i++)
            {
                SqlError error = GetTestError();
                errors[i] = error;
                collection.Add(error);
            }

            return (collection, errors);
        }
    }
}
