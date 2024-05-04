using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.Streams
{
    internal static class TdsReadStreamExtensions
    {
        internal static byte ReadByteCast(this TdsReadStream stream) => (byte)stream.ReadByte();
            
        internal static string ReadString(this TdsReadStream stream, ushort shortLen)
        {
            int byteCount = shortLen << 1;
            Span<byte> stringBytes = stackalloc byte[byteCount];
            stream.Read(stringBytes);
            return Encoding.Unicode.GetString(stringBytes);
        }

        internal static string ReadString(this TdsReadStream stream, int length)
        {
            int byteCount = length << 1;
            Span<byte> stringBytes = stackalloc byte[byteCount];
            stream.Read(stringBytes);
            return Encoding.Unicode.GetString(stringBytes);
        }

        internal static ushort ReadUInt16(this TdsReadStream stream)
        {
            Span<byte> buffer = stackalloc byte[2];
            stream.Read(buffer);
            return (ushort)((buffer[1] << 8) + buffer[0]);
        }

        internal static short ReadInt16(this TdsReadStream stream)
        {
            Span<byte> buffer = stackalloc byte[2];
            stream.Read(buffer);
            return (short)((buffer[1] << 8) + buffer[0]);
        }

        internal static long ReadInt64(this TdsReadStream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            stream.Read(buffer);
            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        internal static simplesqlclient.SqlError ProcessError(this TdsReadStream stream, TdsToken token)
        {

            int number = stream.ReadInt32();

            byte state = (byte)stream.ReadByte();
            byte errorClass = (byte)stream.ReadByte();

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

        internal static int ReadInt32(this TdsReadStream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            stream.Read(buffer);
            return (buffer[3] << 24) + (buffer[2] << 16) + (buffer[1] << 8) + buffer[0];
        }

        internal static uint ReadUInt32(this TdsReadStream stream)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            stream.Read(buffer);
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        internal static TdsToken ProcessToken(this TdsReadStream stream)
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
                    token.Length = stream.ReadInt32();
                    specialToken = true;
                    break;
                case TdsTokens.SQLFEDAUTHINFO:
                    token.Length = stream.ReadInt32();
                    specialToken = true;
                    break;
                case TdsTokens.SQLUDT:
                case TdsTokens.SQLRETURNVALUE:
                    token.Length = -1;
                    specialToken = true;
                    break;
                case TdsTokens.SQLXMLTYPE:
                    token.Length = stream.ReadUInt16();
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
                            tokenLength = stream.ReadUInt16();
                            break;
                        }
                        else if (0 == (token.TokenType & 0x0c))
                        {
                            tokenLength = stream.ReadInt32();
                            break;
                        }
                        else
                        {
                            byte value = (byte)stream.ReadByte();
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
