// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Data.SqlClient.UnitTests.IO
{
    /// <summary>
    /// A stream which can split the payload across multiple reads.
    /// </summary>
    internal class SplittableStream : MemoryStream
    {
        private byte[] _payload;

        private int _payloadOffset;

        private int _splitSize;

        public SplittableStream(byte[] payload, int splitSize) : base()
        {
            _payload = payload;
            _splitSize = splitSize;
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            int dataLeft = _payload.Length - _payloadOffset;

            // Nothing to read.
            if (dataLeft == 0)
            {
                return 0;
            }

            int len = Math.Min(Math.Min(_splitSize, dataLeft), count);
            Buffer.BlockCopy(_payload, _payloadOffset, buffer, offset, len);
            _payloadOffset += len;
            return len;
        }
    }
}
