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
    private enum SqlMetaDataPrivFlags : byte
    {
        None = 0,
        IsNullable = 1 << 1,
        IsMultiValued = 1 << 2
    }



    internal byte precision = TdsEnums.UNKNOWN_PRECISION_SCALE; // give default of unknown (-1)
    internal byte scale = TdsEnums.UNKNOWN_PRECISION_SCALE; // give default of unknown (-1)
    private SqlMetaDataPrivFlags flags;
    internal int length;
    internal SqlCollation collation;
    internal int codePage;
    internal Encoding encoding;
    internal bool isEncrypted; // TCE encrypted?
    internal TdsTypeInfo baseTI;   // for encrypted columns, represents the TYPE_INFO for plaintext value
    internal SqlCipherMetadata cipherMD; // Cipher related metadata for encrypted columns.

    internal MetaType metaType; // cached metaType
    public SqlMetaDataUdt udt;
    public SqlMetaDataXmlSchemaCollection xmlSchemaCollection;

    internal TdsTypeInfo()
    {
    }

    /// <summary>
    /// Represents the database type of the current instance.
    /// </summary>
    internal SqlDbType DbType { get; set; }

    /// <summary>
    /// Gets or sets the TDS (Tabular Data Stream) type of the instance.
    /// </summary>
    internal byte TdsType { get; set; }

    /// <summary>
    /// Is the algorithm handle for the cipher encryption initialized?
    /// </summary>
    internal bool IsAlgorithmInitialized()
    {
        if (cipherMD != null)
        {
            return cipherMD.IsAlgorithmInitialized();
        }

        return false;
    }

    public bool IsNullable
    {
        get => HasFlag(SqlMetaDataPrivFlags.IsNullable);
        set => Set(SqlMetaDataPrivFlags.IsNullable, value);
    }

    public bool IsMultiValued
    {
        get => HasFlag(SqlMetaDataPrivFlags.IsMultiValued);
        set => Set(SqlMetaDataPrivFlags.IsMultiValued, value);
    }

    /// <summary>
    /// Returns the normalization rule version byte.
    /// </summary>
    /// <returns></returns>
    internal byte NormalizationRuleVersion
    {
        get
        {
            if (cipherMD != null)
            {
                return cipherMD.NormalizationRuleVersion;
            }

            return 0x00;
        }
    }

    private bool HasFlag(SqlMetaDataPrivFlags flag)
    {
        return (flags & flag) != 0;
    }

    private void Set(SqlMetaDataPrivFlags flag, bool value)
    {
        flags = value ? flags | flag : flags & ~flag;
    }

    internal virtual void CopyFrom(TdsTypeInfo original)
    {
        this.DbType = original.DbType;
        this.TdsType = original.TdsType;
        this.precision = original.precision;
        this.scale = original.scale;
        this.length = original.length;
        this.collation = original.collation;
        this.codePage = original.codePage;
        this.encoding = original.encoding;
        this.metaType = original.metaType;
        this.flags = original.flags;

        if (original.udt != null)
        {
            udt = new SqlMetaDataUdt();
            udt.CopyFrom(original.udt);
        }

        if (original.xmlSchemaCollection != null)
        {
            xmlSchemaCollection = new SqlMetaDataXmlSchemaCollection();
            xmlSchemaCollection.CopyFrom(original.xmlSchemaCollection);
        }

        this.isEncrypted = original.isEncrypted;
        this.baseTI = original.baseTI;
        this.cipherMD = original.cipherMD;
    }
}
