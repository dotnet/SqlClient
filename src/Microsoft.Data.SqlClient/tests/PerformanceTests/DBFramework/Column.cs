// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Text;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class Column
    {
        public string Name;
        public DataType Type;
        public object Value;
        public ColumnEncryptionKey EncryptionKey;

        public Column(DataType type, string prefix = null, object value = null, ColumnEncryptionKey encryptionKey = null)
        {
            Type = type;
            Name = (prefix ?? "c_") + type.Name;
            Value = value ?? Type.DefaultValue;
            EncryptionKey = Type.EncryptionSupported ? encryptionKey : null;
        }

        public string QueryString =>
            EncryptionKey is null
                ? $"{Name} {Type}"
                : $"{Name} {Type} {EncryptedCollation}ENCRYPTED WITH " +
                    $"(COLUMN_ENCRYPTION_KEY = {EncryptionKey.Name}," +
                    $"ENCRYPTION_TYPE = DETERMINISTIC," +
                    $"ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256')";

        private string EncryptedCollation =>
            Type is MaxLengthValueType maxLengthValueType && maxLengthValueType.CharacterType
                ? "COLLATE Latin1_General_BIN2 "
                : string.Empty;

        public DataColumn AsDataColumn() => new(Name);
    }
}
