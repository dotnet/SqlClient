// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Data.ProviderBase;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// A thread-safe array that allows reservations.
    /// A reservation *must* be made before adding a connection to the collection.
    /// Exceptions *must* be handled by the caller when trying to add connections
    /// and the caller *must* release the reservation.
    /// </summary>
    /// <example>
    /// <code>
    /// ConnectionPoolSlots slots = new ConnectionPoolSlots(100);
    /// 
    /// if (slots.TryReserve())
    /// {
    ///     try {
    ///         var connection = OpenConnection();
    ///         slots.Add(connection);
    ///     catch (InvalidOperationException ex)
    ///     {
    ///         slots.ReleaseReservation();
    ///         throw;
    ///     }
    /// }
    /// 
    /// if (slots.TryRemove())
    /// {
    ///     slots.ReleaseReservation();
    /// }
    /// 
    /// </code>
    /// </example>
    internal class ConnectionPoolSlots
    {
        private readonly DbConnectionInternal?[] _connections;
        private readonly int _capacity;
        private volatile int _reservations;

        /// <summary>
        /// Constructs a ConnectionPoolSlots instance with the given capacity.
        /// </summary>
        /// <param name="capacity">The capacity of the collection.</param>
        public ConnectionPoolSlots(int capacity)
        {
            _capacity = capacity;
            _reservations = 0;
            _connections = new DbConnectionInternal?[capacity];
        }

        /// <summary>
        /// Attempts to reserve a spot in the collection.
        /// </summary>
        /// <returns>True if a reservation was successfully obtained.</returns>
        public bool TryReserve()
        {
            for (var expected = _reservations; expected < _capacity; expected = _reservations)
            {
                // Try to reserve a spot in the collection by incrementing _reservations.
                // If _reservations changed underneath us, then another thread already reserved the spot we were trying to take.
                // Cycle back through the check above to reset expected and to make sure we don't go
                // over capacity.
                // Note that we purposefully don't use SpinWait for this: https://github.com/dotnet/coreclr/pull/21437
                if (Interlocked.CompareExchange(ref _reservations, expected + 1, expected) != expected)
                {
                    continue;
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Releases a reservation that was previously obtained.
        /// Must be called after removing an connection from the collection or if an exception occurs.
        /// </summary>
        public void ReleaseReservation()
        {
            Interlocked.Decrement(ref _reservations);
            Debug.Assert(_reservations >= 0, "Released a reservation that wasn't held");
        }

        /// <summary>
        /// Adds a connection to the collection. Can only be called after a reservation has been made.
        /// </summary>
        /// <param name="connection">The connection to add to the collection.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when unable to find an empty slot. 
        /// This can occur if a reservation is not taken before adding a connection.
        /// </exception>
        public void Add(DbConnectionInternal connection)
        {
            int i;
            for (i = 0; i < _capacity; i++)
            {
                if (Interlocked.CompareExchange(ref _connections[i], connection, null) == null)
                {
                    return;
                }
            }

            throw new InvalidOperationException("Couldn't find an empty slot.");
        }

        /// <summary>
        /// Removes a connection from the collection.
        /// </summary>
        /// <param name="connection">The connection to remove from the collection.</param>
        /// <returns>True if the connection was found and removed; otherwise, false.</returns>
        public bool TryRemove(DbConnectionInternal connection)
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                if (Interlocked.CompareExchange(ref _connections[i], null, connection) == connection)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the total number of reservations.
        /// </summary>
        public int ReservationCount => _reservations;
    }
}
