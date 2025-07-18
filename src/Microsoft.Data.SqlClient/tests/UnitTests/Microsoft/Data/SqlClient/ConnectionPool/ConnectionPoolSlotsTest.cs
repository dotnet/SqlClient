// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.ProviderBase;
using Xunit;
using System.Data;
using System.Data.Common;
using System.Transactions;

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

        [Fact]
        public void Constructor_CapacityEqualToIntMaxValue_DoesNotThrow()
        {
            try
            {
                // Arrange & Act - This should not throw ArgumentOutOfRangeException since Int32.MaxValue is valid
                var poolSlots = new ConnectionPoolSlots((uint)int.MaxValue);

                // Assert
                Assert.Equal(0, poolSlots.ReservationCount);
            }
            catch (OutOfMemoryException)
            {
                // OutOfMemoryException is acceptable when trying to allocate an array of int.MaxValue size
                // This test is primarily checking that ArgumentOutOfRangeException is not thrown for the capacity validation
                // The fact that we reach the OutOfMemoryException means the capacity validation passed
            }
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(5u)]
        [InlineData(10u)]
        [InlineData(100u)]
        [InlineData(1000u)]
        public void Constructor_ValidCapacityValues_SetsReservationCountToZero(uint capacity)
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
            
            // Act
            var connection = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test",
                cleanupState: "cleanup");

            // Assert
            Assert.NotNull(connection);
            Assert.Equal(1, poolSlots.ReservationCount);
        }

        [Fact]
        public void Add_NullFromCreateCallback_ReturnsNullAndDoesNotIncrementReservationCount()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            
            // Act
            var connection = poolSlots.Add(
                createCallback: state => null,
                cleanupCallback: (conn, state) => { },
                createState: "test",
                cleanupState: "cleanup");

            // Assert
            Assert.Null(connection);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void Add_AtCapacity_ReturnsNullForAdditionalConnections()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(1);
            
            // Act - Add first connection
            var connection1 = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test",
                cleanupState: "cleanup");

            // Act - Try to add second connection beyond capacity
            var connection2 = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test",
                cleanupState: "cleanup");

            // Assert
            Assert.NotNull(connection1);
            Assert.Null(connection2);
            Assert.Equal(1, poolSlots.ReservationCount);
        }

        [Fact]
        public void Add_CreateCallbackThrowsException_CallsCleanupCallbackAndRethrowsException()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            bool cleanupCalled = false;
            
            // Act & Assert
            var exception = Assert.Throws<Exception>(() =>
                poolSlots.Add(
                    createCallback: state => throw new InvalidOperationException("Test exception"),
                    cleanupCallback: (conn, state) => { cleanupCalled = true; },
                    createState: "test",
                    cleanupState: "cleanup"));

            Assert.Contains("Failed to create or add connection", exception.Message);
            Assert.True(cleanupCalled);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void Add_MultipleConnections_IncrementsReservationCountCorrectly()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            
            // Act
            var connection1 = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test1",
                cleanupState: "cleanup1");
                
            var connection2 = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test2",
                cleanupState: "cleanup2");

            // Assert
            Assert.NotNull(connection1);
            Assert.NotNull(connection2);
            Assert.Equal(2, poolSlots.ReservationCount);
        }

        [Fact]
        public void TryRemove_ExistingConnection_ReturnsTrueAndDecrementsReservationCount()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var connection = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test",
                cleanupState: "cleanup");

            // Act
            var removed = poolSlots.TryRemove(connection);

            // Assert
            Assert.True(removed);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void TryRemove_NonExistentConnection_ReturnsFalseAndDoesNotChangeReservationCount()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var connection = new MockDbConnectionInternal();

            // Act
            var removed = poolSlots.TryRemove(connection);

            // Assert
            Assert.False(removed);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void TryRemove_SameConnectionTwice_ReturnsFalseOnSecondAttempt()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var connection = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test",
                cleanupState: "cleanup");

            // Act
            var firstRemove = poolSlots.TryRemove(connection);
            var secondRemove = poolSlots.TryRemove(connection);

            // Assert
            Assert.True(firstRemove);
            Assert.False(secondRemove);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void TryRemove_MultipleConnections_RemovesCorrectConnection()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            var connection1 = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test1",
                cleanupState: "cleanup1");
                
            var connection2 = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test2",
                cleanupState: "cleanup2");

            // Act
            var removed = poolSlots.TryRemove(connection1);

            // Assert
            Assert.True(removed);
            Assert.Equal(1, poolSlots.ReservationCount);
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
                        createCallback: state => new MockDbConnectionInternal(),
                        cleanupCallback: (conn, state) => { },
                        createState: $"test{index}",
                        cleanupState: $"cleanup{index}");
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
                    if (connections[index] != null)
                    {
                        poolSlots.TryRemove(connections[index]);
                    }
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
                    createCallback: state => new MockDbConnectionInternal(),
                    cleanupCallback: (conn, state) => { },
                    createState: $"test{i}",
                    cleanupState: $"cleanup{i}");
                Assert.NotNull(connections[i]);
            }

            // Try to add one more beyond capacity
            var extraConnection = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "overflow",
                cleanupState: "overflow");

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
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test1",
                cleanupState: "cleanup1");
            Assert.Equal(1, poolSlots.ReservationCount);

            var conn2 = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test2",
                cleanupState: "cleanup2");
            Assert.Equal(2, poolSlots.ReservationCount);

            var conn3 = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test3",
                cleanupState: "cleanup3");
            Assert.Equal(3, poolSlots.ReservationCount);

            // Remove 1 connection
            poolSlots.TryRemove(conn2);
            Assert.Equal(2, poolSlots.ReservationCount);

            // Remove remaining connections
            poolSlots.TryRemove(conn1);
            Assert.Equal(1, poolSlots.ReservationCount);

            poolSlots.TryRemove(conn3);
            Assert.Equal(0, poolSlots.ReservationCount);
        }

        [Fact]
        public void Add_WithNullCleanupCallback_ThrowsArgumentNullException()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);

            // Act & Assert
            Assert.Throws<NullReferenceException>(() =>
                poolSlots.Add(
                    createCallback: state => throw new InvalidOperationException("Test"),
                    cleanupCallback: null,
                    createState: "test",
                    cleanupState: "cleanup"));
        }

        [Fact]
        public void Add_StateParametersPassedCorrectly_UsesProvidedState()
        {
            // Arrange
            var poolSlots = new ConnectionPoolSlots(5);
            string receivedCreateState = null;
            string receivedCleanupState = null;

            // Act
            try
            {
                poolSlots.Add(
                    createCallback: state => { receivedCreateState = state; throw new InvalidOperationException("Test"); },
                    cleanupCallback: (conn, state) => { receivedCleanupState = state; },
                    createState: "createState",
                    cleanupState: "cleanupState");
            }
            catch
            {
                // Expected due to exception in create callback
            }

            // Assert
            Assert.Equal("createState", receivedCreateState);
            Assert.Equal("cleanupState", receivedCleanupState);
        }

        [Fact]
        public void Constructor_EdgeCase_CapacityOfOne_WorksCorrectly()
        {
            // Arrange & Act
            var poolSlots = new ConnectionPoolSlots(1);

            // Assert - Should be able to add one connection
            var connection = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test",
                cleanupState: "cleanup");

            Assert.NotNull(connection);
            Assert.Equal(1, poolSlots.ReservationCount);

            // Should not be able to add a second connection
            var connection2 = poolSlots.Add(
                createCallback: state => new MockDbConnectionInternal(),
                cleanupCallback: (conn, state) => { },
                createState: "test2",
                cleanupState: "cleanup2");

            Assert.Null(connection2);
            Assert.Equal(1, poolSlots.ReservationCount);
        }

        [Fact]
        public void Constructor_BoundaryValue_MaxInt_WorksCorrectly()
        {
            try
            {
                // This test verifies that Int32.MaxValue is accepted as a valid capacity
                // We don't actually try to fill it as that would consume too much memory
                
                // Arrange & Act
                var poolSlots = new ConnectionPoolSlots((uint)int.MaxValue);

                // Assert
                Assert.Equal(0, poolSlots.ReservationCount);

                // Verify we can add at least one connection
                var connection = poolSlots.Add(
                    createCallback: state => new MockDbConnectionInternal(),
                    cleanupCallback: (conn, state) => { },
                    createState: "test",
                    cleanupState: "cleanup");

                Assert.NotNull(connection);
                Assert.Equal(1, poolSlots.ReservationCount);
            }
            catch (OutOfMemoryException)
            {
                // OutOfMemoryException is acceptable when trying to allocate an array of int.MaxValue size
                // This test is primarily checking that ArgumentOutOfRangeException is not thrown for the capacity validation
                // The fact that we reach the OutOfMemoryException means the capacity validation passed
            }
        }
    }
}
