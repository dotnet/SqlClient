// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Data.SqlClient.Tests.Common
{
    /// <summary>
    /// Utility class that enables disposal of a collection of <see cref="IDisposable"/> objects
    /// with a single <c>using</c> statement.
    /// </summary>
    /// <typeparam name="T">Type of the elements contained within.</typeparam>
    public class DisposableArray<T> : IDisposable, IEnumerable<T>
        where T : IDisposable?
    {
        private readonly T[] _elements;

        /// <summary>
        /// Constructs a new instance with <paramref name="size"/> elements.
        /// </summary>
        /// <remarks>
        /// Remember when using this constructor that the underlying array will be initialized to
        /// <c>default(T)</c>. If <typeparamref name="T"/> is a reference type, this will be
        /// <c>null</c> - even if <typeparamref name="T"/> is not nullable!
        /// </remarks>
        /// <param name="size">Number of elements the new instance will contain.</param>
        public DisposableArray(int size)
        {
            _elements = new T[size];
        }

        /// <summary>
        /// Constructs a new instance from an existing array of elements.
        /// </summary>
        /// <param name="elements">Array of elements to store within the current instance.</param>
        public DisposableArray(T[] elements)
        {
            _elements = elements;
        }

        /// <summary>
        /// Gets or sets the element at index <see cref="i"/>.
        /// </summary>
        /// <param name="i">The element to get/set will be at this position in the array</param>
        public T this[int i]
        {
            get => _elements[i];
            set => _elements[i] = value;
        }

        /// <summary>
        /// Gets the number of elements in the array.
        /// </summary>
        public int Length => _elements.Length;

        /// <summary>
        /// Disposes all elements in the current instance. Each element will be checked for
        /// <c>null</c> before disposing it.
        /// </summary>
        public void Dispose()
        {
            foreach (T element in _elements)
            {
                element?.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator() =>
            ((IEnumerable<T>)_elements).GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() =>
            _elements.GetEnumerator();
    }
}
