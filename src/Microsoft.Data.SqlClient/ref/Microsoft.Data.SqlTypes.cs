// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SqlFileStream/*' />
public sealed class SqlFileStream : System.IO.Stream
{
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor1/*' />
    public SqlFileStream(string path, byte[] transactionContext, System.IO.FileAccess access) { }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor2/*' />
    public SqlFileStream(string path, byte[] transactionContext, System.IO.FileAccess access, System.IO.FileOptions options, System.Int64 allocationSize) { }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Name/*' />
    public string Name { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/TransactionContext/*' />
    public byte[] TransactionContext { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanRead/*' />
    public override bool CanRead { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanSeek/*' />
    public override bool CanSeek { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanTimeout/*' />
    public override bool CanTimeout { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanWrite/*' />
    public override bool CanWrite { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Length/*' />
    public override long Length { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Position/*' />
    public override long Position { get { throw null; } set { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadTimeout/*' />
    public override int ReadTimeout { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteTimeout/*' />
    public override int WriteTimeout { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Flush/*' />
    public override void Flush() { }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginRead/*' />
    public override System.IAsyncResult BeginRead(byte[] buffer, int offset, int count, System.AsyncCallback callback, object state) { throw null; }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndRead/*' />
    public override int EndRead(System.IAsyncResult asyncResult) { throw null; }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginWrite/*' />
    public override System.IAsyncResult BeginWrite(byte[] buffer, int offset, int count, System.AsyncCallback callback, System.Object state) { throw null; }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndWrite/*' />
    public override void EndWrite(System.IAsyncResult asyncResult) { }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Seek/*' />
    public override long Seek(long offset, System.IO.SeekOrigin origin) { throw null; }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SetLength/*' />
    public override void SetLength(long value) { throw null; }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Read/*' />
    public override int Read(byte[] buffer, int offset, int count) { throw null; }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadByte/*' />
    public override int ReadByte() { throw null; }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Write/*' />
    public override void Write(byte[] buffer, int offset, int count) { throw null; }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteByte/*' />
    public override void WriteByte(byte value) { }
}

/// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/SqlJson/*' />
public class SqlJson : System.Data.SqlTypes.INullable
{
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor1/*' />
    public SqlJson() { }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor2/*' />
    public SqlJson(string jsonString) { }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor3/*' />
    public SqlJson(System.Text.Json.JsonDocument jsonDoc) { }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/IsNull/*' />
    public bool IsNull => throw null;
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/Null/*' />
    public static SqlJson Null => throw null;
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/Value/*' />
    public string Value { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ToString/*' />
    public override string ToString() { throw null; }
}

/// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/SqlVector/*' />
public readonly struct SqlVector<T> : System.Data.SqlTypes.INullable
    where T : unmanaged
{
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ctor1/*' />
    public SqlVector(System.ReadOnlyMemory<T> memory) { }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/IsNull/*' />
    public bool IsNull => throw null;
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Null/*' />
    public static SqlVector<T>? Null => throw null;
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Length/*' />
    public int Length { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Memory/*' />
    public System.ReadOnlyMemory<T> Memory { get { throw null; } }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/CreateNull/*' />
    public static SqlVector<T> CreateNull(int length) { throw null; }
}
