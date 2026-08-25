// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// A column master key, created at the start of its scope and dropped when disposed.
/// </summary>
public abstract class ColumnMasterKey : DatabaseObject<ColumnMasterKey.CreationParameters>
{
    private const string DefinitionTemplate = "CREATE COLUMN MASTER KEY {0} WITH (KEY_STORE_PROVIDER_NAME = '{1}', KEY_PATH = '{2}'{3})";

    public sealed class CreationParameters
    {
        public SqlColumnEncryptionKeyStoreProvider Provider { get; }

        public string ProviderName { get; }

        public string KeyPath { get; }

        public bool AllowEnclaveComputations { get; }

        internal CreationParameters(SqlColumnEncryptionKeyStoreProvider provider,
            string providerName,
            string keyPath,
            bool allowEnclaveComputations)
        {
            Provider = provider;
            ProviderName = providerName;
            KeyPath = keyPath;
            AllowEnclaveComputations = allowEnclaveComputations;
        }
    }

    protected ColumnMasterKey(SqlConnection connection, string namePrefix, CreationParameters creationParameters)
        : base(connection, name: GenerateLongName(namePrefix), definition: DefinitionTemplate,
            state: creationParameters, shouldCreate: true, shouldDrop: true)
    {
    }

    protected override void CreateObject(string definition)
    {
        string enclaveStatement;

        if (State.AllowEnclaveComputations)
        {
            byte[] signature = State.Provider.SignColumnMasterKeyMetadata(State.KeyPath, State.AllowEnclaveComputations);
            string signatureString = BitConverter.ToString(signature).Replace("-", "");

            enclaveStatement = ", ENCLAVE_COMPUTATIONS (SIGNATURE = 0x" + signatureString + ")";
        }
        else
        {
            enclaveStatement = string.Empty;
        }

        definition = string.Format(definition, Name, State.ProviderName, State.KeyPath, enclaveStatement);

        using SqlCommand createCommand = new(definition, Connection);

        createCommand.ExecuteNonQuery();
    }

    protected override void DropObject()
    {
        using SqlCommand dropCommand = new($"IF EXISTS (SELECT 1 FROM sys.column_master_keys where name = @Name) DROP COLUMN MASTER KEY {Name}", Connection);
        dropCommand.Parameters.AddWithValue("@Name", UnescapedName);

        dropCommand.ExecuteNonQuery();
    }

    public byte[] Encrypt(byte[] columnEncryptionKey) =>
        State.Provider.EncryptColumnEncryptionKey(State.KeyPath, "RSA_OAEP", columnEncryptionKey);

    public byte[] Decrypt(byte[] encryptedColumnEncryptionKey) =>
        State.Provider.DecryptColumnEncryptionKey(State.KeyPath, "RSA_OAEP", encryptedColumnEncryptionKey);
}

/// <summary>
/// A column master key backed by a Cryptographic Service Provider. Created at the start of its
/// scope and dropped when disposed.
/// </summary>
public sealed class CspProviderBackedColumnMasterKey : ColumnMasterKey
{
    /// <summary>
    /// Initializes a new instance of the CspProviderBackedColumnMasterKey class using the specified
    /// SQL connection, name and a certificate containing a CSP-backed private key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If a column master key with the specified name already exists, it will be dropped automatically
    /// before creation.
    /// </para>
    /// <para>
    /// This column master key will be backed by the <see cref="SqlColumnEncryptionCspProvider"/> class.
    /// </para>
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="namePrefix">The column master key name.</param>
    /// <param name="cspProvider">The certificate to wrap. Must contain a CSP-backed private key.</param>
    /// <param name="allowEnclaveComputations"><c>true</c> to enable enclave computations.</param>
    public CspProviderBackedColumnMasterKey(SqlConnection connection, string namePrefix,
        CspCertificateFixture cspProvider, bool allowEnclaveComputations)
        : base(connection, namePrefix, GenerateCreationParameters(cspProvider, allowEnclaveComputations))
    {
    }

    private static CreationParameters GenerateCreationParameters(CspCertificateFixture cspProvider, bool allowEnclaveComputations) =>
        new(provider: new SqlColumnEncryptionCspProvider(),
            providerName: SqlColumnEncryptionCspProvider.ProviderName,
            cspProvider.CspKeyPath ?? throw new InvalidOperationException("Certificate lacks a CSP key."),
            allowEnclaveComputations);
}

/// <summary>
/// A column master key backed by a certificate. Created at the start of its scope and dropped when disposed.
/// </summary>
public sealed class CertificateBackedColumnMasterKey : ColumnMasterKey
{
    /// <summary>
    /// Initializes a new instance of the CertificateBackedColumnMasterKey class using the specified
    /// SQL connection, name and a certificate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If a column master key with the specified name already exists, it will be dropped automatically
    /// before creation.
    /// </para>
    /// <para>
    /// This column master key will be backed by the <see cref="SqlColumnEncryptionCertificateStoreProvider"/>
    /// class.
    /// </para>
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="namePrefix">The column master key name.</param>
    /// <param name="cspCertificate">The certificate to wrap. Must contain a private key.</param>
    /// <param name="allowEnclaveComputations"><c>true</c> to enable enclave computations.</param>
    public CertificateBackedColumnMasterKey(SqlConnection connection, string namePrefix,
        CspCertificateFixture cspCertificate, bool allowEnclaveComputations)
        : base(connection, namePrefix, GenerateCreationParameters(cspCertificate.CspCertificatePath, allowEnclaveComputations))
    {
    }

    /// <summary>
    /// Initializes a new instance of the ColumnMasterKey class using the specified SQL connection,
    /// name and a certificate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If a column master key with the specified name already exists, it will be dropped automatically
    /// before creation.
    /// </para>
    /// <para>
    /// This column master key will be backed by the <see cref="SqlColumnEncryptionCertificateStoreProvider"/>
    /// class.
    /// </para>
    /// </remarks>
    /// <param name="connection">The SQL connection used to interact with the database.</param>
    /// <param name="namePrefix">The column master key name.</param>
    /// <param name="cmkCertificate">The certificate to wrap. Must contain a private key.</param>
    /// <param name="allowEnclaveComputations"><c>true</c> to enable enclave computations.</param>
    public CertificateBackedColumnMasterKey(SqlConnection connection, string namePrefix,
        ColumnMasterKeyCertificateFixture cmkCertificate, bool allowEnclaveComputations)
        : base(connection, namePrefix, GenerateCreationParameters(
                cmkCertificate.ColumnMasterKeyCertificatePath
                    ?? throw new InvalidOperationException("Certificate has not been created."),
                allowEnclaveComputations))
    {
    }

    private static CreationParameters GenerateCreationParameters(string certificatePath, bool allowEnclaveComputations) =>
        new(provider: new SqlColumnEncryptionCertificateStoreProvider(),
            providerName: SqlColumnEncryptionCertificateStoreProvider.ProviderName,
            certificatePath,
            allowEnclaveComputations);
}
