// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests that <see cref="SqlMetaDataFactory"/> correctly reports the <c>json</c> data type
/// in the <c>DataTypes</c> schema collection based on <see cref="ConnectionCapabilities.JsonType"/>.
/// </summary>
public sealed class SqlMetaDataFactoryDataTypesTest
{
    private static Stream GetMetaDataXmlStream()
    {
        Assembly assembly = typeof(SqlConnection).Assembly;
        Stream? stream = assembly.GetManifestResourceStream("Microsoft.Data.SqlClient.SqlMetaData.xml");
        Assert.NotNull(stream);
        return stream;
    }

    private static SqlMetaDataFactory CreateFactory(bool jsonTypeSupported)
    {
        Stream stream = GetMetaDataXmlStream();
        ConnectionCapabilities capabilities = new()
        {
            TdsVersion = TdsEnums.TDS7X_VERSION,
            ServerMajorVersion = 12,
            ServerMinorVersion = 0,
            ServerBuildNumber = 0,
            JsonType = jsonTypeSupported
        };
        return new SqlMetaDataFactory(stream, capabilities);
    }

    private static bool DataTypesContainsJson(SqlMetaDataFactory factory)
    {
        FieldInfo? field = typeof(SqlMetaDataFactory)
            .GetField("_collectionDataSet", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        DataSet? dataSet = (DataSet?)field.GetValue(factory);
        Assert.NotNull(dataSet);

        DataTable? dataTypes = dataSet.Tables[DbMetaDataCollectionNames.DataTypes];
        Assert.NotNull(dataTypes);

        DataColumn? typeNameColumn = dataTypes.Columns[DbMetaDataColumnNames.TypeName];
        Assert.NotNull(typeNameColumn);

        return dataTypes.Rows
            .Cast<DataRow>()
            .Any(row => (string)row[typeNameColumn] == "json");
    }

    /// <summary>
    /// Verifies that <c>json</c> appears in the DataTypes schema collection when
    /// <see cref="ConnectionCapabilities.JsonType"/> is <see langword="true"/>.
    /// </summary>
    [Fact]
    public void DataTypesTable_ContainsJson_WhenJsonTypeSupported()
    {
        using SqlMetaDataFactory factory = CreateFactory(jsonTypeSupported: true);

        Assert.True(DataTypesContainsJson(factory));
    }

    /// <summary>
    /// Verifies that <c>json</c> does not appear in the DataTypes schema collection when
    /// <see cref="ConnectionCapabilities.JsonType"/> is <see langword="false"/>, covering
    /// Azure SQL which always reports version 12.x regardless of json support.
    /// </summary>
    [Fact]
    public void DataTypesTable_DoesNotContainJson_WhenJsonTypeNotSupported()
    {
        using SqlMetaDataFactory factory = CreateFactory(jsonTypeSupported: false);

        Assert.False(DataTypesContainsJson(factory));
    }
}
