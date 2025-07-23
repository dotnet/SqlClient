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
    /// A thread-safe collection with a fixed capacity. Avoids wasted work by reserving a slot before adding an item.
    /// </summary>
    internal sealed class ConnectionPoolSlots
    {
        /// <summary>
        /// Represents a reservation that manages a resource and ensures cleanup when no longer needed.
        /// </summary>
        /// <typeparam name="T">The type of the resource being managed by the reservation.</typeparam>
        private sealed class Reservation<T> : IDisposable
        {
            private Action<T> cleanupCallback;
            private T state;
            private bool _retain = false;
            private bool _disposed = false;

            internal Reservation(T state, Action<T> cleanupCallback)
            {
                this.state = state;
                this.cleanupCallback = cleanupCallback;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (!_retain)
                {
                    cleanupCallback(state);
                }
            }

            internal void Keep()
            {
                _retain = true;
            }
        }

        internal delegate T CreateCallback<T, S>(S state);
        internal delegate void CleanupCallback<T>(DbConnectionInternal? connection, T state);

        private readonly DbConnectionInternal?[] _connections;
        private readonly uint _capacity;
        private volatile int _reservations;

        /// <summary>
        /// Constructs a ConnectionPoolSlots instance with the given fixed capacity.
        /// </summary>
        /// <param name="fixedCapacity">The fixed capacity of the collection.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when fixedCapacity is greater than Int32.MaxValue or equal to zero.
        /// </exception>
        internal ConnectionPoolSlots(uint fixedCapacity)
        {
            if (fixedCapacity > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedCapacity), "Capacity must be less than or equal to Int32.MaxValue.");
            }

            if (fixedCapacity == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedCapacity), "Capacity must be greater than zero.");
            }

            _capacity = fixedCapacity;
            _reservations = 0;
            _connections = new DbConnectionInternal?[fixedCapacity];
        }

        /// <summary>
        /// Gets the total number of reservations currently held.
        /// </summary>
        internal int ReservationCount => _reservations;

        /// <summary>
        /// Adds a connection to the collection.
        /// </summary>
        /// <param name="createCallback">Callback that provides the connection to add to the collection. This callback 
        /// *must not* call any other ConnectionPoolSlots methods.</param>
        /// <param name="cleanupCallback">Callback to clean up resources if an exception occurs. This callback *must 
        /// not* call any other ConnectionPoolSlots methods. This callback *must not* throw exceptions.</param>
        /// <param name="createState">State made available to the create callback.</param>
        /// <param name="cleanupState">State made available to the cleanup callback.</param>
        /// <exception cref="Exception">
        /// Throws when createCallback throws an exception.
        /// Throws when a reservation is successfully made, but an empty slot cannot be found. This condition is 
        /// unexpected and indicates a bug.
        /// </exception>
        /// <returns>Returns the new connection, or null if there was not available space.</returns>
        internal DbConnectionInternal? Add<T, S>(
            CreateCallback<DbConnectionInternal?, T> createCallback, 
            CleanupCallback<S> cleanupCallback, 
            T createState,
            S cleanupState)
        {
            DbConnectionInternal? connection = null;
            try
            {
                using var reservation = TryReserve();
                if (reservation is null)
                {
                    return null;
                }

                connection = createCallback(createState);

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
                cleanupCallback(connection, cleanupState);
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
        /// <returns>A Reservation if successful, otherwise returns null.</returns>
        private Reservation<ConnectionPoolSlots>? TryReserve()
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

                return new Reservation<ConnectionPoolSlots>(this, (slots) => slots.ReleaseReservation());
            }
            return null;
        }
    }
}
