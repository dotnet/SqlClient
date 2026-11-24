// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests;

/// <summary>Exercises the write-to-read handoff against SQL Server using public APIs.</summary>
[Trait("Set", "3")]
public class SqlCommandAsyncWriteTests
{
    /// <summary>A streamed parameter keeps command state protected until execution completes.</summary>
    [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
    public async Task ExecuteReaderAsync_PendingParameterWrite_PreservesStateAndAllowsReuse()
    {
        // Arrange
        using SqlConnection connection = DataTestUtility.CreateConnection();
        await connection.OpenAsync();
        using SqlTransaction transaction = connection.BeginTransaction();
        using GatedReadStream stream = new();
        using SqlCommand command = new("SELECT DATALENGTH(@value)", connection, transaction);
        command.Parameters.Add("@value", SqlDbType.VarBinary, -1).Value = stream;

        // Act
        Task<SqlDataReader> execution = command.ExecuteReaderAsync();
        try
        {
            // Assert
            Assert.True(stream.ReadStarted);
            Assert.False(execution.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => command.Connection = null);
            Assert.Throws<InvalidOperationException>(() => command.Transaction = null);
            Assert.Throws<InvalidOperationException>(() => command.ExecuteReader());
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteReaderAsync());
            Assert.Same(connection, command.Connection);
            Assert.Same(transaction, command.Transaction);
        }
        finally
        {
            // Cleanup
            command.Connection = connection;
            command.Transaction = transaction;
            stream.Complete();
            using SqlDataReader reader = await execution;
            Assert.True(await reader.ReadAsync());
            Assert.Equal(0, reader.GetInt64(0));
            Assert.False(await reader.ReadAsync());
        }

        // The same command remains usable after the handoff and reader disposal.
        command.Parameters.Clear();
        command.CommandText = "SELECT 42";
        Assert.Equal(42, await command.ExecuteScalarAsync());
        command.Transaction = null;
        command.Transaction = transaction;
        transaction.Rollback();
        command.Connection = null;
        Assert.Null(command.Connection);
    }
}
