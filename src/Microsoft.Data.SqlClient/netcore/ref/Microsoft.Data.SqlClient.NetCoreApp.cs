// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient.Server
{
    [System.AttributeUsageAttribute(System.AttributeTargets.Field | System.AttributeTargets.Property | System.AttributeTargets.ReturnValue | System.AttributeTargets.Parameter, AllowMultiple=false, Inherited=false)]
    public partial class SqlFacetAttribute : System.Attribute
    {
        public SqlFacetAttribute() { }
        public bool IsFixedLength { get { throw null; } set { } }
        public bool IsNullable { get { throw null; } set { } }
        public int MaxSize { get { throw null; } set { } }
        public int Precision { get { throw null; } set { } }
        public int Scale { get { throw null; } set { } }
    }
}
namespace Microsoft.Data.SqlClient
{
    public partial class SqlDataReader : System.Data.Common.IDbColumnSchemaGenerator
    {
        public System.Collections.ObjectModel.ReadOnlyCollection<System.Data.Common.DbColumn> GetColumnSchema() { throw null; }
    }

    public enum PoolBlockingPeriod
    {
        Auto = 0,
        AlwaysBlock = 1,
        NeverBlock = 2,
    }
    
    public sealed partial class SqlConnectionStringBuilder : System.Data.Common.DbConnectionStringBuilder
    {
        public PoolBlockingPeriod PoolBlockingPeriod { get { throw null; } set { } }
        public Microsoft.Data.SqlClient.SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting { get { throw null; } set { } }
        public string EnclaveAttestationUrl { get { throw null; } set { } }
    }
    public sealed partial class SqlParameter : System.Data.Common.DbParameter, System.ICloneable, System.Data.IDataParameter, System.Data.IDbDataParameter
    {
        public bool ForceColumnEncryption { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
    }
    public enum SqlCommandColumnEncryptionSetting
    {
        Disabled = 3,
        Enabled = 1,
        ResultSetOnly = 2,
        UseConnectionSetting = 0,
    }
    public sealed partial class SqlCommand : System.Data.Common.DbCommand, System.ICloneable
    {
        public SqlCommand(string cmdText, Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction, Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting columnEncryptionSetting) { }
        public Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting ColumnEncryptionSetting { get { throw null; } }
    }
    public sealed partial class SqlConnection : System.Data.Common.DbConnection, System.ICloneable
    {
        public static System.TimeSpan ColumnEncryptionKeyCacheTtl { get { throw null; } set { } }
        public static bool ColumnEncryptionQueryMetadataCacheEnabled { get { throw null; } set { } }
        public static System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<string>> ColumnEncryptionTrustedMasterKeyPaths { get { throw null; } }
        public static void RegisterColumnEncryptionKeyStoreProviders(System.Collections.Generic.IDictionary<string, Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider> customProviders) { }
    }
    public enum SqlConnectionColumnEncryptionSetting
    {
        Disabled = 0,
        Enabled = 1,
    }
    public partial class SqlColumnEncryptionCertificateStoreProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
    {
        public const string ProviderName = "MSSQL_CERTIFICATE_STORE";
        public SqlColumnEncryptionCertificateStoreProvider() { }
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey) { throw null; }
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey) { throw null; }
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    public partial class SqlColumnEncryptionCngProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
    {
        public const string ProviderName = "MSSQL_CNG_STORE";
        public SqlColumnEncryptionCngProvider() { }
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey) { throw null; }
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey) { throw null; }
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    public partial class SqlColumnEncryptionCspProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
    {
        public const string ProviderName = "MSSQL_CSP_PROVIDER";
        public SqlColumnEncryptionCspProvider() { }
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey) { throw null; }
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey) { throw null; }
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    public abstract partial class SqlColumnEncryptionKeyStoreProvider
    {
        protected SqlColumnEncryptionKeyStoreProvider() { }
        public abstract byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey);
        public abstract byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey);
        public virtual byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        public virtual bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    public abstract partial class SqlColumnEncryptionEnclaveProvider
    {
        protected SqlColumnEncryptionEnclaveProvider() { }
        public abstract void CreateEnclaveSession(byte[] enclaveAttestationInfo, System.Security.Cryptography.ECDiffieHellmanCng clientDiffieHellmanKey, string attestationUrl, string servername, out Microsoft.Data.SqlClient.SqlEnclaveSession sqlEnclaveSession, out long counter);
        public abstract Microsoft.Data.SqlClient.SqlEnclaveAttestationParameters GetAttestationParameters();
        public abstract void GetEnclaveSession(string serverName, string attestationUrl, out Microsoft.Data.SqlClient.SqlEnclaveSession sqlEnclaveSession, out long counter);
        public abstract void InvalidateEnclaveSession(string serverName, string enclaveAttestationUrl, Microsoft.Data.SqlClient.SqlEnclaveSession enclaveSession);
    }
    public partial class SqlEnclaveAttestationParameters
    {
        public SqlEnclaveAttestationParameters(int protocol, byte[] input, System.Security.Cryptography.ECDiffieHellmanCng clientDiffieHellmanKey) { }
        public System.Security.Cryptography.ECDiffieHellmanCng ClientDiffieHellmanKey { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public int Protocol { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public byte[] GetInput() { throw null; }
    }
    public partial class SqlEnclaveSession
    {
        public SqlEnclaveSession(byte[] sessionKey, long sessionId) { }
        public long SessionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public byte[] GetSessionKey() { throw null; }
    }
}

namespace Microsoft.Data.SqlTypes
{
    public sealed partial class SqlFileStream : System.IO.Stream
    {
        public SqlFileStream(string path, byte[] transactionContext, System.IO.FileAccess access) { }
        public SqlFileStream(string path, byte[] transactionContext, System.IO.FileAccess access, System.IO.FileOptions options, System.Int64 allocationSize) { }
        public string Name { get { throw null; } }
        public byte[] TransactionContext { get { throw null; } }
        public override bool CanRead { get { throw null; } }
        public override bool CanSeek { get { throw null; } }
        public override bool CanWrite { get { throw null; } }
        public override long Length { get { throw null; } }
        public override long Position { get { throw null; } set { throw null; } }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { throw null; }
        public override long Seek(long offset, System.IO.SeekOrigin origin) { throw null; }
        public override void SetLength(long value) { throw null; }
        public override void Write(byte[] buffer, int offset, int count) { throw null; }
    }
}
