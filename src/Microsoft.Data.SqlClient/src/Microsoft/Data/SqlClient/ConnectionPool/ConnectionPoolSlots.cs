// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// A thread-safe collection with a fixed capacity that allows reservations.
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
    ///     }
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
    internal sealed class ConnectionPoolSlots
    {

        private sealed class Reservation : IDisposable
        {
            private readonly ConnectionPoolSlots _slots;
            private bool _retain = false;
            private bool _disposed = false;

            internal Reservation(ConnectionPoolSlots slots)
            {
                _slots = slots;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                if (!_retain)
                {
                    _slots.ReleaseReservation();
                    _disposed = true;
                }
            }

            internal void Keep()
            {
                _retain = true;
            }

        }

        internal delegate void CleanupCallback(DbConnectionInternal? connection);

        private readonly DbConnectionInternal?[] _connections;
        private readonly uint _capacity;
        private volatile int _reservations;

        /// <summary>
        /// Constructs a ConnectionPoolSlots instance with the given fixed capacity.
        /// </summary>
        /// <param name="fixedCapacity">The fixed capacity of the collection.</param>
        internal ConnectionPoolSlots(uint fixedCapacity)
        {
            _capacity = fixedCapacity;
            _reservations = 0;
            _connections = new DbConnectionInternal?[fixedCapacity];
        }

        /// <summary>
        /// Gets the total number of reservations.
        /// </summary>
        internal int ReservationCount => _reservations;

        /// <summary>
        /// Adds a connection to the collection. Can only be called after a reservation has been made.
        /// </summary>
        /// <param name="createCallback">The connection to add to the collection.</param>
        /// <param name="cleanupCallback">Callback to clean up resources if an exception occurs.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when unable to find an empty slot. 
        /// This can occur if a reservation is not taken before adding a connection.
        /// </exception>
        internal DbConnectionInternal? Add(Func<DbConnectionInternal?> createCallback, CleanupCallback cleanupCallback)
        {
            DbConnectionInternal? connection = null;
            try
            {
                using var reservation = TryReserve();
                if (reservation is null)
                {
                    return null;
                }

                connection = createCallback();

                if (connection is null)
                {
                    return null;
                }

                for (int i = 0; i < _capacity; i++)
                {
                    if (Interlocked.CompareExchange(ref _connections[i], connection, null) == null)
                    {
                        reservation.Keep();
                        return connection;
                    }
                }

                throw new InvalidOperationException("Couldn't find an empty slot.");
            }
            catch (Exception e)
            {
                cleanupCallback(connection);
                throw new Exception("Failed to create or add connection", e);
            }
        }

        /// <summary>
        /// Releases a reservation that was previously obtained.
        /// Must be called after removing a connection from the collection or if an exception occurs.
        /// </summary>
        private void ReleaseReservation()
        {
            Interlocked.Decrement(ref _reservations);
            Debug.Assert(_reservations >= 0, "Released a reservation that wasn't held");
        }

        /// <summary>
        /// Removes a connection from the collection.
        /// </summary>
        /// <param name="connection">The connection to remove from the collection.</param>
        /// <returns>True if the connection was found and removed; otherwise, false.</returns>
        internal bool TryRemove(DbConnectionInternal connection)
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                if (Interlocked.CompareExchange(ref _connections[i], null, connection) == connection)
                {
                    ReleaseReservation();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to reserve a spot in the collection.
        /// </summary>
        /// <returns>True if a reservation was successfully obtained.</returns>
        private Reservation? TryReserve()
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

                return new Reservation(this);
            }
            return null;
        }
    }
}
