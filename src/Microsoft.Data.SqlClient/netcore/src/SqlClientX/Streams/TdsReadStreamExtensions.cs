using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.Streams
{
    internal static async class TdsReadStreamExtensions
    {
        internal static async byte ReadByteCastAsync(this TdsReadStream stream) => (byte)stream.ReadByte();
            
        internal static async string ReadStringAsync(this TdsReadStream stream, ushort shortLen)
        {
            int byteCount = shortLen << 1;
            Span<byte> stringBytes = stackalloc byte[byteCount];
            stream.Read(stringBytes);
            return Encoding.Unicode.GetString(stringBytes);
        }

        internal static async ValueTask<string> ReadStringAsync(this TdsReadStream stream, int length, bool isAsync, CancellationToken ct = default)
        {
            int byteCount = length << 1;
            byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
            if (isAsync)
            {
                await stream.ReadAsync(new Memory<byte>(rented), CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                stream.Read(rented);
            }
            
            return Encoding.Unicode.GetString(rented);
        }

        internal static async ValueTask<ushort> ReadUInt16Async(this TdsReadStream stream, bool isAsync, CancellationToken ct = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(2);
            if (isAsync)
            {
                await stream.ReadAsync(new Memory<byte>(buffer), ct);
            }
            else
            { 
                stream.Read(buffer); 
            }
            return (ushort)((buffer[1] << 8) + buffer[0]);
        }

        internal static async short ReadInt16Async(this TdsReadStream stream, bool isAsync, CancellationToken ct = default)
        {
            Span<byte> buffer = stackalloc byte[2];
            stream.Read(buffer);
            return (short)((buffer[1] << 8) + buffer[0]);
        }

        internal static async long ReadInt64Async(this TdsReadStream stream, bool isAsync, CancellationToken ct = default)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            stream.Read(buffer);
            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        internal static async ValueTask<simplesqlclient.SqlError> ProcessErrorAsync(this TdsReadStream stream, TdsToken token, bool isAsync, CancellationToken ct = default)
        {

            int number = await stream.ReadInt32Async(isAsync, ct);

            byte state = (byte)stream.ReadByteAsync(isAsync, ct);
            byte errorClass = (byte)stream.ReadByteAsync(isAsync, ct);

            Debug.Assert(((errorClass >= TdsEnums.MIN_ERROR_CLASS) && token.TokenType == TdsEnums.SQLERROR) ||
                          ((errorClass < TdsEnums.MIN_ERROR_CLASS) && token.TokenType == TdsEnums.SQLINFO), "class and token don't match!");
            ushort shortLen = stream.ReadUInt16();


            string message = stream.ReadString(shortLen);

            byte byteLen = (byte)stream.ReadByte();

            string server = stream.ReadString(byteLen);

            byteLen = (byte)stream.ReadByte();

            string procedure = stream.ReadString(byteLen);


            int line = stream.ReadInt32();

            int batchIndex = -1;

            simplesqlclient.SqlError error = new(number, state, errorClass, server, message, procedure, line, exception: null, batchIndex: batchIndex);
            return error;
        }

        internal static async ValueTask<int> ReadInt32Async(this TdsReadStream stream, bool isAsync, CancellationToken ct = default)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            stream.Read(buffer);
            return (buffer[3] << 24) + (buffer[2] << 16) + (buffer[1] << 8) + buffer[0];
        }

        internal static async ValueTask<uint> ReadUInt32Async(this TdsReadStream stream,
            bool isAsync,
            CancellationToken ct = default)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            stream.Read(buffer);
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        internal static async string ReadStringWithEncodingAsync(this TdsReadStream stream, 
            int length, 
            System.Text.Encoding encoding, 
            bool isPlp, 
            bool isAsync, 
            CancellationToken ct = default)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadStringWithEncoding");  // you need to setup for a thread abort somewhere before you call this method

            if (null == encoding)
            {
                // Need to skip the current column before throwing the error - this ensures that the state shared between this and the data reader is consistent when calling DrainData
                if (isPlp)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    stream.Read(stackalloc byte[8]);
                }

                throw new Exception("Unsupported encoding exception");
            }
            byte[] buf = null;
            int offset = 0;

            if (isPlp)
            {
                throw new NotImplementedException();
            }
            else
            {
                buf = new byte[length];
                stream.Read(buf);
            }

            // BCL optimizes to not use char[] underneath
            return encoding.GetString(buf, offset, length);
        }

        internal static async TdsToken ProcessTokenAsync(this TdsReadStream stream, bool isAsync, CancellationToken ct = default)
        {
            TdsToken token = new();
            byte tokenByte = (byte)stream.ReadByte();
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
                    token.Length = stream.ReadInt32Async(isAsync);
                    specialToken = true;
                    break;
                case TdsTokens.SQLFEDAUTHINFO:
                    token.Length = stream.ReadInt32Async(isAsync);
                    specialToken = true;
                    break;
                case TdsTokens.SQLUDT:
                case TdsTokens.SQLRETURNVALUE:
                    token.Length = -1;
                    specialToken = true;
                    break;
                case TdsTokens.SQLXMLTYPE:
                    token.Length = await stream.ReadUInt16Async(isAsync);
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
                            tokenLength = await stream.ReadUInt16Async(isAsync);
                            break;
                        }
                        else if (0 == (token.TokenType & 0x0c))
                        {
                            tokenLength = stream.ReadInt32Async(isAsync);
                            break;
                        }
                        else
                        {
                            tokenLength = (byte)stream.ReadByteAsync(isAsync);
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
