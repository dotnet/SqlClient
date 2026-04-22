// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.ProviderBase;
using Xunit;
using System.Data;
using System.Data.Common;
using System.Transactions;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    public class ConnectionPoolSlotsTest
    {
        // Mock implementation of DbConnectionInternal for testing
        private class MockDbConnectionInternal : DbConnectionInternal
        {
            public MockDbConnectionInternal() : base(ConnectionState.Open, true, false) { }

            public override string ServerVersion => "Mock Server 1.0";

            public override DbTransaction BeginTransaction(System.Data.IsolationLevel il)
            {
                throw new NotImplementedException();
            }

            public override void EnlistTransaction(Transaction transaction)
            {
                // Mock implementation - do nothing
            }

            protected override void Activate(Transaction transaction)
            {
                // Mock implementation - do nothing
            }

            protected override void Deactivate()
            {
                // Mock implementation - do nothing
            }

            internal override void ResetConnection()
            {
                // Mock implementation - do nothing
            }
        }

        [Fact]
        public void Constructor_ValidCapacity_SetsReservationCountToZero()
        {
            // Arrange & Act
            var poolSlots = new ConnectionPoolSlots(5);

            // Assert
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void Constructor_ZeroCapacity_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionPoolSlots(0));
            Assert.Equal("fixedCapacity", exception.ParamName);
            Assert.Contains("Capacity must be greater than zero", exception.Message);
        }

        [Fact]
        public void Constructor_CapacityGreaterThanIntMaxValue_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            uint invalidCapacity = (uint)int.MaxValue + 1;

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionPoolSlots(invalidCapacity));
            Assert.Equal("fixedCapacity", exception.ParamName);
            Assert.Contains("Capacity must be less than or equal to Int32.MaxValue", exception.Message);
        }

        [Theory]
        [InlineData(10000u)]
        public void Constructor_LargeCapacityValues_SetsReservationCountToZero(uint capacity)
        {
            // Arrange & Act
            var poolSlots = new ConnectionPoolSlots(capacity);

            // Assert
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void Add_ValidConnection_ReturnsConnectionAndIncrementsReservationCount()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var createCallbackCount = 0;
            
            // Act
            var connection = poolSlots.Add(
                createCallback: () => {
                    createCallbackCount++;
                    return new MockDbConnectionInternal();
                },
                cleanupCallback: (conn) => Assert.Fail());

            // Assert
            Assert.NotNull(connection);
            Assert.Equal(1, poolSlots.ReservationCount);
            Assert.Equal(1, createCallbackCount);
        }

        [Fact]
        public void Add_NullFromCreateCallback_ReturnsNullAndDoesNotIncrementReservationCount()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var createCallbackCount = 0;

            // Act
            var connection = poolSlots.Add(
                createCallback: () => {
                    createCallbackCount++;
                    return null;
                },
                cleanupCallback: (conn) => Assert.Fail());

            // Assert
            Assert.Null(connection);
            Assert.Equal(0, poolSlots.ReservationCount);
            Assert.Equal(1, createCallbackCount);
        }

        [Fact]
        public void Add_AtCapacity_ReturnsNullForAdditionalConnections()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(1);
            var createCallbackCount = 0;
            
            // Act - Add first connection
            var connection1 = poolSlots.Add(
                createCallback: () => {
                    createCallbackCount++;
                    return new MockDbConnectionInternal();
                },
                cleanupCallback: (conn) => Assert.Fail());

            // Act - Try to add second connection beyond capacity
            var connection2 = poolSlots.Add(
                createCallback: () =>
                {
                    Assert.Fail();
                    return null;
                },
                cleanupCallback: (conn) => {
                    Assert.Fail();
                });

            // Assert
            Assert.NotNull(connection1);
            Assert.Null(connection2);
            Assert.Equal(1, poolSlots.ReservationCount);
            Assert.Equal(1, createCallbackCount);
        }

        [Fact]
        public void Add_CreateCallbackThrowsException_CallsCleanupCallbackAndRethrowsException()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var createCallbackCount = 0;
            var cleanupCallbackCount = 0;
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                poolSlots.Add(
                    createCallback: () => {
                        createCallbackCount++;
                        throw new InvalidOperationException("Test exception");
                    },
                    cleanupCallback: (conn) => cleanupCallbackCount++));

            Assert.Contains("Test exception", exception.Message);
            Assert.Equal(0, poolSlots.ReservationCount);
            Assert.Equal(1, createCallbackCount);
            Assert.Equal(1, cleanupCallbackCount);
        }

        [Fact]
        public void Add_MultipleConnections_IncrementsReservationCountCorrectly()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var createCallbackCount = 0;
            var createCallbackCount2 = 0;

            // Act
            var connection1 = poolSlots.Add(
                createCallback: () =>
                {
                    createCallbackCount++;
                    return new MockDbConnectionInternal();
                },
                cleanupCallback: (conn) => Assert.Fail());

            var connection2 = poolSlots.Add(
                createCallback: () =>
                {
                    createCallbackCount2++;
                    return new MockDbConnectionInternal();
                },
                cleanupCallback: (conn) => Assert.Fail());

            // Assert
            Assert.NotNull(connection1);
            Assert.NotNull(connection2);
            Assert.Equal(2, poolSlots.ReservationCount);
            Assert.Equal(1, createCallbackCount);
            Assert.Equal(1, createCallbackCount2);
        }

        [Fact]
        public void TryRemove_ExistingConnection_ReturnsTrueAndDecrementsReservationCount()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var connection = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });

            var reservationCountBeforeRemove = poolSlots.ReservationCount;

            // Act
            var removed = poolSlots.TryRemove(connection!);

            // Assert
            Assert.Equal(1, reservationCountBeforeRemove);
            Assert.True(removed);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void TryRemove_NonExistentConnection_ReturnsFalseAndDoesNotChangeReservationCount()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var connection = new MockDbConnectionInternal();
            var connection2 = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });
            var reservationCountBeforeRemove = poolSlots.ReservationCount;

            // Act
            var removed = poolSlots.TryRemove(connection);

            // Assert
            Assert.Equal(1, reservationCountBeforeRemove);
            Assert.False(removed);
            Assert.Equal(1, poolSlots.ReservationCount);
        }

        [Fact]
        public void TryRemove_SameConnectionTwice_ReturnsFalseOnSecondAttempt()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var connection = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });
            var reservationCountBeforeRemove = poolSlots.ReservationCount;

            // Act
            var firstRemove = poolSlots.TryRemove(connection!);
            var secondRemove = poolSlots.TryRemove(connection!);

            // Assert
            Assert.Equal(1, reservationCountBeforeRemove);
            Assert.True(firstRemove);
            Assert.False(secondRemove);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void TryRemove_SameConnectionTwice_ReturnsTrueWhenAddedTwice()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var commonConnection = new MockDbConnectionInternal();
            var connection = poolSlots.Add(
                createCallback: () => commonConnection,
                cleanupCallback: (conn) => { });
            var connection2 = poolSlots.Add(
                createCallback: () => commonConnection,
                cleanupCallback: (conn) => { });
            var reservationCountBeforeRemove = poolSlots.ReservationCount;

            // Act
            var firstRemove = poolSlots.TryRemove(connection!);
            var reservationCountAfterFirstRemove = poolSlots.ReservationCount;
            var secondRemove = poolSlots.TryRemove(connection2!);

            // Assert
            Assert.Equal(2, reservationCountBeforeRemove);
            Assert.True(firstRemove);
            Assert.Equal(1, reservationCountAfterFirstRemove);
            Assert.True(secondRemove);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void TryRemove_MultipleConnections_RemovesCorrectConnection()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var connection1 = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });
                
            var connection2 = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });

            // Act
            var removed = poolSlots.TryRemove(connection1!);

            // Assert
            Assert.True(removed);
            Assert.Equal(1, poolSlots.ReservationCount);

            // Act
            var removed2 = poolSlots.TryRemove(connection1!);

            // Assert
            Assert.False(removed2); // Should return false since connection1 was already removed
            Assert.Equal(1, poolSlots.ReservationCount);

            // Act
            var removed3 = poolSlots.TryRemove(connection2!);

            // Assert
            Assert.True(removed3);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void ConcurrentAddAndRemove_MaintainsCorrectReservationCount()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(100);
            const int operationCount = 50;
            var connections = new DbConnectionInternal[operationCount];
            var addTasks = new Task[operationCount];

            // Act - Add connections concurrently
            for (int i = 0; i < operationCount; i++)
            {
                int index = i;
                addTasks[i] = Task.Run(() =>
                {
                    connections[index] = poolSlots.Add(
                        createCallback: () => new MockDbConnectionInternal(),
                        cleanupCallback: (conn) => { })!;
                });
            }

            // Wait for all add operations to complete
            Task.WaitAll(addTasks);

            // Verify all connections were added
            Assert.Equal(operationCount, poolSlots.ReservationCount);

            var removeTasks = new Task[operationCount];

            // Act - Remove connections concurrently
            for (int i = 0; i < operationCount; i++)
            {
                int index = i;
                removeTasks[i] = Task.Run(() =>
                {
                    poolSlots.TryRemove(connections[index]);
                });                
            }

            // Wait for all remove operations to complete
            Task.WaitAll(removeTasks);

            // Assert
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(5u)]
        [InlineData(10u)]
        [InlineData(100u)]
        public void Add_FillToCapacity_RespectsCapacityLimits(uint capacity)
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(capacity);
            var connections = new DbConnectionInternal[capacity];

            // Act - Fill to capacity
            for (int i = 0; i < capacity; i++)
            {
                connections[i] = poolSlots.Add(
                    createCallback: () => new MockDbConnectionInternal(),
                    cleanupCallback: (conn) => { })!;
                Assert.NotNull(connections[i]);
            }

            // Try to add one more beyond capacity
            var extraConnection = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });

            // Assert
            Assert.Equal((int)capacity, poolSlots.ReservationCount);
            Assert.Null(extraConnection); // The overflow connection should be null
        }

        [Fact]
        public void ReservationCount_AfterAddAndRemoveOperations_ReflectsCurrentState()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(10);

            // Act & Assert - Start with 0
            Assert.Equal(0, poolSlots.ReservationCount);

            // Add 3 connections
            var conn1 = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });
            Assert.Equal(1, poolSlots.ReservationCount);

            var conn2 = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });
            Assert.Equal(2, poolSlots.ReservationCount);

            var conn3 = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });
            Assert.Equal(3, poolSlots.ReservationCount);

            // Remove 1 connection
            poolSlots.TryRemove(conn2!);
            Assert.Equal(2, poolSlots.ReservationCount);

            // Remove remaining connections
            poolSlots.TryRemove(conn1!);
            Assert.Equal(1, poolSlots.ReservationCount);

            poolSlots.TryRemove(conn3!);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void Constructor_EdgeCase_CapacityOfOne_WorksCorrectly()
        {
            // Arrange & Act
            var poolSlots = new ConnectionPoolSlots(1);

            // Assert - Should be able to add one connection
            var connection = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });

            Assert.NotNull(connection);
            Assert.Equal(1, poolSlots.ReservationCount);

            // Should not be able to add a second connection
            var connection2 = poolSlots.Add(
                createCallback: () => new MockDbConnectionInternal(),
                cleanupCallback: (conn) => { });

            Assert.Null(connection2);
            Assert.Equal(1, poolSlots.ReservationCount);
        }
    }
}
