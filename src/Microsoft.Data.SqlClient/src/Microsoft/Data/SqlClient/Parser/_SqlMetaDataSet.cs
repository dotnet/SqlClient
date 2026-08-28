// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Parser;

internal sealed class _SqlMetaDataSet
{
    internal readonly SqlTceCipherInfoTable cekTable; // table of "column encryption keys" used for this metadataset
    internal ushort id;             // for altrow-columns only

    internal DataTable schemaTable;
    private readonly _SqlMetaData[] _metaDataArray;
    internal ReadOnlyCollection<DbColumn> dbColumnSchema;

    private int _hiddenColumnCount;
    private int[] _visibleColumnMap;

    internal _SqlMetaDataSet(int count)
    {
        _hiddenColumnCount = -1;
        _metaDataArray = new _SqlMetaData[count];
        for (int i = 0; i < _metaDataArray.Length; ++i)
        {
            _metaDataArray[i] = new _SqlMetaData(i);
        }
    }

    internal _SqlMetaDataSet(int count, SqlTceCipherInfoTable cipherTable)
        : this(count)
    {
        cekTable = cipherTable;
    }

    private _SqlMetaDataSet(_SqlMetaDataSet original)
    {
        id = original.id;
        _hiddenColumnCount = original._hiddenColumnCount;
        _visibleColumnMap = original._visibleColumnMap;
        dbColumnSchema = original.dbColumnSchema;
        schemaTable = original.schemaTable;
        cekTable = original.cekTable;

        if (original._metaDataArray == null)
        {
            _metaDataArray = null;
        }
        else
        {
            _metaDataArray = new _SqlMetaData[original._metaDataArray.Length];
            for (int idx = 0; idx < _metaDataArray.Length; idx++)
            {
                _metaDataArray[idx] = (_SqlMetaData)original._metaDataArray[idx].Clone();
            }
        }
    }

    internal int Length
    {
        get
        {
            return _metaDataArray.Length;
        }
    }

    internal int VisibleColumnCount
    {
        get
        {
            if (_hiddenColumnCount == -1)
            {
                SetupHiddenColumns();
            }
            return Length - _hiddenColumnCount;
        }
    }

    internal _SqlMetaData this[int index]
    {
        get
        {
            return _metaDataArray[index];
        }
        set
        {
            Debug.Assert(value == null, "used only by SqlBulkCopy");
            _metaDataArray[index] = value;
        }
    }

    public int GetVisibleColumnIndex(int index)
    {
        if (_hiddenColumnCount == -1)
        {
            SetupHiddenColumns();
        }
        if (_visibleColumnMap is null)
        {
            return index;
        }
        else
        {
            return _visibleColumnMap[index];
        }
    }

    public _SqlMetaDataSet Clone()
    {
        return new _SqlMetaDataSet(this);
    }

    private void SetupHiddenColumns()
    {
        int hiddenColumnCount = 0;
        for (int index = 0; index < Length; index++)
        {
            if (_metaDataArray[index].IsHidden)
            {
                hiddenColumnCount += 1;
            }
        }

        if (hiddenColumnCount > 0)
        {
            int[] visibleColumnMap = new int[Length - hiddenColumnCount];
            int mapIndex = 0;
            for (int metaDataIndex = 0; metaDataIndex < Length; metaDataIndex++)
            {
                if (!_metaDataArray[metaDataIndex].IsHidden)
                {
                    visibleColumnMap[mapIndex] = metaDataIndex;
                    mapIndex += 1;
                }
            }
            _visibleColumnMap = visibleColumnMap;
        }
        _hiddenColumnCount = hiddenColumnCount;
    }
}
