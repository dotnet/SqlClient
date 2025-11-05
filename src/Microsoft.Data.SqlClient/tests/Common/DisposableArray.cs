// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.Tests.Common
{
    public class DisposableArray<T> : IDisposable, IEnumerable<T>
        where T : IDisposable
    {
        private readonly T[] _elements;

        public T this[int i]
        {
            get => _elements[i];
            set => _elements[i] = value;
        }

        public int Length => _elements.Length;

        public DisposableArray(int size)
        {
            _elements = new T[size];
        }

        public DisposableArray(T[] elements)
        {
            _elements = elements;
        }

        public void Dispose()
        {
            foreach (T element in _elements)
            {
                element?.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        public IEnumerator<T> GetEnumerator() =>
            ((IEnumerable<T>)_elements).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            _elements.GetEnumerator();
    }
}
