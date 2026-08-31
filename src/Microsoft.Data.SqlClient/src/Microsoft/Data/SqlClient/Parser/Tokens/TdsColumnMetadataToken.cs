// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

internal sealed class TdsColumnMetadataToken
{
    /// <summary>
    /// Represents the identifier associated with an alternative metadata set in the TDS stream.
    /// </summary>
    // @TODO: This cannot be converted to an auto-property yet because it is accessed as an out parameter from TryReadUint16
    internal ushort id;

    private readonly _SqlMetaData[] _metadataArray;
    private int _hiddenColumnCount; // @TODO: -1 is used as magic value for "unread hidden columns". We should derive all this from _visibleColumnMap?
    private int[] _visibleColumnMap;

    internal TdsColumnMetadataToken(int count)
    {
        _hiddenColumnCount = -1;
        _metadataArray = new _SqlMetaData[count];
        for (int i = 0; i < _metadataArray.Length; ++i)
        {
            _metadataArray[i] = new _SqlMetaData(i);
        }
    }

    internal TdsColumnMetadataToken(int count, SqlTceCipherInfoTable cipherTable)
        : this(count)
    {
        CekTable = cipherTable;
    }

    private TdsColumnMetadataToken(TdsColumnMetadataToken original)
    {
        id = original.id;
        _hiddenColumnCount = original._hiddenColumnCount;
        _visibleColumnMap = original._visibleColumnMap;
        DbColumnSchema = original.DbColumnSchema;
        SchemaTable = original.SchemaTable;
        CekTable = original.CekTable;

        if (original._metadataArray == null)
        {
            _metadataArray = null;
        }
        else
        {
            _metadataArray = new _SqlMetaData[original._metadataArray.Length];
            for (int idx = 0; idx < _metadataArray.Length; idx++)
            {
                _metadataArray[idx] = (_SqlMetaData)original._metadataArray[idx].Clone();
            }
        }
    }

    /// <summary>
    /// Provides access to the metadata array associated with this token. Allows retrieval and
    /// updating of metadata at a specific index.
    /// </summary>
    internal _SqlMetaData this[int index]
    {
        get => _metadataArray[index];

        set
        {
            // @TODO: If we're only allowed to set this to null, it feels like we're doing something wrong.
            Debug.Assert(value == null, "used only by SqlBulkCopy");
            _metadataArray[index] = value;
        }
    }

    /// <summary>
    /// Represents the table of column encryption keys (CEKs) associated with this metadata token.
    /// </summary>
    internal SqlTceCipherInfoTable CekTable { get; }

    /// <summary>
    /// Represents the schema of database columns associated with this metadata token.
    /// </summary>
    internal ReadOnlyCollection<DbColumn> DbColumnSchema { get; set; }

    /// <summary>
    /// Gets the number of metadata entries in the associated metadata array. This includes both
    /// visible and hidden columns.
    /// </summary>
    internal int Length => _metadataArray.Length;

    /// <summary>
    /// Represents the schema metadata for the columns associated with this token.
    /// </summary>
    internal DataTable SchemaTable { get; set; }

    /// <summary>
    /// Gets the number of visible columns excluding hidden columns.
    /// </summary>
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

    /// <summary>
    /// Creates a new instance of TdsColumnMetadataToken that is a deep copy of the current
    /// instance.
    /// </summary>
    public TdsColumnMetadataToken Clone()
    {
        return new TdsColumnMetadataToken(this);
    }

    /// <summary>
    /// Retrieves the index of a visible column based on the provided index,
    /// considering hidden columns.
    /// </summary>
    /// <param name="index">The input index of the column to retrieve.</param>
    /// <returns>The index of the visible column corresponding to the given input index.</returns>
    public int GetVisibleColumnIndex(int index)
    {
        if (_hiddenColumnCount == -1)
        {
            SetupHiddenColumns();
        }

        return _visibleColumnMap?[index] ?? index;
    }

    private void SetupHiddenColumns()
    {
        int hiddenColumnCount = 0;
        for (int index = 0; index < Length; index++)
        {
            if (_metadataArray[index].IsHidden)
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
                if (!_metadataArray[metaDataIndex].IsHidden)
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
