// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Represents a TDS-specific table name, which may include server, catalog, schema, and table segments.
/// </summary>
internal struct TdsTableName
{
    internal static readonly TdsTableName Null = new([null, null, null, null]);

    private string _multipartName;
    private string _serverName;
    private string _catalogName;
    private string _schemaName;
    private string _tableName;

    /// <summary>
    /// Constructs a new instance from an array of name parts.
    /// </summary>
    internal TdsTableName(string[] parts)
    {
        _multipartName = null;
        _serverName = parts[0];
        _catalogName = parts[1];
        _schemaName = parts[2];
        _tableName = parts[3];
    }

    /// <summary>
    /// Constructs a new instance from a multipart name. This name must be parsed before its
    /// constituent parts can be accessed.
    /// </summary>
    /// <param name="multipartName">Multipart name.</param>
    internal TdsTableName(string multipartName)
    {
        // @TODO: Is it faster to just parse this now versus rather than check everytime the parts are accessed?
        _multipartName = multipartName;
        _serverName = null;
        _catalogName = null;
        _schemaName = null;
        _tableName = null;
    }

    /// <summary>
    /// Gets or sets the server name associated with the TDS-specific table name.
    /// </summary>
    internal string ServerName
    {
        get
        {
            ParseMultipartName();
            return _serverName;
        }
        set { _serverName = value; }
    }

    /// <summary>
    /// Gets or sets the catalog name segment of the TDS-specific table name.
    /// </summary>
    internal string CatalogName
    {
        get
        {
            ParseMultipartName();
            return _catalogName;
        }
        set { _catalogName = value; }
    }

    /// <summary>
    /// Gets or sets the schema name associated with the TDS-specific table name.
    /// </summary>
    internal string SchemaName
    {
        get
        {
            ParseMultipartName();
            return _schemaName;
        }
        set { _schemaName = value; }
    }

    /// <summary>
    /// Gets or sets the table name segment of the TDS-specific table representation.
    /// </summary>
    internal string TableName
    {
        get
        {
            ParseMultipartName();
            return _tableName;
        }
        set { _tableName = value; }
    }

    private void ParseMultipartName()
    {
        if (_multipartName != null)
        {
            string[] parts = MultipartIdentifier.ParseMultipartIdentifier(
                _multipartName,
                Strings.SQL_TDSParserTableName,
                throwOnEmptyMultipartIdentifier: false);

            _serverName = parts[0];
            _catalogName = parts[1];
            _schemaName = parts[2];
            _tableName = parts[3];
            _multipartName = null;
        }
    }
}
