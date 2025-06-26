// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    internal class BoundedConcurrentDictionary<T>
    {
        private readonly ConcurrentDictionary<T, byte> items;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _capacity;

        public BoundedConcurrentDictionary(int capacity)
        {
            _capacity = capacity;
            _semaphore = new(_capacity);
            items = new ConcurrentDictionary<T, byte>();
        }

        public bool TryReserve() => _semaphore.Wait(0);

        public void ReleaseReservation() => _semaphore.Release();

        public bool TryAdd(T item) => items.TryAdd(item, 0);

        public bool TryRemove(T item)
        {
            if (items.TryRemove(item, out _))
            {
                ReleaseReservation();
                return true;
            }
            return false;
        }

        public int Count => _capacity - _semaphore.CurrentCount;
    }
}
