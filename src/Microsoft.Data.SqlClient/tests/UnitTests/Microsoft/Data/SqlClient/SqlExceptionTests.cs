// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
#if NETFRAMEWORK
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Data.Common.ConnectionString;
#endif
using Xunit;
using static Microsoft.Data.SqlClient.UnitTests.Utilities.EncryptionInfoTestData;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Unit tests for <see cref="SqlException"/>.
/// </summary>
public class SqlExceptionTests
{
    // These tests use BinaryFormatter, which only exists on .NET Framework; it was removed from .NET.
#if NETFRAMEWORK
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

    /// <summary>
    /// Verifies that when a <see cref="SqlException"/> is roundtripped correctly through a
    /// serialization process.
    /// </summary>
    /// <remarks>
    /// This serialization process (BinaryFormatter) is not available on netcore due to safety
    /// concerns. These concerns do not apply in this test, since the test maintains control
    /// over the entire payload.
    /// </remarks>
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
#endif

    #region EncryptionInfo

    /// <summary>
    /// Verifies exceptions created via the error-collection factory return an empty snapshot.
    /// </summary>
    [Fact]
    public void EncryptionInfo_CreateFromCollection_ReturnsEmptySnapshot()
    {
        // Act
        SqlException exception = SqlException.CreateException(CreateErrorCollection(), ServerVersion);

        // Assert
        AssertEmpty(exception.EncryptionInfo);
    }

    /// <summary>
    /// Verifies exceptions created via the single-error factory return an empty snapshot.
    /// </summary>
    [Fact]
    public void EncryptionInfo_CreateFromError_ReturnsEmptySnapshot()
    {
        // Act
        SqlException exception = SqlException.CreateException(
            CreateError(),
            ServerVersion,
            internalConnection: null,
            innerException: new InvalidOperationException("inner"));

        // Assert
        AssertEmpty(exception.EncryptionInfo);
    }

    /// <summary>
    /// Verifies exceptions created via the connection-id factory return an empty snapshot.
    /// </summary>
    [Fact]
    public void EncryptionInfo_CreateWithConnectionId_ReturnsEmptySnapshot()
    {
        // Act
        SqlException exception = SqlException.CreateException(
            CreateErrorCollection(),
            ServerVersion,
            Guid.NewGuid(),
            innerException: null);

        // Assert
        AssertEmpty(exception.EncryptionInfo);
    }

    /// <summary>
    /// Verifies exceptions created via the batch-command factory return an empty snapshot.
    /// </summary>
    [Fact]
    public void EncryptionInfo_CreateWithBatchCommand_ReturnsEmptySnapshot()
    {
        // Act
        SqlException exception = SqlException.CreateException(
            CreateErrorCollection(),
            ServerVersion,
            new SqlBatchCommand("SELECT 1"));

        // Assert
        AssertEmpty(exception.EncryptionInfo);
    }

    /// <summary>
    /// Verifies that a populated snapshot set internally is returned by the getter and is
    /// preserved by <see cref="SqlException.InternalClone"/>.
    /// </summary>
    [Fact]
    public void EncryptionInfo_PopulatedSnapshot_PreservedByInternalClone()
    {
        // Arrange
        SqlConnectionEncryptionInfo info = CreatePopulated();
        SqlException exception = SqlException.CreateException(CreateErrorCollection(), ServerVersion);
        exception.EncryptionInfo = info;

        // Act
        SqlException clone = exception.InternalClone();

        // Assert
        Assert.Same(info, exception.EncryptionInfo);
        Assert.Same(info, clone.EncryptionInfo);
        Assert.Equal(SampleCipherSuite, clone.EncryptionInfo.NegotiatedCipherSuiteId);
    }

    /// <summary>
    /// Verifies that clearing the snapshot internally falls back to the empty snapshot.
    /// </summary>
    [Fact]
    public void EncryptionInfo_SetNull_ReturnsEmptySnapshot()
    {
        // Arrange
        SqlException exception = SqlException.CreateException(CreateErrorCollection(), ServerVersion);
        exception.EncryptionInfo = CreatePopulated();

        // Act
        // The internal setter accepts null to clear the snapshot.
        exception.EncryptionInfo = null!;

        // Assert
        AssertEmpty(exception.EncryptionInfo);
    }

