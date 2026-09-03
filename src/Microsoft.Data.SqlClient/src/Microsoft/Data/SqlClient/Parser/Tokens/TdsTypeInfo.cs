// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Text;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Represents metadata information for a SQL Server TDS data type, including type properties and
/// encoding settings. Provides functionality for handling encryption and normalization details.
/// </summary>
internal class TdsTypeInfo
{
    [Flags]
    private enum TdsTypeInfoFlags : byte
    {
        None = 0,
        IsNullable = 1 << 1,
        IsMultiValued = 1 << 2
    }

    /// <summary>
    /// Represents the collation settings associated with character-based SQL Server types.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal SqlCollation collation;


    /// <summary>
    /// Represents the length property associated with SQL Server type information. Used for
    /// defining the size or limit of a data type within the TDS protocol.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal int length;


    /// <summary>
    /// Specifies the numeric precision for a numeric SQL Server data type. Defaults to
    /// UNKNOWN_PRECISION_SCALE when not explicitly set.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal byte precision = TdsEnums.UNKNOWN_PRECISION_SCALE;


    /// <summary>
    /// Specifies the scale component of a numeric SQL Server type. Defaults to
    /// UNKNOWN_PRECISION_SCALE when not explicitly set.
    /// </summary>
    // @TODO: This cannot be an auto property yet because this value is set via an out parameter in TryRead*.
    internal byte scale = TdsEnums.UNKNOWN_PRECISION_SCALE; // give default of unknown (-1)

    private TdsTypeInfoFlags flags;

    internal TdsTypeInfo()
    {
    }

    protected TdsTypeInfo(TdsTypeInfo original)
    {
        // @TODO: Some of these fields are not simple types, should they also be cloned?

        collation = original.collation;
        flags = original.flags;
        length = original.length;
        precision = original.precision;
        scale = original.scale;

        CodePage = original.CodePage;
        DbType = original.DbType;
        Encoding = original.Encoding;
        MetaType = original.MetaType; // This is a cached instance, it should not be cloned.
        TdsType = original.TdsType;

        UdtTypeInfo = original.UdtTypeInfo?.Clone();
        XmlTypeInfo = original.XmlTypeInfo?.Clone();

        BaseTypeInfo = original.BaseTypeInfo;
        CipherMetadata = original.CipherMetadata;
        IsEncrypted = original.IsEncrypted;
    }

    /// <summary>
    /// Gets or sets the plaintext type information for the column if the column is encrypted.
    /// </summary>
    internal TdsTypeInfo BaseTypeInfo { get; set; }

    /// <summary>
    /// Gets or sets the encryption-related metadata associated with the SQL Server TDS data type.
    /// </summary>
    internal SqlCipherMetadata CipherMetadata { get; set; }

    /// <summary>
    /// Gets or sets the code page used for character encoding associated with the TDS data type
    /// instance.
    /// </summary>
    internal int CodePage { get; set; }

    /// <summary>
    /// Represents the database type of the current instance.
    /// </summary>
    internal SqlDbType DbType { get; set; }

    /// <summary>
    /// Gets or sets the character encoding used for string data representation.
    /// </summary>
    internal Encoding Encoding { get; set; }

    /// <summary>
    /// Is the algorithm handle for the cipher encryption initialized?
    /// </summary>
    internal bool IsAlgorithmInitialized() => CipherMetadata?.IsAlgorithmInitialized() ?? false;

    /// <summary>
    /// Gets or sets whether the type described by the current instance is TCE encrypted.
    /// </summary>
    internal bool IsEncrypted { get; set; }

    /// <summary>
    /// Indicates whether the associated SQL Server TDS data type supports multiple values.
    /// </summary>
    public bool IsMultiValued
    {
        get => HasFlag(TdsTypeInfoFlags.IsMultiValued);
        set => Set(TdsTypeInfoFlags.IsMultiValued, value);
    }

    /// <summary>
    /// Indicates whether the associated database column allows null values.
    /// </summary>
    public bool IsNullable
    {
        get => HasFlag(TdsTypeInfoFlags.IsNullable);
        set => Set(TdsTypeInfoFlags.IsNullable, value);
    }

    /// <summary>
    /// Gets or sets the cached MetaType information associated with the current instance.
    /// </summary>
    internal MetaType MetaType { get; set; }

    /// <summary>
    /// Returns the normalization rule version byte.
    /// </summary>
    internal byte NormalizationRuleVersion => CipherMetadata?.NormalizationRuleVersion ?? 0x00;

    /// <summary>
    /// Gets or sets the TDS (Tabular Data Stream) type of the instance.
    /// </summary>
    internal byte TdsType { get; set; }

    /// <summary>
    /// Gets or sets the UDT metadata associated with this object.
    /// </summary>
    public TdsUdtTypeInfo UdtTypeInfo { get; set; }

    /// <summary>
    /// Gets or sets the metadata for the XML schema collection associated with this data type.
    /// </summary>
    public TdsXmlTypeInfo XmlTypeInfo { get; set; }

    /// <summary>
    /// Creates a new instance of the TdsTypeInfo class that is a copy of the current instance.
    /// </summary>
    /// <returns>
    /// A new TdsTypeInfo instance populated with the same values as the original.
    /// </returns>
    internal TdsTypeInfo Clone() =>
        new TdsTypeInfo(this);

    private bool HasFlag(TdsTypeInfoFlags flag)
    {
        return (flags & flag) != 0;
    }

    private void Set(TdsTypeInfoFlags flag, bool value)
    {
        flags = value ? flags | flag : flags & ~flag;
    }
}
