// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Parser;

internal struct MultiPartTableName
{
    private string _multipartName;
    private string _serverName;
    private string _catalogName;
    private string _schemaName;
    private string _tableName;

    internal MultiPartTableName(string[] parts)
    {
        _multipartName = null;
        _serverName = parts[0];
        _catalogName = parts[1];
        _schemaName = parts[2];
        _tableName = parts[3];
    }

    internal MultiPartTableName(string multipartName)
    {
        _multipartName = multipartName;
        _serverName = null;
        _catalogName = null;
        _schemaName = null;
        _tableName = null;
    }

    internal string ServerName
    {
        get
        {
            ParseMultipartName();
            return _serverName;
        }
        set { _serverName = value; }
    }
    internal string CatalogName
    {
        get
        {
            ParseMultipartName();
            return _catalogName;
        }
        set { _catalogName = value; }
    }
    internal string SchemaName
    {
        get
        {
            ParseMultipartName();
            return _schemaName;
        }
        set { _schemaName = value; }
    }
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
            string[] parts = MultipartIdentifier.ParseMultipartIdentifier(_multipartName, Strings.SQL_TDSParserTableName, false);
            _serverName = parts[0];
            _catalogName = parts[1];
            _schemaName = parts[2];
            _tableName = parts[3];
            _multipartName = null;
        }
    }

    internal static readonly MultiPartTableName Null = new(new string[] { null, null, null, null });
}
