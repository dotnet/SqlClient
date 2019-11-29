// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class TdsParser
    {
        internal static void FillGuidBytes(Guid guid, Span<byte> buffer) => guid.TryWriteBytes(buffer);

        internal static void FillDoubleBytes(double value, Span<byte> buffer) => BitConverter.TryWriteBytes(buffer, value);

        internal static void FillFloatBytes(float v, Span<byte> buffer) => BitConverter.TryWriteBytes(buffer, v);
        
        internal static Guid ConstructGuid(ReadOnlySpan<byte> bytes)
        {
            Debug.Assert(bytes.Length >= 16, "not enough bytes to set guid");
            return new Guid(bytes);
        }

        private sealed partial class TdsOutputStream
        {
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
            {
                Debug.Assert(_parser._asyncWrite);
                ReadOnlySpan<byte> span = buffer.Span;

                StripPreamble(ref span);

                ValueTask task = default;
                if (span.Length > 0)
                {
                    _parser.WriteInt(span.Length, _stateObj); // write length of chunk
                    task = new ValueTask(_stateObj.WriteByteSpan(span, canAccumulate: false));
                }

                return task;
            }

            private void StripPreamble(ref ReadOnlySpan<byte> buffer)
            {
                if (_preambleToStrip != null && buffer.Length >= _preambleToStrip.Length)
                {
                    for (int idx = 0; idx < _preambleToStrip.Length; idx++)
                    {
                        if (_preambleToStrip[idx] != buffer[idx])
                        {
                            _preambleToStrip = null;
                            return;
                        }
                    }

                    buffer = buffer.Slice(_preambleToStrip.Length);
                }
                _preambleToStrip = null;
            }
        }
    }
}
