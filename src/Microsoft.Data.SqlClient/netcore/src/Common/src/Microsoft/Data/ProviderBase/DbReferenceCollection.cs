// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Data.ProviderBase
{
    internal abstract class DbReferenceCollection
    {
        private struct CollectionEntry
        {
            private int _tag;              // information about the reference
            private WeakReference<object> _weak;   // the reference itself.

            public void NewTarget(int tag, object target)
            {
                Debug.Assert(!TryGetTarget(out object _) , "Entry already has a valid target");
                Debug.Assert(tag != 0, "Bad tag");
                Debug.Assert(target != null, "Invalid target");

                if (_weak == null)
                {
                    _weak = new WeakReference<object>(target, false);
                }
                else
                {
                    _weak.SetTarget(target);
                }
                _tag = tag;
            }

            public void RemoveTarget()
            {
                _tag = 0;
                _weak.SetTarget(null);
            }

            public int Tag => _tag;

            public bool TryGetTarget(out object target)
            {
                target = null;
                return _tag != 0 && _weak.TryGetTarget(out target);
            }
        }

        private const int LockPollTime = 100;   // Time to wait (in ms) between attempting to get the _itemLock
        private const int DefaultCollectionSize = 20;   // Default size for the collection, and the amount to grow every time the collection is full
        private CollectionEntry[] _items;       // The collection of items we are keeping track of
        private readonly object _itemLock;      // Used to synchronize access to the _items collection
        private int _optimisticCount;           // (#ItemsAdded - #ItemsRemoved) - This estimates the number of items that we *should* have (but doesn't take into account item targets being GC'd)
        private int _lastItemIndex;             // Location of the last item in _items
        private volatile bool _isNotifying;     // Indicates that the collection is currently being notified (and, therefore, about to be cleared)

        protected DbReferenceCollection()
        {
            _items = new CollectionEntry[DefaultCollectionSize];
            _itemLock = new object();
            _optimisticCount = 0;
            _lastItemIndex = 0;
        }

        abstract public void Add(object value, int tag);

        protected void AddItem(object value, int tag)
        {
            Debug.Assert(null != value && 0 != tag, "AddItem with null value or 0 tag");
            bool itemAdded = false;

            lock (_itemLock)
            {
                // Try to find a free spot
                for (int i = 0; i <= _lastItemIndex; ++i)
                {
                    if (_items[i].Tag == 0)
                    {
                        _items[i].NewTarget(tag, value);
                        Debug.Assert(_items[i].TryGetTarget(out object _), "missing expected target");
                        itemAdded = true;
                        break;
                    }
                }

                // No free spots, can we just add on to the end?
                if ((!itemAdded) && (_lastItemIndex + 1 < _items.Length))
                {
                    _lastItemIndex++;
                    _items[_lastItemIndex].NewTarget(tag, value);
                    itemAdded = true;
                }

                // If no free spots and no space at the end, try to find a dead item
                if (!itemAdded)
                {
                    for (int i = 0; i <= _lastItemIndex; ++i)
                    {
                        if (!_items[i].TryGetTarget(out object _))
                        {
                            _items[i].NewTarget(tag, value);
                            Debug.Assert(_items[i].TryGetTarget(out object _), "missing expected target");
                            itemAdded = true;
                            break;
                        }
                    }
                }

                // If nothing was free, then resize and add to the end
                if (!itemAdded)
                {
                    Array.Resize<CollectionEntry>(ref _items, _items.Length * 2);
                    _lastItemIndex++;
                    _items[_lastItemIndex].NewTarget(tag, value);
                }

                _optimisticCount++;
            }
        }

        internal T FindItem<T>(int tag, Func<T, bool> filterMethod) where T : class
        {
            bool lockObtained = false;
            try
            {
                TryEnterItemLock(ref lockObtained);
                if (lockObtained)
                {
                    if (_optimisticCount > 0)
                    {
                        for (int counter = 0; counter <= _lastItemIndex; counter++)
                        {
                            // Check tag (should be easiest and quickest)
                            if (_items[counter].Tag == tag)
                            {
                                if (_items[counter].TryGetTarget(out object value))
                                {
                                    // Make sure the item has the correct type and passes the filtering
                                    if (value is T tempItem && filterMethod(tempItem))
                                    {
                                        return tempItem;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                ExitItemLockIfNeeded(lockObtained);
            }

            // If we got to here, then no item was found, so return null
            return null;
        }

        public void Notify(int message)
        {
            bool lockObtained = false;
            try
            {
                TryEnterItemLock(ref lockObtained);
                if (lockObtained)
                {
                    try
                    {
                        _isNotifying = true;

                        // Loop through each live item and notify it
                        if (_optimisticCount > 0)
                        {
                            for (int index = 0; index <= _lastItemIndex; ++index)
                            {
                                if (_items[index].TryGetTarget(out object value))
                                {
                                    NotifyItem(message, _items[index].Tag, value);
                                    _items[index].RemoveTarget();
                                }
                                Debug.Assert(!_items[index].TryGetTarget(out object _), "Unexpected target after notifying");
                            }
                            _optimisticCount = 0;
                        }

                        // Shrink collection (if needed)
                        if (_items.Length > 100)
                        {
                            _lastItemIndex = 0;
                            _items = new CollectionEntry[DefaultCollectionSize];
                        }
                    }
                    finally
                    {
                        _isNotifying = false;
                    }
                }
            }
            finally
            {
                ExitItemLockIfNeeded(lockObtained);
            }
        }

        abstract protected void NotifyItem(int message, int tag, object value);

        abstract public void Remove(object value);

        protected void RemoveItem(object value)
        {
            Debug.Assert(null != value, "RemoveItem with null");

            bool lockObtained = false;
            try
            {
                TryEnterItemLock(ref lockObtained);

                if (lockObtained)
                {
                    // Find the value, and then remove the target from our collection
                    if (_optimisticCount > 0)
                    {
                        for (int index = 0; index <= _lastItemIndex; ++index)
                        {
                            if (_items[index].TryGetTarget(out object target) && value == target)
                            {
                                _items[index].RemoveTarget();
                                _optimisticCount--;
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                ExitItemLockIfNeeded(lockObtained);
            }
        }

        // This is polling lock that will abandon getting the lock if _isNotifying is set to true
        private void TryEnterItemLock(ref bool lockObtained)
        {
            // Assume that we couldn't take the lock
            lockObtained = false;
            // Keep trying to take the lock until either we've taken it, or the collection is being notified
            while ((!_isNotifying) && (!lockObtained))
            {
                Monitor.TryEnter(_itemLock, LockPollTime, ref lockObtained);
            }
        }

        private void ExitItemLockIfNeeded(bool lockObtained)
        {
            if (lockObtained)
            {
                Monitor.Exit(_itemLock);
            }
        }
    }
}

