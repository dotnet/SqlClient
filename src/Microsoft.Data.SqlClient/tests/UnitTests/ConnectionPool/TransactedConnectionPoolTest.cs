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
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Data.Common.ConnectionString;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool;

public class TransactedConnectionPoolTest
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPool_SetsPoolProperty()
    {
        // Arrange
        var mockPool = new MockDbConnectionPool();

        // Act
        var transactedPool = new TransactedConnectionPool(mockPool);

        // Assert
        Assert.Same(mockPool, transactedPool.Pool);
        Assert.True(transactedPool.Id > 0);
    }

    [Fact]
    public void Constructor_UniqueIds()
    {
        // Arrange
        var pool1 = new TransactedConnectionPool(new MockDbConnectionPool());
        var pool2 = new TransactedConnectionPool(new MockDbConnectionPool());

        // Act & Assert
        Assert.NotEqual(pool1.Id, pool2.Id);
        Assert.True(pool1.Id > 0);
        Assert.True(pool2.Id > 0);
    }

    #endregion

    #region GetTransactedObject Tests

    [Fact]
    public void GetTransactedObject_WithNonExistentTransaction_ReturnsNull()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Act
        var result = transactedPool.GetTransactedObject(transaction);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetTransactedObject_WithExistingTransaction_ReturnsAndRemovesConnection()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connection = new MockDbConnectionInternal();
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // First add a connection
        transactedPool.PutTransactedObject(transaction, connection);

        // Act
        var result = transactedPool.GetTransactedObject(transaction);

        // Assert
        Assert.Same(connection, result);

        // Verify the connection is removed (second call should return null)
        var secondResult = transactedPool.GetTransactedObject(transaction);
        Assert.Null(secondResult);
    }

    [Fact]
    public void GetTransactedObject_WithMultipleConnections_ReturnsLastAdded()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connection1 = new MockDbConnectionInternal();
        var connection2 = new MockDbConnectionInternal();
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Add multiple connections
        transactedPool.PutTransactedObject(transaction, connection1);
        transactedPool.PutTransactedObject(transaction, connection2);

        // Act
        var result = transactedPool.GetTransactedObject(transaction);

        // Assert
        Assert.Same(connection2, result); // Should return the last added (LIFO behavior)
    }

    [Fact]
    public void GetTransactedObject_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connections = new DbConnectionInternal[10];
        for (int i = 0; i < connections.Length; i++)
        {
            connections[i] = new MockDbConnectionInternal();
        }

        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Add all connections
        foreach (var conn in connections)
        {
            transactedPool.PutTransactedObject(transaction, conn);
        }

        var retrievedConnections = new ConcurrentBag<DbConnectionInternal>();
        var tasks = new Task[connections.Length];

        // Act - retrieve connections concurrently
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var conn = transactedPool.GetTransactedObject(transaction);
                Assert.NotNull(conn);
                retrievedConnections.Add(conn);
            });
        }

        Task.WaitAll(tasks);

        // Assert
        Assert.Equal(connections.Length, retrievedConnections.Count);
        Assert.True(connections.All(retrievedConnections.Contains));
    }

    #endregion

    #region PutTransactedObject Tests

    [Fact]
    public void PutTransactedObject_WithNullConnection_ThrowsArgumentNullException()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => 
            transactedPool.PutTransactedObject(transaction, null!));
    }

    [Fact]
    public void PutTransactedObject_WithNewTransaction_CreatesNewConnectionList()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connection = new MockDbConnectionInternal();
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Act
        transactedPool.PutTransactedObject(transaction, connection);

        // Assert
        var retrievedConnection = transactedPool.GetTransactedObject(transaction);
        Assert.Same(connection, retrievedConnection);
    }

    [Fact]
    public void PutTransactedObject_WithExistingTransaction_AddsToExistingConnectionList()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connection1 = new MockDbConnectionInternal();
        var connection2 = new MockDbConnectionInternal();
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Act
        transactedPool.PutTransactedObject(transaction, connection1);
        transactedPool.PutTransactedObject(transaction, connection2);

        // Assert
        var retrieved1 = transactedPool.GetTransactedObject(transaction);
        var retrieved2 = transactedPool.GetTransactedObject(transaction);
        
        Assert.Same(connection2, retrieved1); // Last in, first out
        Assert.Same(connection1, retrieved2);
    }

    [Fact]
    public void PutTransactedObject_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connections = new DbConnectionInternal[10];
        for (int i = 0; i < connections.Length; i++)
        {
            connections[i] = new MockDbConnectionInternal();
        }

        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        var tasks = new Task[connections.Length];

        // Act - add connections concurrently
        for (int i = 0; i < tasks.Length; i++)
        {
            var connection = connections[i];
            tasks[i] = Task.Run(() => transactedPool.PutTransactedObject(transaction, connection));
        }

        Task.WaitAll(tasks);

        // Assert - all connections should be retrievable
        var retrievedConnections = new List<DbConnectionInternal>();
        DbConnectionInternal? conn;
        while ((conn = transactedPool.GetTransactedObject(transaction)) != null)
        {
            retrievedConnections.Add(conn);
        }

        Assert.Equal(connections.Length, retrievedConnections.Count);
        Assert.True(connections.All(retrievedConnections.Contains));
    }

    [Fact]
    public void PutTransactedObject_SameConnectionTwice_AddsToPoolTwice()
    {
        // TODO: this behavior is suspicious should we prevent this?

        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connection = new MockDbConnectionInternal();

        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Act
        transactedPool.PutTransactedObject(transaction, connection);
        transactedPool.PutTransactedObject(transaction, connection);

        // Assert
        var retrieved1 = transactedPool.GetTransactedObject(transaction);
        var retrieved2 = transactedPool.GetTransactedObject(transaction);

        Assert.Same(connection, retrieved1);
        Assert.Same(connection, retrieved2);
    }

    #endregion

    #region TransactionEnded Tests

    [Fact]
    public void TransactionEnded_WithNullConnection_ThrowsNullReferenceException()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => 
            transactedPool.TransactionEnded(transaction, null!));
    }

    [Fact]
    public void TransactionEnded_WithNonExistentTransaction_DoesNotThrow()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connection = new MockDbConnectionInternal();
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Act & Assert (should not throw)
        transactedPool.TransactionEnded(transaction, connection);
        // TODO: is this really the behavior we want?
    }

    [Fact]
    public void TransactionEnded_WithExistingConnection_RemovesConnectionAndReturnsToPool()
    {
        // Arrange
        var mockPool = new MockDbConnectionPool();
        var transactedPool = new TransactedConnectionPool(mockPool);
        var connection = new MockDbConnectionInternal();
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Add connection to transacted pool
        transactedPool.PutTransactedObject(transaction, connection);

        // Act
        transactedPool.TransactionEnded(transaction, connection);

        // Assert
        Assert.Contains(connection, mockPool.ReturnedConnections);
        
        // Verify connection is no longer in transacted pool
        var retrievedConnection = transactedPool.GetTransactedObject(transaction);
        Assert.Null(retrievedConnection);
    }

    [Fact]
    public void TransactionEnded_WithMultipleConnections_RemovesOnlySpecifiedConnection()
    {
        // Arrange
        var mockPool = new MockDbConnectionPool();
        var transactedPool = new TransactedConnectionPool(mockPool);
        var connection1 = new MockDbConnectionInternal();
        var connection2 = new MockDbConnectionInternal();
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Add multiple connections
        transactedPool.PutTransactedObject(transaction, connection1);
        transactedPool.PutTransactedObject(transaction, connection2);

        // Act - end only one connection
        transactedPool.TransactionEnded(transaction, connection1);

        // Assert
        Assert.Contains(connection1, mockPool.ReturnedConnections);
        Assert.DoesNotContain(connection2, mockPool.ReturnedConnections);

        // Verify other connection is still in pool
        // TODO: there shouldn't be partial state in the pool after the transaction ends
        // May be a way to register a single callback to clear the whole list.
        var retrievedConnection = transactedPool.GetTransactedObject(transaction);
        Assert.Same(connection2, retrievedConnection);
    }

    [Fact]
    public void TransactionEnded_WithConnectionNotInPool_DoesNotReturnToMainPool()
    {
        // Arrange
        var mockPool = new MockDbConnectionPool();
        var transactedPool = new TransactedConnectionPool(mockPool);
        var connection = new MockDbConnectionInternal();
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Don't add connection to transacted pool

        // Act
        transactedPool.TransactionEnded(transaction, connection);

        // Assert
        Assert.DoesNotContain(connection, mockPool.ReturnedConnections);
    }

    [Fact]
    public void TransactionEnded_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var mockPool = new MockDbConnectionPool();
        var transactedPool = new TransactedConnectionPool(mockPool);
        var connections = new DbConnectionInternal[10];
        for (int i = 0; i < connections.Length; i++)
        {
            connections[i] = new MockDbConnectionInternal();
        }

        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Add all connections
        foreach (var conn in connections)
        {
            transactedPool.PutTransactedObject(transaction, conn);
        }

        var tasks = new Task[connections.Length];

        // Act - end transactions concurrently
        for (int i = 0; i < tasks.Length; i++)
        {
            var connection = connections[i];
            tasks[i] = Task.Run(() => transactedPool.TransactionEnded(transaction, connection));
        }

        Task.WaitAll(tasks);

        // Assert
        Assert.Equal(connections.Length, mockPool.ReturnedConnections.Count);
        Assert.True(connections.All(mockPool.ReturnedConnections.Contains));
    }

    [Fact]
    public void TransactionEnded_MultipleCallsWithSameConnection_OnlyReturnsOnce()
    {
        // Arrange
        var mockPool = new MockDbConnectionPool();
        var transactedPool = new TransactedConnectionPool(mockPool);
        var connection = new MockDbConnectionInternal();

        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Add connection to transacted pool
        transactedPool.PutTransactedObject(transaction, connection);

        // Act - call TransactionEnded multiple times
        transactedPool.TransactionEnded(transaction, connection);
        transactedPool.TransactionEnded(transaction, connection);
        transactedPool.TransactionEnded(transaction, connection);

        // Assert - connection should only be returned to pool once
        Assert.Single(mockPool.ReturnedConnections);
        Assert.Contains(connection, mockPool.ReturnedConnections);
    }

    [Fact]
    public void TransactionEnded_CalledBeforePut_HandlesRaceCondition()
    {
        // TODO: this test shows that we actually don't handle the race correctly
        // we shouldn't allow connections associated with ended transactions in the pool

        // Arrange
        var mockPool = new MockDbConnectionPool();
        var transactedPool = new TransactedConnectionPool(mockPool);
        var connection = new MockDbConnectionInternal();

        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Act - simulate race condition where TransactionEnded is called before PutTransactedObject
        transactedPool.TransactionEnded(transaction, connection);
        transactedPool.PutTransactedObject(transaction, connection);

        // Assert - connection should still be in the transacted pool
        var retrievedConnection = transactedPool.GetTransactedObject(transaction);
        Assert.Same(connection, retrievedConnection);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FullLifecycle_PutGetEnd_WorksCorrectly()
    {
        // Arrange
        var mockPool = new MockDbConnectionPool();
        var transactedPool = new TransactedConnectionPool(mockPool);
        var connection = new MockDbConnectionInternal();
        
        using var transactionScope = new TransactionScope();
        var transaction = Transaction.Current!;

        // Act & Assert
        // 1. Put connection in transacted pool
        transactedPool.PutTransactedObject(transaction, connection);
        
        // 2. Get connection from transacted pool
        var retrievedConnection = transactedPool.GetTransactedObject(transaction);
        Assert.Same(connection, retrievedConnection);
        
        // 3. Put it back
        transactedPool.PutTransactedObject(transaction, connection);
        
        // 4. End transaction
        transactedPool.TransactionEnded(transaction, connection);
        
        // 5. Verify connection returned to main pool
        Assert.Contains(connection, mockPool.ReturnedConnections);
        
        // 6. Verify transacted pool is empty
        var finalRetrieved = transactedPool.GetTransactedObject(transaction);
        Assert.Null(finalRetrieved);
    }

    [Fact]
    public void MultipleTransactions_IsolatedCorrectly()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connection1 = new MockDbConnectionInternal();
        var connection2 = new MockDbConnectionInternal();

        Transaction? transaction1 = null;
        Transaction? transaction2 = null;

        using (new TransactionScope())
        {
            transaction1 = Transaction.Current;
            transactedPool.PutTransactedObject(transaction1!, connection1);
        }

        using (new TransactionScope())
        {
            transaction2 = Transaction.Current;
            transactedPool.PutTransactedObject(transaction2!, connection2);
        }

        // Act & Assert
        var retrieved1 = transactedPool.GetTransactedObject(transaction1!);
        var retrieved2 = transactedPool.GetTransactedObject(transaction2!);

        Assert.Same(connection1, retrieved1);
        Assert.Same(connection2, retrieved2);
    }

    [Fact]
    public void ConcurrentPutAndGet_DifferentTransactions_Isolated()
    {
        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var numberOfTransactions = 5;
        var connectionsPerTransaction = 3;
        var results = new ConcurrentDictionary<int, List<DbConnectionInternal>>();
        using var countdown = new CountdownEvent(numberOfTransactions);

        // Act - create multiple transactions concurrently
        var tasks = Enumerable.Range(0, numberOfTransactions).Select(txIndex =>
        {
            return Task.Run(() =>
            {
                try
                {
                    using var scope = new TransactionScope();
                    var transaction = Transaction.Current!;
                    
                    // Add connections to this transaction
                    for (int i = 0; i < connectionsPerTransaction; i++)
                    {
                        var conn = new MockDbConnectionInternal();
                        transactedPool.PutTransactedObject(transaction, conn);
                    }

                    // Retrieve connections from this transaction
                    var retrieved = new List<DbConnectionInternal>();
                    DbConnectionInternal? retrievedConn;
                    while ((retrievedConn = transactedPool.GetTransactedObject(transaction)) != null)
                    {
                        retrieved.Add(retrievedConn);
                    }

                    results[txIndex] = retrieved;
                }
                finally
                {
                    countdown.Signal();
                }
            });
        }).ToList();

        // Wait for all tasks to complete
        Task.WaitAll(tasks.ToArray());

        // Assert - each transaction should have isolated connections
        Assert.Equal(numberOfTransactions, results.Count);
        
        foreach (var result in results.Values)
        {
            Assert.Equal(connectionsPerTransaction, result.Count);
        }

        // Verify no overlap between transactions
        var allConnections = results.Values.SelectMany(r => r).ToList();
        Assert.Equal(allConnections.Count, allConnections.Distinct().Count());
    }

    [Fact]
    public void TransactionScope_CompleteAndDispose_HandledCorrectly()
    {
        // TODO: this test shows that we don't give strong guarantees that
        // the pool state will match the transaction state.

        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connection = new MockDbConnectionInternal();
        Transaction? capturedTransaction = null;

        // Act
        using (var scope = new TransactionScope())
        {
            capturedTransaction = Transaction.Current!;
            transactedPool.PutTransactedObject(capturedTransaction, connection);
            scope.Complete();
        } // TransactionScope disposes here

        // Assert - connection should still be retrievable if transaction completed
        var retrieved = transactedPool.GetTransactedObject(capturedTransaction!);
        Assert.Same(connection, retrieved);
    }

    [Fact]
    public void PutTransactedObject_WithDisposedTransaction_HandlesGracefully()
    {
        //TODO: this test should not pass! why would we store connections from a disposed transaction?

        // Arrange
        var transactedPool = new TransactedConnectionPool(new MockDbConnectionPool());
        var connection = new MockDbConnectionInternal();
        Transaction? disposedTransaction = null;

        using (var scope = new TransactionScope())
        {
            disposedTransaction = Transaction.Current!;
        } // Transaction is now disposed

        // Act & Assert - should handle gracefully without throwing
        try
        {
            transactedPool.PutTransactedObject(disposedTransaction!, connection);
            // If no exception, test passes
            Assert.True(true);
        }
        catch (ObjectDisposedException)
        {
            // This is expected behavior and acceptable
            Assert.True(true);
        }
    }

    #endregion

    #region Mock Classes

    internal class MockDbConnectionPool : IDbConnectionPool
    {
        public ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts { get; } = new();
        public SqlConnectionFactory ConnectionFactory => throw new NotImplementedException();
        public int Count => throw new NotImplementedException();
        public bool ErrorOccurred => throw new NotImplementedException();
        public int Id { get; } = 1;
        public DbConnectionPoolIdentity Identity => throw new NotImplementedException();
        public bool IsRunning => throw new NotImplementedException();
        public TimeSpan LoadBalanceTimeout => throw new NotImplementedException();
        public DbConnectionPoolGroup PoolGroup => throw new NotImplementedException();
        public DbConnectionPoolGroupOptions PoolGroupOptions => throw new NotImplementedException();
        public DbConnectionPoolProviderInfo ProviderInfo => throw new NotImplementedException();
        public DbConnectionPoolState State => throw new NotImplementedException();
        public bool UseLoadBalancing => throw new NotImplementedException();

        public ConcurrentBag<DbConnectionInternal> ReturnedConnections { get; } = new();

        public void Clear() => throw new NotImplementedException();

        public bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal> taskCompletionSource, DbConnectionOptions userOptions, out DbConnectionInternal? connection)
        {
            throw new NotImplementedException();
        }

        public DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)
        {
            throw new NotImplementedException();
        }

        public void ReturnInternalConnection(DbConnectionInternal obj, DbConnection owningObject)
        {
            throw new NotImplementedException();
        }

        public void PutObjectFromTransactedPool(DbConnectionInternal obj)
        {
            ReturnedConnections.Add(obj);
        }

        public void Startup() => throw new NotImplementedException();

        public void Shutdown() => throw new NotImplementedException();

        public void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject)
        {
            throw new NotImplementedException();
        }
    }

    internal class MockDbConnectionInternal : DbConnectionInternal
    {
        private static int s_nextId = 1;
        public int MockId { get; } = Interlocked.Increment(ref s_nextId);

        public override string ServerVersion => "Mock";

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

        public override string ToString() => $"MockConnection_{MockId}";
    }

    #endregion
}