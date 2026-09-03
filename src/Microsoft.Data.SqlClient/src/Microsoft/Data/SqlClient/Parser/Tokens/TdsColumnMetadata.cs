// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Represents metadata information for a specific column in a TDS stream. Corresponds with a
/// single column within a SQLCOLMETADATA/SQLALTMETADATA token.
/// </summary>
// @TODO: We have a mix of patterns here where some subtypes are contained within the types, while some are inherited. It seems this class would be better suited to contain the TdsTypeInfo rather than inherit from it.
internal sealed class TdsColumnMetadata : TdsTypeInfo
{
    [Flags]
    private enum MetadataFlags
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

    /// <summary>
    /// Represents the original name of the column in the data source if an alias is used.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryReadString.
    internal string baseColumn;

    /// <summary>
    /// Represents the name of the column in the TDS stream.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryReadString.
    internal string column;

    /// <summary>
    /// Represents the operation type associated with the column metadata, primarily used for
    /// alternate-row column processing.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal byte op;        // for altrow-columns only

    /// <summary>
    /// Stores the multipart name details of the table associated with the column, including server,
    /// catalog, schema, and table names.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryProcessOneTable.
    internal TdsTableName tableName;

    /// <summary>
    /// Identifies the table associated with a column, if applicable, within the context of a
    /// metadata structure.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal byte tableNum;

    private MetadataFlags _flags;

    internal TdsColumnMetadata(int ordinal) : base()
    {
        Ordinal = ordinal;
    }

    private TdsColumnMetadata(TdsColumnMetadata original) : base(original)
    {
        _flags = original._flags;

        baseColumn = original.baseColumn;
        column = original.column;
        tableName = original.tableName;
        tableNum = original.tableNum;
        op = original.op;
        Operand = original.Operand;
        Ordinal = original.Ordinal;
    }

    /// <summary>
    /// Gets the catalog name associated with the column's metadata.
    /// </summary>
    internal string CatalogName => tableName.CatalogName;


    internal bool Is2008DateTimeType => DbType is SqlDbType.Date
                                               or SqlDbType.Time
                                               or SqlDbType.DateTime2
                                               or SqlDbType.DateTimeOffset;

    /// <summary>
    /// Indicates whether the column is part of a sparse column set.
    /// </summary>
    public bool IsColumnSet
    {
        get => HasFlag(MetadataFlags.IsColumnSet);
        set => SetFlag(MetadataFlags.IsColumnSet, value);
    }

    /// <summary>
    /// Indicates whether the column has a name that differs from its original name in the data
    /// source.
    /// </summary>
    public bool IsDifferentName
    {
        get => HasFlag(MetadataFlags.IsDifferentName);
        set => SetFlag(MetadataFlags.IsDifferentName, value);
    }

    /// <summary>
    /// Indicates whether the column in the metadata is based on an expression
    /// rather than a direct column reference.
    /// </summary>
    public bool IsExpression
    {
        get => HasFlag(MetadataFlags.IsExpression);
        set => SetFlag(MetadataFlags.IsExpression, value);
    }

    /// <summary>
    /// Indicates whether the column is hidden in the result set metadata.
    /// </summary>
    public bool IsHidden
    {
        get => HasFlag(MetadataFlags.IsHidden);
        set => SetFlag(MetadataFlags.IsHidden, value);
    }

    /// <summary>
    /// Indicates whether the column is an identity column in the database.
    /// </summary>
    public bool IsIdentity
    {
        get => HasFlag(MetadataFlags.IsIdentity);
        set => SetFlag(MetadataFlags.IsIdentity, value);
    }

    /// <summary>
    /// Indicates whether the column is part of the primary key in the table or view.
    /// </summary>
    public bool IsKey
    {
        get => HasFlag(MetadataFlags.IsKey);
        set => SetFlag(MetadataFlags.IsKey, value);
    }

    /// <summary>
    /// Indicates whether the column is a large UDT with the maximum allowable length.
    /// </summary>
    internal bool IsLargeUdt => DbType == SqlDbType.Udt && length == int.MaxValue;

    /// <summary>
    /// Indicates whether the column is read-only in the context of the data source.
    /// </summary>
    public bool IsReadOnly => !HasFlag(MetadataFlags.IsUpdatableMask);

    /// <summary>
    /// Gets the zero-based position of the column within the result set metadata.
    /// </summary>
    internal int Ordinal { get; }

    /// <summary>
    /// Gets or sets the operand value associated with the column metadata.
    /// </summary>
    internal ushort Operand { get; set; }

    /// <summary>
    /// Represents the schema name associated with the table containing the column.
    /// </summary>
    internal string SchemaName => tableName.SchemaName;

    /// <summary>
    /// Gets the name of the server associated with the column's metadata.
    /// </summary>
    internal string ServerName => tableName.ServerName;

    /// <summary>
    /// Represents the name of the table associated with the column metadata.
    /// </summary>
    internal string TableName => tableName.TableName;

    /// <summary>
    /// Indicates whether the associated column can be updated.
    /// </summary>
    public byte Updatability
    {
        get => (byte)(_flags & MetadataFlags.IsUpdatableMask);
        set => _flags = (MetadataFlags)((value & (byte)MetadataFlags.IsUpdatableMask) | ((int)_flags & ~(byte)MetadataFlags.IsUpdatableMask));
    }

    /// <summary>
    /// Creates a copy of the current TdsColumnMetadata instance.
    /// </summary>
    /// <returns>
    /// A new TdsColumnMetadata object that is a copy of the current instance.
    /// </returns>
    internal new TdsColumnMetadata Clone() =>
        new TdsColumnMetadata(this);

    private bool HasFlag(MetadataFlags flag)
    {
        return (_flags & flag) != 0;
    }

    private void SetFlag(MetadataFlags flag, bool value)
    {
        _flags = value ? _flags | flag : _flags & ~flag;
    }
}
