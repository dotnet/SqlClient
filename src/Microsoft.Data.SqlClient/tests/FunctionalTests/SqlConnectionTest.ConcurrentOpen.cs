// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    /// <summary>
    /// Tests that confirm the race condition described in GitHub issue #3314:
    /// If _innerConnection is set to DbConnectionClosedConnecting (a transitional state)
    /// at the moment TryOpenInner casts it to SqlConnectionInternal, an InvalidCastException
    /// is thrown. This simulates a concurrent Open() on the same SqlConnection instance.
    /// </summary>
    public partial class SqlConnectionTest
    {
        /// <summary>
        /// Verifies that when _innerConnection holds DbConnectionClosedConnecting (a non-open
        /// transitional state), casting it to SqlConnectionInternal throws InvalidCastException.
        /// This is the exact exception observed in issue #3314.
        /// </summary>
        [Fact]
        public void InnerConnection_CastToSqlConnectionInternal_ThrowsInvalidCast_WhenInConnectingState()
        {
            // Arrange: get the DbConnectionClosedConnecting singleton via reflection
            Type closedConnectingType = typeof(SqlConnection).Assembly
                .GetType("Microsoft.Data.ProviderBase.DbConnectionClosedConnecting", throwOnError: true);
            FieldInfo singletonField = closedConnectingType
                .GetField("SingletonInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(singletonField);
            object connectingSingleton = singletonField.GetValue(null);
            Assert.NotNull(connectingSingleton);

            // Verify it is NOT SqlConnectionInternal — this is the root cause of the InvalidCastException
            Type sqlConnectionInternalType = typeof(SqlConnection).Assembly
                .GetType("Microsoft.Data.SqlClient.Connection.SqlConnectionInternal", throwOnError: true);
            Assert.False(
                sqlConnectionInternalType.IsInstanceOfType(connectingSingleton),
                "DbConnectionClosedConnecting must not be assignable to SqlConnectionInternal");

            // Act: set a SqlConnection's _innerConnection to the Connecting state to simulate the race
            var connection = new SqlConnection("Data Source=localhost");
            FieldInfo innerConnectionField = typeof(SqlConnection)
                .GetField("_innerConnection", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(innerConnectionField);

            innerConnectionField.SetValue(connection, connectingSingleton);

            // Read it back through InnerConnection and attempt the same cast that TryOpenInner does
            PropertyInfo innerConnectionProperty = typeof(SqlConnection)
                .GetProperty("InnerConnection", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(innerConnectionProperty);
            object innerConnection = innerConnectionProperty.GetValue(connection);

            // Assert: the runtime type is not assignable to SqlConnectionInternal
            Assert.False(
                sqlConnectionInternalType.IsAssignableFrom(innerConnection.GetType()),
                $"Expected InnerConnection type '{innerConnection.GetType().FullName}' to NOT be assignable " +
                $"to '{sqlConnectionInternalType.FullName}'. If it were, the cast in TryOpenInner would " +
                "succeed and the race condition in issue #3314 would not manifest as InvalidCastException.");

            // Perform the exact cast that TryOpenInner does at SqlConnection.cs line 2228:
            //   var tdsInnerConnection = (SqlConnectionInternal)InnerConnection;
            // This must throw InvalidCastException when _innerConnection is DbConnectionClosedConnecting.
            Exception ex = Assert.ThrowsAny<Exception>(() =>
            {
                // Use Convert.ChangeType or direct cast via reflection to replicate
                // the CLR's cast behavior for internal types we cannot reference directly.
                Convert.ChangeType(innerConnection, sqlConnectionInternalType);
            });
            Assert.True(
                ex is InvalidCastException,
                $"Expected InvalidCastException but got {ex.GetType().Name}: {ex.Message}");
        }

        /// <summary>
        /// Verifies that a SqlConnection in the Connecting state reports ConnectionState.Connecting,
        /// which is an unexpected state for post-open code to encounter.
        /// </summary>
        [Fact]
        public void InnerConnection_InConnectingState_ReportsConnectingState()
        {
            // Arrange
            Type closedConnectingType = typeof(SqlConnection).Assembly
                .GetType("Microsoft.Data.ProviderBase.DbConnectionClosedConnecting", throwOnError: true);
            FieldInfo singletonField = closedConnectingType
                .GetField("SingletonInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            object connectingSingleton = singletonField.GetValue(null);

            var connection = new SqlConnection("Data Source=localhost");
            FieldInfo innerConnectionField = typeof(SqlConnection)
                .GetField("_innerConnection", BindingFlags.Instance | BindingFlags.NonPublic);
            innerConnectionField.SetValue(connection, connectingSingleton);

            // Act & Assert: the state should be Connecting, not Open
            // This confirms the connection is in a transitional state where
            // TryOpenInner's cast would fail
            Assert.Equal(ConnectionState.Connecting, connection.State);
        }

        /// <summary>
        /// Verifies that calling Open() on a SqlConnection that is already in the Connecting state
        /// throws InvalidOperationException ("already open"), confirming that concurrent Open()
        /// calls on the same instance are not supported.
        /// </summary>
        [Fact]
        public void Open_WhenAlreadyConnecting_ThrowsInvalidOperation()
        {
            // Arrange: force `_innerConnection` to DbConnectionClosedConnecting
            Type closedConnectingType = typeof(SqlConnection).Assembly
                .GetType("Microsoft.Data.ProviderBase.DbConnectionClosedConnecting", throwOnError: true);
            FieldInfo singletonField = closedConnectingType
                .GetField("SingletonInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            object connectingSingleton = singletonField.GetValue(null);

            var connection = new SqlConnection("Data Source=localhost");
            FieldInfo innerConnectionField = typeof(SqlConnection)
                .GetField("_innerConnection", BindingFlags.Instance | BindingFlags.NonPublic);
            innerConnectionField.SetValue(connection, connectingSingleton);

            // Act & Assert: Open() while connecting should throw
            // DbConnectionClosedConnecting.TryOpenConnection throws "connection already open"
            // when retry is null (synchronous path)
            Assert.Throws<InvalidOperationException>(() => connection.Open());
        }
    }
}
