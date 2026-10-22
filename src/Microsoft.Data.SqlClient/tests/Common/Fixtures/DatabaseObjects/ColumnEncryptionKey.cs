// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A column encryption key, created at the start of its scope and dropped when disposed.
/// </summary>
public sealed class ColumnEncryptionKey : DatabaseObject<ColumnMasterKey>
{
    private const int PlaintextKeyLength = 32;

    private const string DefinitionTemplate = "CREATE COLUMN ENCRYPTION KEY {0} WITH VALUES" +
        " (COLUMN_MASTER_KEY = {1}, ALGORITHM = 'RSA_OAEP', ENCRYPTED_VALUE = 0x{2})";

    private ColumnMasterKey ColumnMasterKey => State;

    /// <summary>
    /// Initializes a new instance of the ColumnEncryptionKey class using the specified SQL connection,
    /// name and a column master key.
    /// </summary>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="namePrefix">The column encryption key name.</param>
    /// <param name="cmkOrigin">The column master key which backs this encryption key.</param>
    public ColumnEncryptionKey(SqlConnection connection, string namePrefix, ColumnMasterKey cmkOrigin)
        : base(connection, GenerateLongName(namePrefix), definition: DefinitionTemplate,
            state: cmkOrigin)
    {
    }

    protected override void CreateObject(string definition)
    {
        string encryptedValue;

        using (RandomNumberGenerator rnd = RandomNumberGenerator.Create())
        {
            byte[] randomPlaintext = new byte[PlaintextKeyLength];
            byte[] encryptedPlaintext;

            rnd.GetBytes(randomPlaintext);
            encryptedPlaintext = ColumnMasterKey.Encrypt(randomPlaintext);

            encryptedValue = BitConverter.ToString(encryptedPlaintext).Replace("-", "");
        }

        definition = string.Format(definition, Name, ColumnMasterKey.Name, encryptedValue);
        using SqlCommand createCommand = new(definition, Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        using SqlCommand dropCommand = new($"IF EXISTS (SELECT 1 FROM sys.column_encryption_keys where name = @Name) DROP COLUMN ENCRYPTION KEY {Name}", Connection);
        dropCommand.Parameters.AddWithValue("@Name", UnescapedName);

        dropCommand.ExecuteNonQuery();
    }
}
