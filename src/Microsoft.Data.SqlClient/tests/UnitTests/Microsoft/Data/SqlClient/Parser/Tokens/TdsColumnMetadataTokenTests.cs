// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Microsoft.Data.SqlClient.Parser.Tokens;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class TdsColumnMetadataTokenTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CloneDoesNotCopyColumnEncryptionMetadata(bool encrypted)
        {
            TdsColumnMetadata original = new(0)
            {
                DbType = SqlDbType.VarBinary,
                IsEncrypted = encrypted,
                BaseTypeInfo = encrypted ? new TdsTypeInfo { DbType = SqlDbType.Int } : null,
                CipherMetadata = encrypted ? new SqlCipherMetadata(new SqlTceCipherInfoEntry(), 0, 2, null, 1, 1) : null,
                Udt = new TdsUdtTypeInfo { TypeName = "TestType" },
                XmlSchemaCollection = new SqlMetaDataXmlSchemaCollection { Name = "TestSchema" }
            };

            TdsColumnMetadata clone = (TdsColumnMetadata)original.Clone();

            Assert.NotSame(original, clone);
            Assert.Equal(original.DbType, clone.DbType);
            Assert.False(clone.IsEncrypted);
            Assert.Null(clone.BaseTypeInfo);
            Assert.Null(clone.CipherMetadata);
            Assert.NotSame(original.Udt, clone.Udt);
            Assert.Equal(original.Udt.TypeName, clone.Udt.TypeName);
            Assert.NotSame(original.XmlSchemaCollection, clone.XmlSchemaCollection);
            Assert.Equal(original.XmlSchemaCollection.Name, clone.XmlSchemaCollection.Name);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CloneDoesNotCopyCipherTable(bool encrypted)
        {
            SqlTceCipherInfoTable cipherTable = encrypted ? new SqlTceCipherInfoTable(1) : null;
            TdsColumnMetadataToken original = new(1, cipherTable);
            original[0].IsEncrypted = encrypted;
            original[0].BaseTypeInfo = encrypted ? new TdsTypeInfo { DbType = SqlDbType.Int } : null;
            original[0].CipherMetadata = encrypted ? new SqlCipherMetadata(new SqlTceCipherInfoEntry(), 0, 2, null, 1, 1) : null;

            TdsColumnMetadataToken clone = original.Clone();

            Assert.NotSame(original, clone);
            Assert.Null(clone.CekTable);
            Assert.Equal(original.Length, clone.Length);
            Assert.NotSame(original[0], clone[0]);
            Assert.False(clone[0].IsEncrypted);
            Assert.Null(clone[0].BaseTypeInfo);
            Assert.Null(clone[0].CipherMetadata);
        }
    }
}
