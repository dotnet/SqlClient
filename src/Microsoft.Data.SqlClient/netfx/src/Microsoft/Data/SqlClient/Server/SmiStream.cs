// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Stream-like object that uses SmiEventSink for server-side errors.

using System.IO;

namespace Microsoft.Data.SqlClient.Server
{
    internal abstract class SmiStream
    {
        public abstract bool CanRead
        {
            get;
        }

        // If CanSeek is false, Position, Seek, Length, and SetLength should throw.
        public abstract bool CanSeek
        {
            get;
        }

        public abstract bool CanWrite
        {
            get;
        }

        public abstract long GetLength(SmiEventSink sink);

        public abstract long GetPosition(SmiEventSink sink);
        public abstract void SetPosition(SmiEventSink sink, long position);

        public abstract void Flush(SmiEventSink sink);

        public abstract long Seek(SmiEventSink sink, long offset, SeekOrigin origin);

        public abstract void SetLength(SmiEventSink sink, long value);

        public abstract int Read(SmiEventSink sink, byte[] buffer, int offset, int count);
        public abstract void Write(SmiEventSink sink, byte[] buffer, int offset, int count);

        public abstract int Read(SmiEventSink sink, char[] buffer, int offset, int count);
        public abstract void Write(SmiEventSink sink, char[] buffer, int offset, int count);
    }

}
