// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Parser;

internal sealed class _SqlMetaDataSetCollection
{
    private readonly List<_SqlMetaDataSet> _altMetaDataSetArray;
    internal _SqlMetaDataSet metaDataSet;

    internal _SqlMetaDataSetCollection()
    {
        _altMetaDataSetArray = new List<_SqlMetaDataSet>();
    }

    internal void SetAltMetaData(_SqlMetaDataSet altMetaDataSet)
    {
        // If altmetadata with same id is found, override it rather than adding a new one
        int newId = altMetaDataSet.id;
        for (int i = 0; i < _altMetaDataSetArray.Count; i++)
        {
            if (_altMetaDataSetArray[i].id == newId)
            {
                // override the existing metadata with the same id
                _altMetaDataSetArray[i] = altMetaDataSet;
                return;
            }
        }

        // if we did not find metadata to override, add as new
        _altMetaDataSetArray.Add(altMetaDataSet);
    }

    internal _SqlMetaDataSet GetAltMetaData(int id)
    {
        foreach (_SqlMetaDataSet altMetaDataSet in _altMetaDataSetArray)
        {
            if (altMetaDataSet.id == id)
            {
                return altMetaDataSet;
            }
        }
        Debug.Fail("Can't match up altMetaDataSet with given id");
        return null;
    }

    public object Clone()
    {
        _SqlMetaDataSetCollection result = new _SqlMetaDataSetCollection() { metaDataSet = metaDataSet?.Clone() };

        foreach (_SqlMetaDataSet set in _altMetaDataSetArray)
        {
            result._altMetaDataSetArray.Add(set.Clone());
        }
        return result;
    }
}
