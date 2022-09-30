// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlEnvChange
    {
        internal byte _type;
        internal byte _oldLength;
        internal int _newLength; // 7206 TDS changes makes this length an int
        internal int _length;
        internal string _newValue;
        internal string _oldValue;
        /// <summary>
        /// contains binary data, before using this field check newBinRented to see if you can take the field array or whether you should allocate and copy
        /// </summary>
        internal byte[] _newBinValue;
        /// <summary>
        /// contains binary data, before using this field check newBinRented to see if you can take the field array or whether you should allocate and copy
        /// </summary>
        internal byte[] _oldBinValue;
        internal long _newLongValue;
        internal long _oldLongValue;
        internal SqlCollation _newCollation;
        internal SqlCollation _oldCollation;
        internal RoutingInfo _newRoutingInfo;
        internal bool _newBinRented;
        internal bool _oldBinRented;

        internal SqlEnvChange _next;

        internal void Clear()
        {
            _type = 0;
            _oldLength = 0;
            _newLength = 0;
            _length = 0;
            _newValue = null;
            _oldValue = null;
            if (_newBinValue != null)
            {
                Array.Clear(_newBinValue, 0, _newBinValue.Length);
                if (_newBinRented)
                {
                    ArrayPool<byte>.Shared.Return(_newBinValue);
                }
                _newBinValue = null;
            }
            if (_oldBinValue != null)
            {
                Array.Clear(_oldBinValue, 0, _oldBinValue.Length);
                if (_oldBinRented)
                {
                    ArrayPool<byte>.Shared.Return(_oldBinValue);
                }
                _oldBinValue = null;
            }
            _newBinRented = false;
            _oldBinRented = false;
            _newLongValue = 0;
            _oldLongValue = 0;
            _newCollation = null;
            _oldCollation = null;
            _newRoutingInfo = null;
            _next = null;
        }
    }
}
