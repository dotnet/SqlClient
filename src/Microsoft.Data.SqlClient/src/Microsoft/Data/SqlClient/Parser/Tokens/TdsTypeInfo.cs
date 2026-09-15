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
    public SqlMetaDataUdt Udt { get; set; }

    /// <summary>
    /// Gets or sets the metadata for the XML schema collection associated with this data type.
    /// </summary>
    public SqlMetaDataXmlSchemaCollection XmlSchemaCollection { get; set; }

    // @TODO: Can this be converted to Clone like all the other token types do?
    internal virtual void CopyFrom(TdsTypeInfo original)
    {
        this.DbType = original.DbType;
        this.TdsType = original.TdsType;
        this.precision = original.precision;
        this.scale = original.scale;
        this.length = original.length;
        this.collation = original.collation;
        this.CodePage = original.CodePage;
        this.Encoding = original.Encoding;
        this.MetaType = original.MetaType;
        this.flags = original.flags;

        if (original.Udt != null)
        {
            Udt = new SqlMetaDataUdt();
            Udt.CopyFrom(original.Udt);
        }

        if (original.XmlSchemaCollection != null)
        {
            XmlSchemaCollection = new SqlMetaDataXmlSchemaCollection();
            XmlSchemaCollection.CopyFrom(original.XmlSchemaCollection);
        }

        this.IsEncrypted = original.IsEncrypted;
        this.BaseTypeInfo = original.BaseTypeInfo;
        this.CipherMetadata = original.CipherMetadata;
    }

    private bool HasFlag(TdsTypeInfoFlags flag)
    {
        return (flags & flag) != 0;
    }

    private void Set(TdsTypeInfoFlags flag, bool value)
    {
        flags = value ? flags | flag : flags & ~flag;
    }
}
