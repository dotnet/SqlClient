// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


#if NET8_0_OR_GREATER
using System;
using System.Collections;
using System.Collections.Frozen;
#endif
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Microsoft.Data.Common;

namespace Microsoft.Data.ProviderBase
{
    internal sealed class FieldNameLookup
#if NET8_0_OR_GREATER
        : IEnumerable<KeyValuePair<string,int>>, IEnumerator<KeyValuePair<string,int>>
#endif
    {
        private readonly string[] _fieldNames;
        private readonly int _defaultLocaleID;
#if NET8_0_OR_GREATER
        private int _enumeratorIndex;
#endif

        private IDictionary<string, int> _fieldNameLookup;
        private CompareInfo _compareInfo;

        public FieldNameLookup(string[] fieldNames, int defaultLocaleID)
        {
            _defaultLocaleID = defaultLocaleID;
            if (fieldNames == null)
            {
                throw ADP.ArgumentNull(nameof(fieldNames));
            }
            _fieldNames = fieldNames;
        }

        public FieldNameLookup(IDataReader reader, int defaultLocaleID)
        {
            _defaultLocaleID = defaultLocaleID;
            string[] fieldNames = new string[reader.FieldCount];
            for (int i = 0; i < fieldNames.Length; ++i)
            {
                fieldNames[i] = reader.GetName(i);
            }
            _fieldNames = fieldNames;
        }

        public int GetOrdinal(string fieldName)
        {
            if (fieldName == null)
            {
                throw ADP.ArgumentNull(nameof(fieldName));
            }
            int index = IndexOf(fieldName);
            if (index == -1)
            {
                throw ADP.IndexOutOfRange(fieldName);
            }
            return index;
        }

        private int IndexOf(string fieldName)
        {
            if (_fieldNameLookup == null)
            {
                GenerateLookup();
            }
            if (!_fieldNameLookup.TryGetValue(fieldName, out int index))
            {
                index = LinearIndexOf(fieldName, CompareOptions.IgnoreCase);
                if (index == -1)
                {
                    // do the slow search now (kana, width insensitive comparison)
                    index = LinearIndexOf(fieldName, ADP.DefaultCompareOptions);
                }
            }

            return index;
        }

        private CompareInfo GetCompareInfo()
        {
            if (_defaultLocaleID != -1)
            {
                return CompareInfo.GetCompareInfo(_defaultLocaleID);
            }
            return CultureInfo.InvariantCulture.CompareInfo;
        }

        private int LinearIndexOf(string fieldName, CompareOptions compareOptions)
        {
            if (_compareInfo == null)
            {
                _compareInfo = GetCompareInfo();
            }

#if NET8_0_OR_GREATER
            // if we have failed a lookup in the frozen dictionary then we're going
            // to have to modify the dictionary as we do comparison sensitive lookups
            // and since we can't modify a frozen dictionary we need to revert to a
            // standard mutable dictionary
            if (_fieldNameLookup is FrozenDictionary<string,int> frozen)
            {
                _fieldNameLookup = new Dictionary<string,int>(frozen);
            }
#endif

            for (int index = 0; index < _fieldNames.Length; index++)
            {
                if (_compareInfo.Compare(fieldName, _fieldNames[index], compareOptions) == 0)
                {
                    _fieldNameLookup[fieldName] = index;
                    return index;
                }
            }
            return -1;
        }

        private void GenerateLookup()
        {
#if NET8_0_OR_GREATER
            _fieldNameLookup = this.ToFrozenDictionary();
#else

            int length = _fieldNames.Length;
            Dictionary<string, int> lookup = new Dictionary<string, int>(length);
            // walk the field names from the end to the beginning so that if a name exists
            // multiple times the first (from beginning to end) index of it is stored
            // in the hash table
            for (int index = length - 1; 0 <= index; --index)
            {
                string fieldName = _fieldNames[index];
                lookup[fieldName] = index;
            }
            _fieldNameLookup = lookup;
#endif
        }

#if NET8_0_OR_GREATER
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            Reset();
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public KeyValuePair<string, int> Current
        {
            get
            {
                if (_enumeratorIndex == -2)
                {
                    throw new ObjectDisposedException(nameof(FieldNameLookup));
                }
                int index = _fieldNames.Length - (_enumeratorIndex + 1);
                return new KeyValuePair<string,int>(_fieldNames[index], index);
            }
        }
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_enumeratorIndex == -2)
            {
                throw new ObjectDisposedException(nameof(FieldNameLookup));
            }
            if (_enumeratorIndex == -1)
            {
                _enumeratorIndex = 0;
                return true;
            }
            _enumeratorIndex += 1;
            return _enumeratorIndex < _fieldNames.Length;
        }

        public void Reset()
        {
            _enumeratorIndex = -1;
        }

        public void Dispose()
        {
            _enumeratorIndex = -2;
        }
#endif
    }
}
