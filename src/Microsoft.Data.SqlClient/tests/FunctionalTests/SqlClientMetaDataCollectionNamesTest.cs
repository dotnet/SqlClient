// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlClientMetaDataCollectionNamesTest
    {
        [Fact]
        public void ValuesTest()
        {
            Assert.Equal("Columns", SqlClientMetaDataCollectionNames.Columns);
            Assert.Equal("Databases", SqlClientMetaDataCollectionNames.Databases);
            Assert.Equal("ForeignKeys", SqlClientMetaDataCollectionNames.ForeignKeys);
            Assert.Equal("IndexColumns", SqlClientMetaDataCollectionNames.IndexColumns);
            Assert.Equal("Indexes", SqlClientMetaDataCollectionNames.Indexes);
            Assert.Equal("ProcedureParameters", SqlClientMetaDataCollectionNames.ProcedureParameters);
            Assert.Equal("Procedures", SqlClientMetaDataCollectionNames.Procedures);
            Assert.Equal("Tables", SqlClientMetaDataCollectionNames.Tables);
            Assert.Equal("UserDefinedTypes", SqlClientMetaDataCollectionNames.UserDefinedTypes);
            Assert.Equal("Users", SqlClientMetaDataCollectionNames.Users);
            Assert.Equal("ViewColumns", SqlClientMetaDataCollectionNames.ViewColumns);
            Assert.Equal("Views", SqlClientMetaDataCollectionNames.Views);
            Assert.Equal("AllColumns", SqlClientMetaDataCollectionNames.AllColumns);
            Assert.Equal("ColumnSetColumns", SqlClientMetaDataCollectionNames.ColumnSetColumns);
        }
    }
}
