// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.Serialization;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    public class SqlErrorTests
    {
        [Fact]
        public void SerializationRoundTrip()
        {
            // Arrange
            DataContractSerializer serializer = new(typeof(SqlError));
            using MemoryStream stream = new();
            
            // - Create the test error
            SqlError originalError = new(
                infoNumber: 123,
                errorState: 0x02,
                errorClass: 0x03,
                server: "foo",
                errorMessage: "bar",
                procedure: "baz",
                lineNumber: 234,
                exception: new Exception(),
                batchIndex: 345);

            // Act - Serialize and deserialize
            serializer.WriteObject(stream, originalError);
            stream.Position = 0;
            SqlError? actualError = serializer.ReadObject(stream) as SqlError;
            
            // Assert
            Assert.NotNull(actualError);
            Assert.Equal(originalError.Source, actualError.Source);
            Assert.Equal(originalError.Number, actualError.Number);
            Assert.Equal(originalError.State, actualError.State);
            Assert.Equal(originalError.Class, actualError.Class);
            Assert.Equal(originalError.Server, actualError.Server);
            Assert.Equal(originalError.Message, actualError.Message);
            Assert.Equal(originalError.Procedure, actualError.Procedure);
            Assert.Equal(originalError.LineNumber, actualError.LineNumber);
            Assert.Equal(originalError.Win32ErrorCode, actualError.Win32ErrorCode);
            Assert.Equal(originalError.BatchIndex, actualError.BatchIndex);
            
            Assert.NotNull(actualError.Exception);
            Assert.Equal(originalError.Exception.Message, actualError.Exception.Message);
            Assert.Equal(originalError.Exception.HResult, actualError.Exception.HResult);
        }
    }
}
