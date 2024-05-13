using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.SqlValuesProcessing;
using Microsoft.Data.SqlClient.SqlClientX.TDS;
using Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.Streams
{
    internal static class TdsReadStreamExtensions
    {
        //
        // internal static async ValueTask<byte> ReadByteCastAsync(this TdsReadStream stream) => (byte)stream.ReadByte();

        /// <summary>
        /// Put this inside the Tds reader instead.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        internal static async ValueTask<byte> ReadByteAsync(this TdsReadStream stream, bool isAsync, CancellationToken ct = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(1);
            _ = isAsync? await stream.ReadAsync(buffer.AsMemory(0,1), ct) : 
                stream.Read(buffer.AsSpan().Slice(0,1));
            var result = buffer[0];
            ArrayPool<byte>.Shared.Return(buffer);
            return result;
        }

        internal static async ValueTask<string> ReadStringAsync(this TdsReadStream stream, int length, bool isAsync, CancellationToken ct = default)
        {
            int byteCount = length << 1;
            byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
            if (isAsync)
            {
                 await stream.ReadAsync(rented.AsMemory().Slice(0, byteCount), CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                stream.Read(rented.AsSpan().Slice(0, byteCount));
            }
            var result = Encoding.Unicode.GetString(rented,0, byteCount);
            ArrayPool<byte>.Shared.Return(rented);

            return result;
        }

        internal static async ValueTask<ushort> ReadUInt16Async(this TdsReadStream stream, bool isAsync, CancellationToken ct = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(2);
            if (isAsync)
            {
                await stream.ReadAsync(buffer.AsMemory().Slice(0, 2), ct);
            }
            else
            {
                stream.Read(buffer.AsSpan().Slice(0, 2));
            }
            var result = (ushort)((buffer[1] << 8) + buffer[0]);
            ArrayPool<byte>.Shared.Return(buffer);
            return result;
        }

        internal static async ValueTask<short> ReadInt16Async(this TdsReadStream stream, bool isAsync, CancellationToken ct = default)
        {
            int length = 2;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);

            _ = isAsync ? await stream.ReadAsync(buffer.AsMemory().Slice(0, length), ct) : 
                    stream.Read(buffer.AsSpan().Slice(0, length));

            var result = (short)((buffer[1] << 8) + buffer[0]);
            ArrayPool<byte>.Shared.Return(buffer);

            return result;
        }

        internal static async ValueTask<long> ReadInt64Async(this TdsReadStream stream, 
            bool isAsync, CancellationToken ct = default)
        {
            int size = sizeof(long);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeof(long));

            _ = isAsync ? await stream.ReadAsync(buffer.AsMemory(0, size), ct) :
                stream.Read(buffer.AsSpan().Slice(0, size));

            var result = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan().Slice(0, size));
            ArrayPool<byte>.Shared.Return(buffer);

            return result;
        }

        internal static async ValueTask<float> ReadSingleAsync(this TdsReadStream stream, bool isAsync, CancellationToken ct)
        {
            int size = 4;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

            _ = isAsync ? await stream.ReadAsync(buffer.AsMemory(0, size), ct) :
                stream.Read(buffer.AsSpan().Slice(0, size));

            var result = BitConverterCompatible.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan().Slice(0, size)));
            ArrayPool<byte>.Shared.Return(buffer);

            return result;
        }

        internal static async ValueTask<double> ReadDoubleAsync(this TdsReadStream stream, bool isAsync, CancellationToken ct)
        {
            int size = 4;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

            _ = isAsync ? await stream.ReadAsync(buffer.AsMemory(0, size), ct) :
                stream.Read(buffer.AsSpan().Slice(0, size));

            var result = BitConverterCompatible.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan().Slice(0, size)));
            ArrayPool<byte>.Shared.Return(buffer);

            return result;
        }


        internal static async ValueTask<simplesqlclient.SqlError> ProcessErrorAsync(this TdsReadStream stream, TdsToken token, bool isAsync, CancellationToken ct = default)
        {

            int number = await stream.ReadInt32Async(isAsync, ct);

            byte state = await stream.ReadByteAsync(isAsync, ct);
            byte errorClass = await stream.ReadByteAsync(isAsync, ct);

            Debug.Assert(((errorClass >= TdsEnums.MIN_ERROR_CLASS) && token.TokenType == TdsEnums.SQLERROR) ||
                          ((errorClass < TdsEnums.MIN_ERROR_CLASS) && token.TokenType == TdsEnums.SQLINFO), "class and token don't match!");
            ushort shortLen = await stream.ReadUInt16Async(isAsync);


            string message = await stream.ReadStringAsync(shortLen, isAsync);

            byte byteLen = await stream.ReadByteAsync(isAsync);

            string server = await stream.ReadStringAsync(byteLen, isAsync);

            byteLen = await stream.ReadByteAsync(isAsync);

            string procedure = await stream.ReadStringAsync(byteLen, isAsync);


            int line = await stream.ReadInt32Async(isAsync);

            int batchIndex = -1;

            simplesqlclient.SqlError error = new(number, state, errorClass, server, message, procedure, line, exception: null, batchIndex: batchIndex);
            return error;
        }

        internal static async ValueTask<int> ReadInt32Async(this TdsReadStream stream, bool isAsync, CancellationToken ct = default)
        {
            int size = sizeof(int);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

            _ = isAsync ? await stream.ReadAsync(buffer.AsMemory(0, size), ct) :
                stream.Read(buffer.AsSpan().Slice(0, size));

            var result = (buffer[3] << 24) + (buffer[2] << 16) + (buffer[1] << 8) + buffer[0];
            ArrayPool<byte>.Shared.Return(buffer);

            return result;
        }

        internal static async ValueTask<uint> ReadUInt32Async(this TdsReadStream stream,
            bool isAsync,
            CancellationToken ct = default)
        {
            int size = sizeof(uint);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

            _ = isAsync ? await stream.ReadAsync(buffer.AsMemory(0, size), ct).ConfigureAwait(false) :
                stream.Read(buffer.AsSpan()[..size]);

            var result = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan()[..size]);
            ArrayPool<byte>.Shared.Return(buffer);

            return result;
        }

        internal static async ValueTask<Tuple<int, byte[]>> ReadPlpBytesAsync(this TdsReadStream stream,
            int offset, int len, StreamExecutionState executionState,
            bool isAsync,
            CancellationToken ct)
        {
            int totalBytesRead;
            int bytesRead;
            int bytesLeft;
            byte[] buff = null;
            byte[] newbuf;

            if (executionState.LongLenLeft == 0)
            {
                Debug.Assert(executionState.LongLenLeft == 0);
                
                buff = Array.Empty<byte>();
                return Tuple.Create(0, buff);
            }

            Debug.Assert(executionState.LongLenLeft != TdsEnums.SQL_PLP_NULL, "Out of sync plp read request");
            Debug.Assert((buff == null && offset == 0) || (buff?.Length >= offset + len), "Invalid length sent to ReadPlpBytes()!");

            bytesLeft = len;

            // If total length is known up front, allocate the whole buffer in one shot instead of realloc'ing and copying over each time
            if (buff == null && executionState.LongLen != TdsEnums.SQL_PLP_UNKNOWNLEN)
            {

                if ((ulong)(buff?.Length ?? 0) != executionState.LongLen)
                {
                    // if the buffer is null or the wrong length create one to use
                    buff = new byte[(Math.Min((int)executionState.LongLen, len))];
                }
            }

            if (executionState.LongLenLeft == 0)
            {
                _ = await stream.ReadPlpLengthAsync(executionState, false, isAsync, ct).ConfigureAwait(false);
                
                if (executionState.LongLenLeft == 0)
                { // Data read complete
                    return Tuple.Create(0, buff);
                }
            }

            if (buff == null)
            {
                buff = new byte[executionState.LongLenLeft];
            }

            totalBytesRead = 0;

            while (bytesLeft > 0)
            {
                int bytesToRead = (int)Math.Min(executionState.LongLenLeft, (ulong)bytesLeft);
                if (buff.Length < (offset + bytesToRead))
                {
                    // Grow the array
                    newbuf = new byte[offset + bytesToRead];
                    Buffer.BlockCopy(buff, 0, newbuf, 0, offset);
                    buff = newbuf;
                }

                //bool result = TryReadByteArray(buff.AsSpan(offset), bytesToRead, out bytesRead);
                
                if (isAsync)
                {
                    bytesRead = await stream.ReadAsync(buff.AsMemory(offset, bytesToRead), ct).ConfigureAwait(false);
                }
                else
                {
                    bytesRead = stream.Read(buff.AsSpan(offset, bytesToRead));
                }
                Debug.Assert(bytesRead <= bytesLeft, "Read more bytes than we needed");
                //Debug.Assert((ulong)bytesRead <= executionState.LongLenLeft, "Read more bytes than is available");
                
                bytesLeft -= bytesRead;
                offset += bytesRead;
                totalBytesRead += bytesRead;
                executionState.LongLenLeft -= (ulong)bytesRead;

                if (executionState.LongLenLeft == 0)
                {
                    // Read the next chunk or cleanup state if hit the end
                    _ = await stream.ReadPlpLengthAsync(executionState, false, isAsync, ct).ConfigureAwait(false);
                }

                //AssertValidState();

                // Catch the point where we read the entire plp data stream and clean up state
                if (executionState.LongLenLeft == 0)   // Data read complete
                    break;
            }
            return Tuple.Create(totalBytesRead, buff);
        }

        internal static async ValueTask<ulong> SkipPlpValueAsync(this TdsReadStream stream,
            ulong cb,
            StreamExecutionState executionState,
            bool isAsync,
            CancellationToken ct)
        {
            int bytesSkipped;
            ulong totalBytesSkipped = 0;

            if (executionState.LongLenLeft == 0)
            {
                _ = await stream.ReadPlpLengthAsync(executionState, false, isAsync, ct).ConfigureAwait(false);
            }

            while ((totalBytesSkipped < cb) &&
                    (executionState.LongLenLeft > 0))
            {
                if (executionState.LongLenLeft > int.MaxValue)
                    bytesSkipped = int.MaxValue;
                else
                    bytesSkipped = (int)executionState.LongLenLeft;
                bytesSkipped = ((cb - totalBytesSkipped) < (ulong)bytesSkipped) ? (int)(cb - totalBytesSkipped) : bytesSkipped;

                await stream.SkipBytesAsync(bytesSkipped, isAsync, ct).ConfigureAwait(false);
                executionState.LongLenLeft -= (ulong)bytesSkipped;
                totalBytesSkipped += (ulong)bytesSkipped;

                if (executionState.LongLenLeft == 0)
                {
                    _ = await stream.ReadPlpLengthAsync(executionState, false, isAsync, ct).ConfigureAwait(false);
                }
            }
            return totalBytesSkipped;
        }

        internal static async ValueTask<string> ReadStringWithEncodingAsync(
            this TdsReadStream stream, 
            int length, 
            System.Text.Encoding encoding, 
            bool isPlp,
            StreamExecutionState executionState,
            bool isAsync, 
            CancellationToken ct = default)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadStringWithEncoding");  // you need to setup for a thread abort somewhere before you call this method

            if (null == encoding)
            {
                // Need to skip the current column before throwing the error - this ensures that the state shared between this and the data reader is consistent when calling DrainData
                if (isPlp)
                {
                    await stream.SkipPlpValueAsync((ulong)length, executionState, isAsync, ct).ConfigureAwait(false);
                }
                else
                {
                    await stream.SkipBytesAsync(8, isAsync, ct).ConfigureAwait(false);
                }

                throw new Exception("Unsupported encoding exception");
            }
            byte[] buf = null;
            int offset = 0;

            if (isPlp)
            {
                //await stream.ReadPlpBytesAsync();
                (length, buf) = await stream.ReadPlpBytesAsync(
                    0,
                    int.MaxValue,
                    executionState,
                    isAsync,
                    ct).ConfigureAwait(false);
            }
            else
            {
                buf = ArrayPool<byte>.Shared.Rent(length);
                _ = isAsync ? await stream.ReadAsync(buf.AsMemory(0, length), ct) :
                    stream.Read(buf.AsSpan().Slice(0, length));
            }

            // BCL optimizes to not use char[] underneath
            return encoding.GetString(buf, offset, length);
        }

        internal static async ValueTask<TdsToken> ProcessTokenAsync(this TdsReadStream stream, bool isAsync, CancellationToken ct)
        {
            TdsToken token = new();
            byte tokenByte = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            token.TokenType = tokenByte;
            // TODO: Validate token type
            //if (!Enum.IsDefined(typeof(TdsTokens), tokenByte))
            {
                //  throw new Exception($"Invalid token type {tokenByte}");
            }

            // Figure out the token type. It is going to be one of 4 kinds
            // Fixed Len      xx11xxxx
            // Variable Len   xx10xxxx
            // Zero length    xx01xxxx
            // Variable count tokens xx00xxxx

            // Figure out the length by reading the data length size from the token
            bool specialToken = false;
            switch (token.TokenType)
            {
                // Handle special tokens.
                case TdsTokens.SQLFEATUREEXTACK:
                    token.Length = -1;
                    specialToken = true;
                    break;
                case TdsTokens.SQLSESSIONSTATE:
                    token.Length = await stream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                    specialToken = true;
                    break;
                case TdsTokens.SQLFEDAUTHINFO:
                    token.Length = await stream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                    specialToken = true;
                    break;
                case TdsTokens.SQLUDT:
                case TdsTokens.SQLRETURNVALUE:
                    token.Length = -1;
                    specialToken = true;
                    break;
                case TdsTokens.SQLXMLTYPE:
                    token.Length = await stream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                    specialToken = true;
                    break;

                default:
                    specialToken = false;
                    break;
            }

            int tokenLength = 0;
            if (!specialToken)
            {
                switch (token.TokenType & TdsEnums.SQLLenMask)
                {
                    case TdsEnums.SQLFixedLen:
                        tokenLength = (0x01 << ((token.TokenType & 0x0c) >> 2)) & 0xff;
                        break;
                    case TdsEnums.SQLZeroLen:
                        tokenLength = 0;
                        break;
                    case TdsEnums.SQLVarLen:
                    case TdsEnums.SQLVarCnt:
                        if (0 != (token.TokenType & 0x80))
                        {
                            tokenLength = await stream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                            break;
                        }
                        else if (0 == (token.TokenType & 0x0c))
                        {
                            tokenLength = await stream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                            break;
                        }
                        else
                        {
                            tokenLength = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                            break;
                        }
                    default:
                        Debug.Fail("Unknown token length!");
                        tokenLength = 0;
                        break;
                }
                token.Length = tokenLength;
            }
            // Read the length

            // Read the data

            return token;
        }
    }
}
