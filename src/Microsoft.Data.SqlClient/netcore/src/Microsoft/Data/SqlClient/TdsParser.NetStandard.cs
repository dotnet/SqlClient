// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class TdsParser
    {
        internal static void FillGuidBytes(Guid guid, Span<byte> buffer)
        {
            byte[] bytes = guid.ToByteArray();
            bytes.AsSpan().CopyTo(buffer);
        }

        internal static void FillDoubleBytes(double value, Span<byte> buffer)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            bytes.AsSpan().CopyTo(buffer);
        }

        internal static void FillFloatBytes(float value, Span<byte> buffer)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            bytes.AsSpan().CopyTo(buffer);
        }

        internal static Guid ConstructGuid(ReadOnlySpan<byte> bytes)
        {
            Debug.Assert(bytes.Length >= 16, "not enough bytes to set guid");
            byte[] temp = ArrayPool<byte>.Shared.Rent(16);
            bytes.CopyTo(temp.AsSpan());
            Guid retval = new Guid(temp);
            ArrayPool<byte>.Shared.Return(temp);
            return retval;
        }

        private sealed partial class TdsOutputStream
        {
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Debug.Assert(_parser._asyncWrite);
                ValidateWriteParameters(buffer, offset, count);

                StripPreamble(buffer, ref offset, ref count);

                Task task = null;
                if (count > 0)
                {
                    _parser.WriteInt(count, _stateObj); // write length of chunk
                    task = _stateObj.WriteByteArray(buffer, count, offset, canAccumulate: false);
                }

                return task ?? Task.CompletedTask;
            }
        }
    }
}
