// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/SqlFacetAttribute/*'/>
    [System.AttributeUsageAttribute(System.AttributeTargets.Field | System.AttributeTargets.Property | System.AttributeTargets.ReturnValue | System.AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public partial class SqlFacetAttribute : System.Attribute
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/ctor/*'/>
        public SqlFacetAttribute() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/IsFixedLength/*'/>
        public bool IsFixedLength { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/IsNullable/*'/>
        public bool IsNullable { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/MaxSize/*'/>
        public int MaxSize { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/Precision/*'/>
        public int Precision { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFacetAttribute.xml' path='docs/members[@name="SqlFacetAttribute"]/Scale/*'/>
        public int Scale { get { throw null; } set { } }
    }
}
namespace Microsoft.Data.SqlClient
{
    public partial class SqlDataReader : System.Data.Common.IDbColumnSchemaGenerator
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetColumnSchema/*'/>
        public System.Collections.ObjectModel.ReadOnlyCollection<System.Data.Common.DbColumn> GetColumnSchema() { throw null; }
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/PoolBlockingPeriod/*'/>
    public enum PoolBlockingPeriod
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/Auto/*'/>
        Auto = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/AlwaysBlock/*'/>
        AlwaysBlock = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/NeverBlock/*'/>
        NeverBlock = 2,
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/SqlConnectionStringBuilder/*'/>
    public sealed partial class SqlConnectionStringBuilder : System.Data.Common.DbConnectionStringBuilder
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PoolBlockingPeriod/*'/>
        public PoolBlockingPeriod PoolBlockingPeriod { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ColumnEncryptionSetting/*'/>
        public Microsoft.Data.SqlClient.SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/EnclaveAttestationUrl/*'/>
        public string EnclaveAttestationUrl { get { throw null; } set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SqlParameter/*'/>
    public sealed partial class SqlParameter : System.Data.Common.DbParameter, System.ICloneable, System.Data.IDataParameter, System.Data.IDbDataParameter
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ForceColumnEncryption/*'/>
        public bool ForceColumnEncryption { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/SqlCommandColumnEncryptionSetting/*'/>
    public enum SqlCommandColumnEncryptionSetting
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/Disabled/*'/>
        Disabled = 3,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/Enabled/*'/>
        Enabled = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/ResultSetOnly/*'/>
        ResultSetOnly = 2,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/UseConnectionSetting/*'/>
        UseConnectionSetting = 0,
    }
    public sealed partial class SqlCommand : System.Data.Common.DbCommand, System.ICloneable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="cmdTextStringAndSqlConnectionAndSqlTransactionAndSqlCommandColumnEncryptionSetting"]/*'/>
        public SqlCommand(string cmdText, Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction, Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting columnEncryptionSetting) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ColumnEncryptionSetting/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting ColumnEncryptionSetting { get { throw null; } }
    }
    public sealed partial class SqlConnection : System.Data.Common.DbConnection, System.ICloneable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionKeyCacheTtl/*'/>
        public static System.TimeSpan ColumnEncryptionKeyCacheTtl { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionQueryMetadataCacheEnabled/*'/>
        public static bool ColumnEncryptionQueryMetadataCacheEnabled { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionTrustedMasterKeyPaths/*'/>
        public static System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<string>> ColumnEncryptionTrustedMasterKeyPaths { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RegisterColumnEncryptionKeyStoreProviders/*'/>
        public static void RegisterColumnEncryptionKeyStoreProviders(System.Collections.Generic.IDictionary<string, Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider> customProviders) { }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionColumnEncryptionSetting.xml' path='docs/members[@name="SqlConnectionColumnEncryptionSetting"]/SqlConnectionColumnEncryptionSetting/*'/>
    public enum SqlConnectionColumnEncryptionSetting
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionColumnEncryptionSetting.xml' path='docs/members[@name="SqlConnectionColumnEncryptionSetting"]/Disabled/*'/>
        Disabled = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionColumnEncryptionSetting.xml' path='docs/members[@name="SqlConnectionColumnEncryptionSetting"]/Enabled/*'/>
        Enabled = 1,
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/SqlColumnEncryptionCertificateStoreProvider/*'/>
    public partial class SqlColumnEncryptionCertificateStoreProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/ProviderName/*'/>
        public const string ProviderName = "MSSQL_CERTIFICATE_STORE";
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/ctor/*'/>
        public SqlColumnEncryptionCertificateStoreProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/DecryptColumnEncryptionKey/*'/>
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/EncryptColumnEncryptionKey/*'/>
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/SignColumnMasterKeyMetadata/*'/>
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/VerifyColumnMasterKeyMetadata/*'/>
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/SqlColumnEncryptionCngProvider/*'/>
    public partial class SqlColumnEncryptionCngProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/ProviderName/*'/>
        public const string ProviderName = "MSSQL_CNG_STORE";
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/ctor/*'/>
        public SqlColumnEncryptionCngProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/DecryptColumnEncryptionKey/*'/>
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/EncryptColumnEncryptionKey/*'/>
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/SignColumnMasterKeyMetadata/*'/>
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/VerifyColumnMasterKeyMetadata/*'/>
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/SqlColumnEncryptionCspProvider/*'/>
    public partial class SqlColumnEncryptionCspProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/ProviderName/*'/>
        public const string ProviderName = "MSSQL_CSP_PROVIDER";
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/ctor/*'/>
        public SqlColumnEncryptionCspProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/DecryptColumnEncryptionKey/*'/>
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/EncryptColumnEncryptionKey/*'/>
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/SignColumnMasterKeyMetadata/*'/>
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/VerifyColumnMasterKeyMetadata/*'/>
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/SqlColumnEncryptionKeyStoreProvider/*'/>
    public abstract partial class SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/ctor/*'/>
        protected SqlColumnEncryptionKeyStoreProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/DecryptColumnEncryptionKey/*'/>
        public abstract byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/EncryptColumnEncryptionKey/*'/>
        public abstract byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/SignColumnMasterKeyMetadata/*'/>
        public virtual byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/VerifyColumnMasterKeyMetadata/*'/>
        public virtual bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/SqlColumnEncryptionEnclaveProvider/*'/>
    public abstract partial class SqlColumnEncryptionEnclaveProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/ctor/*'/>
        protected SqlColumnEncryptionEnclaveProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/CreateEnclaveSession/*'/>
        public abstract void CreateEnclaveSession(byte[] enclaveAttestationInfo, System.Security.Cryptography.ECDiffieHellmanCng clientDiffieHellmanKey, string attestationUrl, string servername, out Microsoft.Data.SqlClient.SqlEnclaveSession sqlEnclaveSession, out long counter);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetAttestationParameters/*'/>
        public abstract Microsoft.Data.SqlClient.SqlEnclaveAttestationParameters GetAttestationParameters();
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetEnclaveSession/*'/>
        public abstract void GetEnclaveSession(string serverName, string attestationUrl, out Microsoft.Data.SqlClient.SqlEnclaveSession sqlEnclaveSession, out long counter);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/InvalidateEnclaveSession/*'/>
        public abstract void InvalidateEnclaveSession(string serverName, string enclaveAttestationUrl, Microsoft.Data.SqlClient.SqlEnclaveSession enclaveSession);
    }
    /// <include file='.\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/SqlEnclaveAttestationParameters/*' />
    public partial class SqlEnclaveAttestationParameters
    {
        /// <include file='.\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/ctor/*' />
        public SqlEnclaveAttestationParameters(int protocol, byte[] input, System.Security.Cryptography.ECDiffieHellmanCng clientDiffieHellmanKey) { }
        /// <include file='.\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/ClientDiffieHellmanKey/*' />
        public System.Security.Cryptography.ECDiffieHellmanCng ClientDiffieHellmanKey { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='.\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/Protocol/*' />
        public int Protocol { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='.\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/GetInput/*' />
        public byte[] GetInput() { throw null; }
    }
    /// <include file='.\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/SqlEnclaveSession/*' />
    public partial class SqlEnclaveSession
    {
        /// <include file='.\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/ctor/*' />
        public SqlEnclaveSession(byte[] sessionKey, long sessionId) { }
        /// <include file='.\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/SessionId/*' />
        public long SessionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='.\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlEnclaveSession.xml' path='docs/members[@name="SqlEnclaveSession"]/GetSessionKey/*' />
        public byte[] GetSessionKey() { throw null; }
    }
}

namespace Microsoft.Data.SqlTypes
{
    /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SqlFileStream/*' />
    public sealed partial class SqlFileStream : System.IO.Stream
    {
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor1/*' />
        public SqlFileStream(string path, byte[] transactionContext, System.IO.FileAccess access) { }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor2/*' />
        public SqlFileStream(string path, byte[] transactionContext, System.IO.FileAccess access, System.IO.FileOptions options, System.Int64 allocationSize) { }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Name/*' />
        public string Name { get { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/TransactionContext/*' />
        public byte[] TransactionContext { get { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanRead/*' />
        public override bool CanRead { get { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanSeek/*' />
        public override bool CanSeek { get { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanTimeout/*' />
        public override bool CanTimeout { get { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanWrite/*' />
        public override bool CanWrite { get { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Length/*' />
        public override long Length { get { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Position/*' />
        public override long Position { get { throw null; } set { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadTimeout/*' />
        public override int ReadTimeout { get { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteTimeout/*' />
        public override int WriteTimeout { get { throw null; } }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Flush/*' />
        public override void Flush() { }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginRead/*' />
        public override System.IAsyncResult BeginRead(byte[] buffer, int offset, int count, System.AsyncCallback callback, object state) { throw null; }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndRead/*' />
        public override int EndRead(System.IAsyncResult asyncResult) { throw null; }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginWrite/*' />
        public override System.IAsyncResult BeginWrite(byte[] buffer, int offset, int count, System.AsyncCallback callback, System.Object state) { throw null; }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndWrite/*' />
        public override void EndWrite(System.IAsyncResult asyncResult) { }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Seek/*' />
        public override long Seek(long offset, System.IO.SeekOrigin origin) { throw null; }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SetLength/*' />
        public override void SetLength(long value) { throw null; }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Read/*' />
        public override int Read(byte[] buffer, int offset, int count) { throw null; }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadByte/*' />
        public override int ReadByte() { throw null; }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Write/*' />
        public override void Write(byte[] buffer, int offset, int count) { throw null; }
        /// <include file='..\..\..\..\doc\snippets\Microsoft.Data.SqlTypes\SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteByte/*' />
        public override void WriteByte(byte value) { }
    }
}
