// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Data.Common.ConnectionString;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

public class SqlExceptionTests
{
    private static SqlError ExampleSqlError =>
        new(infoNumber: 123,
            errorState: 2,
            errorClass: 3,
            server: "ServerName",
            errorMessage: "SqlError Message",
            procedure: "ProcedureName",
            lineNumber: 234,
            win32ErrorCode: 15,
            exception: new ArgumentNullException(paramName: "param", message: "Associated exception"),
            batchIndex: 0);

    [Fact]
    public void Serialization_RoundTrips()
    {
        // Arrange
        // - Create the test exception
        SqlBatchCommand originalBatchCommand = new("SELECT @@VERSION");
        Guid originalConnId = Guid.NewGuid();
        SqlException originalException = SqlException.CreateException(
            errorCollection: [ExampleSqlError],
            serverVersion: "0.0.000.0",
            conId: originalConnId,
            innerException: null,
            batchCommand: originalBatchCommand);
        originalException._doNotReconnect = true;

        // Set up the serialization infrastructure
        BinaryFormatter binaryFormatter = new();
        using MemoryStream stream = new();

        // Act - Serialize and deserialize
        binaryFormatter.Serialize(stream, originalException);
        stream.Position = 0;
        SqlException? actualException = binaryFormatter.Deserialize(stream) as SqlException;

        // Assert
        Assert.NotNull(actualException);
        Assert.NotNull(actualException.InnerException);
        Assert.NotNull(actualException.Errors);

        Assert.Equal(originalConnId, actualException.ClientConnectionId);
        Assert.Equal(originalException.InnerException!.ToString(), actualException.InnerException.ToString());
        Assert.Equal(originalException.Errors.Count, actualException.Errors.Count);

        Assert.Equal(DbConnectionStringDefaults.ApplicationName, actualException.Errors[0].Source);
        Assert.Equal(ExampleSqlError.Number, actualException.Errors[0].Number);
        Assert.Equal(ExampleSqlError.State, actualException.Errors[0].State);
        Assert.Equal(ExampleSqlError.Class, actualException.Errors[0].Class);
        Assert.Equal(ExampleSqlError.Server, actualException.Errors[0].Server);
        Assert.Equal(ExampleSqlError.Message, actualException.Errors[0].Message);
        Assert.Equal(ExampleSqlError.Procedure, actualException.Errors[0].Procedure);
        Assert.Equal(ExampleSqlError.LineNumber, actualException.Errors[0].LineNumber);
        Assert.Equal(ExampleSqlError.Win32ErrorCode, actualException.Errors[0].Win32ErrorCode);
        Assert.Equal(ExampleSqlError.BatchIndex, actualException.Errors[0].BatchIndex);

        Assert.NotNull(actualException.Errors[0].Exception);
        Assert.Equal(ExampleSqlError.Exception.ToString(), actualException.Errors[0].Exception.ToString());

        // Some fields are not serialized
        Assert.Null(actualException.BatchCommand);
        Assert.False(actualException._doNotReconnect);
    }
}

#endif
