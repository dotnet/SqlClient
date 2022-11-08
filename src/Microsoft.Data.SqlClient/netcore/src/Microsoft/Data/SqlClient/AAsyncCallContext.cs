// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    // this class is a base class for creating derived objects that will store state for async operations
    // avoiding the use of closures and allowing caching/reuse of the instances for frequently used async
    // calls
    //
    // DO derive from this and seal your class
    // DO add additional fields or properties needed for the async operation and then override Clear to zero them
    // DO override AfterClear and use the owner parameter to return the object to a cache location if you have one, this is the purpose of the method
    // CONSIDER creating your own Set method that calls the base Set rather than providing a parameterized ctor, it is friendlier to caching
    // DO NOT use this class' state after Dispose has been called. It will not throw ObjectDisposedException but it will be a cleared object

    internal abstract class AAsyncCallContext<TOwner, TTask, TDisposable> : AAsyncBaseCallContext<TOwner,TTask>
        where TOwner : class
        where TDisposable : IDisposable
    {
        protected TDisposable _disposable;

        protected AAsyncCallContext()
        {
        }

        protected AAsyncCallContext(TOwner owner, TaskCompletionSource<TTask> source, TDisposable disposable = default)
        {
            Set(owner, source, disposable);
        }

        protected void Set(TOwner owner, TaskCompletionSource<TTask> source, TDisposable disposable = default)
        {
            base.Set(owner, source);
            _disposable = disposable;
        }

        protected override void DisposeCore()
        {
            TDisposable copyDisposable = _disposable;
            _disposable = default;
            copyDisposable?.Dispose();
        }
    }

    internal abstract class AAsyncBaseCallContext<TOwner, TTask>
    {
        protected TOwner _owner;
        protected TaskCompletionSource<TTask> _source;
        protected bool _isDisposed;

        protected AAsyncBaseCallContext()
        {
        }

        protected void Set(TOwner owner, TaskCompletionSource<TTask> source)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _isDisposed = false;
        }

        protected void ClearCore()
        {
            _source = null;
            _owner = default;
            try
            {
                DisposeCore();
            }
            finally
            {
                _isDisposed = true;
            }
        }

        protected abstract void DisposeCore();

        /// <summary>
        /// override this method to cleanup instance data before ClearCore is called which will blank the base data
        /// </summary>
        protected virtual void Clear()
        {
        }

        /// <summary>
        /// override this method to do work after the instance has been totally blanked, intended for cache return etc
        /// </summary>
        protected virtual void AfterCleared(TOwner owner)
        {
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                TOwner owner = _owner;
                try
                {
                    Clear();
                }
                finally
                {
                    ClearCore();
                }
                AfterCleared(owner);
            }
        }
    }
}
