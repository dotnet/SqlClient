// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;

namespace Microsoft.Data.SqlClient
{
    internal ref struct DisposableTemporaryOnStack<T>
    where T : IDisposable
    {
        private T _value;
        private bool _hasValue;

        public void Set(T value)
        {
            _value = value;
            _hasValue = true;
        }

        public T Take()
        {
            T value = _value;
            _value = default;
            _hasValue = false;
            return value;
        }

        public void Dispose()
        {
            if (_hasValue)
            {
                _value.Dispose();
                _value = default;
                _hasValue = false;
            }
        }
    }
}