    /// <summary>
    /// Verifies that a deserialized exception returns a non-null empty snapshot and that the
    /// snapshot is not written to the serialized payload.
    /// </summary>
    [Fact]
    public void EncryptionInfo_DataContractRoundTrip_ReturnsEmptySnapshot()
    {
        // Arrange
        SqlException original = SqlException.CreateException(CreateErrorCollection(), ServerVersion);
        original.EncryptionInfo = CreatePopulated();

        // Exception.Data is backed by an internal dictionary type, and GetObjectData writes the
        // error collection, so these types must be known to the serializer.
        DataContractSerializer serializer = new(
            typeof(SqlException),
            new[]
            {
                original.Data.GetType(),
                typeof(SqlErrorCollection),
                typeof(SqlError),
                typeof(List<object>),
            });
        using MemoryStream stream = new();

        // Act
        serializer.WriteObject(stream, original);
        stream.Position = 0;
        string payload = new StreamReader(stream).ReadToEnd();
        stream.Position = 0;
        SqlException? roundTripped = serializer.ReadObject(stream) as SqlException;

        // Assert
        // The snapshot, including the certificate, is not written to the payload.
        Assert.DoesNotContain(nameof(SqlException.EncryptionInfo), payload);
        Assert.DoesNotContain(SampleCertificateThumbprint, payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SampleCertificateBase64.Substring(0, 24), payload);
        Assert.DoesNotContain("encryption-info.test", payload);

        // The deserialized exception reports an empty snapshot.
        Assert.NotNull(roundTripped);
        Assert.Equal(original.Message, roundTripped.Message);
        AssertEmpty(roundTripped.EncryptionInfo);
    }

    // BinaryFormatter only exists on .NET Framework.
#if NETFRAMEWORK
    /// <summary>
    /// Verifies BinaryFormatter serialization is unaffected by the non-serialized snapshot field
    /// and that the deserialized exception returns a non-null empty snapshot.
    /// </summary>
    [Fact]
    public void EncryptionInfo_BinaryFormatterRoundTrip_ReturnsEmptySnapshot()
    {
        // Arrange
        SqlException original = SqlException.CreateException(CreateErrorCollection(), ServerVersion);
        original.EncryptionInfo = CreatePopulated();
        BinaryFormatter formatter = new();
        using MemoryStream stream = new();

        // Act
        formatter.Serialize(stream, original);
        stream.Position = 0;
        SqlException roundTripped = (SqlException)formatter.Deserialize(stream);

        // Assert
        Assert.Equal(original.Message, roundTripped.Message);
        AssertEmpty(roundTripped.EncryptionInfo);
    }
#endif

    /// <summary>
    /// Verifies that <see cref="Exception.Message"/> and <see cref="SqlException.ToString"/> do
    /// not change when a populated snapshot is attached, and that no snapshot value leaks into
    /// them.
    /// </summary>
    [Fact]
    public void EncryptionInfo_PopulatedSnapshot_MessageAndToStringUnchanged()
    {
        // Arrange
        Guid connectionId = Guid.NewGuid();
        SqlException withoutInfo = SqlException.CreateException(CreateErrorCollection(), ServerVersion, connectionId, null);
        SqlException withInfo = SqlException.CreateException(CreateErrorCollection(), ServerVersion, connectionId, null);

        // Act
        withInfo.EncryptionInfo = CreatePopulated();

        // Assert
        Assert.Equal(withoutInfo.Message, withInfo.Message);
        Assert.Equal(withoutInfo.ToString(), withInfo.ToString());
        Assert.DoesNotContain(SampleCertificateThumbprint, withInfo.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SampleCertificateThumbprint, withInfo.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SampleCertificateBase64.Substring(0, 24), withInfo.ToString());
        Assert.DoesNotContain("encryption-info.test", withInfo.ToString());
        Assert.DoesNotContain(nameof(SqlConnectionEncryptionInfo.GetServerCertificate), withInfo.ToString());
        Assert.DoesNotContain(nameof(SqlConnectionEncryptionMode.Strict), withInfo.Message);
        Assert.DoesNotContain("NegotiatedCipherSuite", withInfo.ToString());
        Assert.DoesNotContain("TLS_AES_256_GCM_SHA384", withInfo.ToString());
    }

    /// <summary>
    /// Verifies that a cloned exception exposes an equivalent certificate, and that disposing a
    /// certificate returned from one exception does not affect the other.
    /// </summary>
    [Fact]
    public void EncryptionInfo_InternalClone_ServerCertificateIsIsolated()
    {
        // Arrange
        SqlException exception = SqlException.CreateException(CreateErrorCollection(), ServerVersion);
        exception.EncryptionInfo = CreatePopulated();
        SqlException clone = exception.InternalClone();

        // Act
        X509Certificate2? fromOriginal = exception.EncryptionInfo.GetServerCertificate();
        using X509Certificate2? fromClone = clone.EncryptionInfo.GetServerCertificate();
        Assert.NotNull(fromOriginal);
        fromOriginal.Dispose();
        using X509Certificate2? again = exception.EncryptionInfo.GetServerCertificate();

        // Assert
        Assert.NotNull(fromClone);
        Assert.NotNull(again);
        Assert.NotSame(fromOriginal, fromClone);
        Assert.Equal(SampleCertificateThumbprint, fromClone.Thumbprint);
        Assert.Equal(SampleCertificateThumbprint, again.Thumbprint);
    }

    #endregion
}
