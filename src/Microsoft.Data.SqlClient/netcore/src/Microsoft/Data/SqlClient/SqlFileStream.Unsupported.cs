// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SqlFileStream/*' />
    public sealed partial class SqlFileStream : System.IO.Stream
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor1/*' />
        public SqlFileStream(string path, byte[] transactionContext, FileAccess access)
        {
            throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor2/*' />
        public SqlFileStream(string path, byte[] transactionContext, FileAccess access, FileOptions options, Int64 allocationSize)
        {
            throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported);
        }
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Name/*' />
        public string Name { get { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); } }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/TransactionContext/*' />
        public byte[] TransactionContext { get { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); } }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginRead/*' />
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndRead/*' />
        public override int EndRead(IAsyncResult asyncResult) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginWrite/*' />
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndWrite/*' />
        public override void EndWrite(IAsyncResult asyncResult) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanRead/*' />
        public override bool CanRead { get { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); } }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanSeek/*' />
        public override bool CanSeek { get { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); } }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanWrite/*' />
        public override bool CanWrite { get { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); } }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Length/*' />
        public override long Length { get { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); } }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Position/*' />
        public override long Position { get { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); } set { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); } }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Flush/*' />
        public override void Flush() { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Read/*' />
        public override int Read(byte[] buffer, int offset, int count) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
#if !NETSTANDARD2_0
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadAsync1/*' />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadAsync2/*' />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
#endif
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Seek/*' />
        public override long Seek(long offset, System.IO.SeekOrigin origin) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SetLength/*' />
        public override void SetLength(long value) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Write/*' />
        public override void Write(byte[] buffer, int offset, int count) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
#if !NETSTANDARD2_0
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteAsync1/*' />
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteAsync2/*' />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { throw new PlatformNotSupportedException(Strings.SqlFileStream_NotSupported); }
#endif
    }
}
