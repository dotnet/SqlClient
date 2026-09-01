// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

internal sealed class TdsColumnMetadata : SqlMetaDataPriv
{
    [Flags]
    private enum MetadataFlags : int
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
    private MetadataFlags flags;

    internal TdsColumnMetadata(int ordinal) : base()
    {
        this.ordinal = ordinal;
    }

    private bool HasFlag(MetadataFlags flag)
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
        get => (byte)(flags & MetadataFlags.IsUpdatableMask);
        set => flags = (MetadataFlags)((value & (byte)MetadataFlags.IsUpdatableMask) | ((int)flags & ~(byte)MetadataFlags.IsUpdatableMask));
    }

    public bool IsReadOnly
    {
        get => !HasFlag(MetadataFlags.IsUpdatableMask);
    }

    public bool IsDifferentName
    {
        get => HasFlag(MetadataFlags.IsDifferentName);
        set => Set(MetadataFlags.IsDifferentName, value);
    }

    public bool IsKey
    {
        get => HasFlag(MetadataFlags.IsKey);
        set => Set(MetadataFlags.IsKey, value);
    }

    public bool IsHidden
    {
        get => HasFlag(MetadataFlags.IsHidden);
        set => Set(MetadataFlags.IsHidden, value);
    }

    public bool IsExpression
    {
        get => HasFlag(MetadataFlags.IsExpression);
        set => Set(MetadataFlags.IsExpression, value);
    }

    public bool IsIdentity
    {
        get => HasFlag(MetadataFlags.IsIdentity);
        set => Set(MetadataFlags.IsIdentity, value);
    }

    public bool IsColumnSet
    {
        get => HasFlag(MetadataFlags.IsColumnSet);
        set => Set(MetadataFlags.IsColumnSet, value);
    }

    private void Set(MetadataFlags flag, bool value)
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
        TdsColumnMetadata result = new TdsColumnMetadata(ordinal);
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
