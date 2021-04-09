// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Microsoft.Data.Common;

namespace Microsoft.Data.ProviderBase
{
    internal sealed class FieldNameLookup
    {
        private readonly string[] _fieldNames;
        private readonly int _defaultLocaleID;

        private Dictionary<string, int> _fieldNameLookup;
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
        }
    }
}
