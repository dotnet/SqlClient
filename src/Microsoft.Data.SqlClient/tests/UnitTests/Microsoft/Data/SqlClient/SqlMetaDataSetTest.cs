// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Microsoft.Data.SqlClient.Parser.Tokens;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class SqlMetaDataSetTest
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CloneDoesNotCopyColumnEncryptionMetadata(bool encrypted)
        {
            _SqlMetaData original = new(0)
            {
                type = SqlDbType.VarBinary,
                isEncrypted = encrypted,
                baseTI = encrypted ? new SqlMetaDataPriv { type = SqlDbType.Int } : null,
                cipherMD = encrypted ? new SqlCipherMetadata(new SqlTceCipherInfoEntry(), 0, 2, null, 1, 1) : null,
                udt = new SqlMetaDataUdt { TypeName = "TestType" },
                xmlSchemaCollection = new SqlMetaDataXmlSchemaCollection { Name = "TestSchema" }
            };

            _SqlMetaData clone = (_SqlMetaData)original.Clone();

            Assert.NotSame(original, clone);
            Assert.Equal(original.type, clone.type);
            Assert.False(clone.isEncrypted);
            Assert.Null(clone.baseTI);
            Assert.Null(clone.cipherMD);
            Assert.NotSame(original.udt, clone.udt);
            Assert.Equal(original.udt.TypeName, clone.udt.TypeName);
            Assert.NotSame(original.xmlSchemaCollection, clone.xmlSchemaCollection);
            Assert.Equal(original.xmlSchemaCollection.Name, clone.xmlSchemaCollection.Name);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CloneDoesNotCopyCipherTable(bool encrypted)
        {
            SqlTceCipherInfoTable cipherTable = encrypted ? new SqlTceCipherInfoTable(1) : null;
            TdsColumnMetadataToken original = new(1, cipherTable);
            original[0].isEncrypted = encrypted;
            original[0].baseTI = encrypted ? new SqlMetaDataPriv { type = SqlDbType.Int } : null;
            original[0].cipherMD = encrypted ? new SqlCipherMetadata(new SqlTceCipherInfoEntry(), 0, 2, null, 1, 1) : null;

            TdsColumnMetadataToken clone = original.Clone();

            Assert.NotSame(original, clone);
            Assert.Null(clone.cekTable);
            Assert.Equal(original.Length, clone.Length);
            Assert.NotSame(original[0], clone[0]);
            Assert.False(clone[0].isEncrypted);
            Assert.Null(clone[0].baseTI);
            Assert.Null(clone[0].cipherMD);
        }
    }
}
