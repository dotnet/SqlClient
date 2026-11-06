// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests that <see cref="SqlMetaDataFactory"/> correctly reports the <c>json</c> data type
/// in the <c>DataTypes</c> schema collection based on <see cref="ConnectionCapabilities.JsonType"/>.
/// </summary>
public sealed class SqlMetaDataFactoryDataTypesTest
{
    private static DataTable ReadDataTypesTable(bool jsonTypeSupported)
    {
        using TdsServer server = new();
        server.Start();
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional
        };
        using SqlConnection connection = new(builder.ConnectionString);
        connection.Open();

        connection.InnerConnection.Capabilities.TdsVersion = TdsEnums.TDS7X_VERSION;
        connection.InnerConnection.Capabilities.ServerMajorVersion = 12;
        connection.InnerConnection.Capabilities.ServerMinorVersion = 0;
        connection.InnerConnection.Capabilities.ServerBuildNumber = 0;
        connection.InnerConnection.Capabilities.JsonType = jsonTypeSupported;

        DataTable dataTypes = connection.GetSchema(DbMetaDataCollectionNames.DataTypes);

        connection.InnerConnection.Capabilities.Reset();
        return dataTypes;
    }

    /// <summary>
    /// Verifies that <c>json</c> appears in the DataTypes schema collection when
    /// <see cref="ConnectionCapabilities.JsonType"/> is <see langword="true"/>.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DataTypesTable_Json_Supported(bool jsonTypeSupported)
    {
        DataTable dataTypes = ReadDataTypesTable(jsonTypeSupported);

        DataRow[] rows = dataTypes.Select("TypeName = 'json'");
        Assert.Equal(jsonTypeSupported, rows.Length == 1);
    }
}
