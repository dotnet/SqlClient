// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
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
        public void CopyTo_ObjectArrayZeroIndex()
        {
            // Arrange
            SqlErrorCollection collection = new();
            
            SqlError error1 = GetTestError();
            collection.Add(error1);
            
            SqlError error2 = GetTestError();
            collection.Add(error2);
            
            SqlError error3 = GetTestError();
            collection.Add(error3);

            object[] copyDestination = new object[3];

            // Act
            collection.CopyTo(copyDestination, index: 0);
            
            // Assert
            Assert.Same(error1, copyDestination[0]);
            Assert.Same(error2, copyDestination[1]);
            Assert.Same(error3, copyDestination[2]);
        }
        
        [Fact]
        public void CopyTo_ObjectArrayNonZeroIndex()
        {
            // Arrange
            SqlErrorCollection collection = new();
            
            SqlError error1 = GetTestError();
            collection.Add(error1);
            
            SqlError error2 = GetTestError();
            collection.Add(error2);
            
            SqlError error3 = GetTestError();
            collection.Add(error3);

            object[] copyDestination = new object[3];

            // Act
            collection.CopyTo(copyDestination, index: 1);
            
            // Assert
            Assert.Same(error2, copyDestination[0]);
            Assert.Same(error3, copyDestination[1]);
            Assert.Null(error3);
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
    }
}
