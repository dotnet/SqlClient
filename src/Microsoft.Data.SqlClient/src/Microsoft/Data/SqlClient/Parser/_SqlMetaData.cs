// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;

namespace Microsoft.Data.SqlClient.Parser;

internal sealed class _SqlMetaData : SqlMetaDataPriv
{
    [Flags]
    private enum _SqlMetadataFlags : int
    {
        None = 0,

        Updatable = 1 << 0,
        UpdateableUnknown = 1 << 1,
        IsDifferentName = 1 << 2,
        IsKey = 1 << 3,
        IsHidden = 1 << 4,
        IsExpression = 1 << 5,
        IsIdentity = 1 << 6,
        IsColumnSet = 1 << 7,

        IsUpdatableMask = (Updatable | UpdateableUnknown) // two bit field (0 is read only, 1 is updatable, 2 is updatability unknown)
    }

    internal string column;
    internal string baseColumn;
    internal MultiPartTableName multiPartTableName;
    internal readonly int ordinal;
    internal byte tableNum;
    internal byte op;        // for altrow-columns only
    internal ushort operand; // for altrow-columns only
    private _SqlMetadataFlags flags;

    internal _SqlMetaData(int ordinal) : base()
    {
        this.ordinal = ordinal;
    }

    private bool HasFlag(_SqlMetadataFlags flag)
    {
        return (flags & flag) != 0;
    }

    internal string serverName
    {
        get
        {
            return multiPartTableName.ServerName;
        }
    }
    internal string catalogName
    {
        get
        {
            return multiPartTableName.CatalogName;
        }
    }
    internal string schemaName
    {
        get
        {
            return multiPartTableName.SchemaName;
        }
    }
    internal string tableName
    {
        get
        {
            return multiPartTableName.TableName;
        }
    }

    public byte Updatability
    {
        get => (byte)(flags & _SqlMetadataFlags.IsUpdatableMask);
        set => flags = (_SqlMetadataFlags)((value & (byte)_SqlMetadataFlags.IsUpdatableMask) | ((int)flags & ~(byte)_SqlMetadataFlags.IsUpdatableMask));
    }

    public bool IsReadOnly
    {
        get => !HasFlag(_SqlMetadataFlags.IsUpdatableMask);
    }

    public bool IsDifferentName
    {
        get => HasFlag(_SqlMetadataFlags.IsDifferentName);
        set => Set(_SqlMetadataFlags.IsDifferentName, value);
    }

    public bool IsKey
    {
        get => HasFlag(_SqlMetadataFlags.IsKey);
        set => Set(_SqlMetadataFlags.IsKey, value);
    }

    public bool IsHidden
    {
        get => HasFlag(_SqlMetadataFlags.IsHidden);
        set => Set(_SqlMetadataFlags.IsHidden, value);
    }

    public bool IsExpression
    {
        get => HasFlag(_SqlMetadataFlags.IsExpression);
        set => Set(_SqlMetadataFlags.IsExpression, value);
    }

    public bool IsIdentity
    {
        get => HasFlag(_SqlMetadataFlags.IsIdentity);
        set => Set(_SqlMetadataFlags.IsIdentity, value);
    }

    public bool IsColumnSet
    {
        get => HasFlag(_SqlMetadataFlags.IsColumnSet);
        set => Set(_SqlMetadataFlags.IsColumnSet, value);
    }

    private void Set(_SqlMetadataFlags flag, bool value)
    {
        flags = value ? flags | flag : flags & ~flag;
    }

    internal bool Is2008DateTimeType
    {
        get
        {
            return SqlDbType.Date == type || SqlDbType.Time == type || SqlDbType.DateTime2 == type || SqlDbType.DateTimeOffset == type;
        }
    }

    internal bool IsLargeUdt
    {
        get
        {
            return type == SqlDbType.Udt && length == int.MaxValue;
        }
    }

    public object Clone()
    {
        _SqlMetaData result = new _SqlMetaData(ordinal);
        result.CopyFrom(this);
        result.column = column;
        result.baseColumn = baseColumn;
        result.multiPartTableName = multiPartTableName;
        result.tableNum = tableNum;
        result.flags = flags;
        result.op = op;
        result.operand = operand;
        return result;
    }
}
