using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX
{
    internal class BufferReader
    {
        private Stream transportStream;
        private byte[] inBuffer;
        private int offset = 0;
        internal int lengthRead = 0;
        private byte[] headerBuffer = new byte[TdsConstants.PACKET_HEADER_SIZE];

        public BufferReader(Stream transportStream)
        {
            inBuffer = new byte[TdsConstants.DEFAULT_LOGIN_PACKET_SIZE];
            this.transportStream = transportStream;
        }

        /// <summary>
        /// Initiates a packet read from the stream.
        /// </summary>
        /// <returns></returns>
        internal int ReadPacket()
        {
            this.lengthRead = this.transportStream.Read(inBuffer);
            return this.lengthRead;
        }

        private void EnsureBytes(int count)
        {
            // If the required bytes are not available in the buffer, read more data from the stream only 
            // if the the buffer has enough space for the required count of bytes.
            if (offset + count > lengthRead)
            {
                // If the caller requests a length, which will exceed the buffer size, then we need to throw an exception.
                if (offset + count < inBuffer.Length)
                {
                    // The required bytes are not available in the buffer.
                    // Read more data from the stream.
                    // TODO: Need a case where the stream has more data than the buffer size. This would lead 
                    // to dataloss since the underlying stream will be read, and then discarded.
                    int bytesRead = this.transportStream.ReadAtLeast(inBuffer.AsSpan(offset), count);
                    lengthRead += bytesRead;
                }
                else
                {
                    // The buffer is full. 
                    // We need to read more data from the stream.
                    // We need to resize the buffer.
                    throw new Exception("Buffer is full. Cannot read more data.");
                }
            }
        }


        internal byte ReadByte()
        {
            EnsureBytes(1);
            return inBuffer[offset++];
        }

        internal void SkipBytes(int count)
        {
            EnsureBytes(count);
            offset += count;
        }

        /// <summary>
        /// Check the input buffer for the packet header.
        /// The packet header may be split across multiple reads.
        /// Keep reading from the stream until the header is complete.
        /// </summary>
        internal TdsPacketHeader ProcessPacketHeader()
        {
            TdsPacketHeader header;
            EnsureBytes(TdsConstants.PACKET_HEADER_SIZE);
            // header.PacketType = ReadByte();
            header.PacketType = ReadByte();
            header.Status = ReadByte();
            header.Length = (ushort)((ushort)(ReadByte() << 8 | ReadByte()) - TdsConstants.PACKET_HEADER_SIZE);
            header.Spid = (ushort)(ReadByte() << 8 | ReadByte());
            header.PacketNumber = ReadByte();
            header.Window = ReadByte();
            return header;
        }

        internal void ReadByteArray(Span<byte> outputArray)
        {
            EnsureBytes(outputArray.Length);
            inBuffer.AsSpan(offset, outputArray.Length).CopyTo(outputArray);
            offset += outputArray.Length;
        }

        internal void UpdateStream(Stream sslStream)
        {
            this.transportStream = sslStream;
        }

        internal void ResetPacket()
        {
            this.offset = 0;
            this.lengthRead = 0;
        }


        internal TdsToken ProcessToken()
        {
            TdsToken token = new();
            byte tokenByte = ReadByte();
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
                    token.Length = ReadInt32();
                    specialToken = true;
                    break;
                case TdsTokens.SQLFEDAUTHINFO:
                    token.Length = ReadInt32();
                    specialToken = true;
                    break;
                case TdsTokens.SQLUDT:
                case TdsTokens.SQLRETURNVALUE:
                    token.Length = -1;
                    specialToken = true;
                    break;
                case TdsTokens.SQLXMLTYPE:
                    token.Length = ReadUInt16();
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
                            tokenLength = ReadUInt16();
                            break;
                        }
                        else if (0 == (token.TokenType & 0x0c))
                        {
                            tokenLength = ReadInt32();
                            break;
                        }
                        else
                        {
                            byte value = ReadByte();
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

        public ushort ReadUInt16()
        {
            Span<byte> buffer = stackalloc byte[2];
            ReadByteArray(buffer);
            return (ushort)((buffer[1] << 8) + buffer[0]);
        }

        internal int ReadInt32()
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            ReadByteArray(buffer);
            return (buffer[3] << 24) + (buffer[2] << 16) + (buffer[1] << 8) + buffer[0];
        }

        internal simplesqlclient.SqlError ProcessError(TdsToken token)
        {

            int number = ReadInt32();

            byte state = ReadByte();
            byte errorClass = ReadByte();

            Debug.Assert(((errorClass >= TdsEnums.MIN_ERROR_CLASS) && token.TokenType == TdsEnums.SQLERROR) ||
                          ((errorClass < TdsEnums.MIN_ERROR_CLASS) && token.TokenType == TdsEnums.SQLINFO), "class and token don't match!");
            ushort shortLen = ReadUInt16();


            string message = ReadString(shortLen);

            byte byteLen = ReadByte();

            string server = ReadString(byteLen);

            byteLen = ReadByte();

            string procedure = ReadString(byteLen);


            int line = ReadUInt16();

            int batchIndex = -1;

            simplesqlclient.SqlError error = new(number, state, errorClass, server, message, procedure, line, exception: null, batchIndex: batchIndex);
            return error;
        }

        internal string ReadString(ushort shortLen)
        {
            int byteCount = shortLen << 1;
            Span<byte> stringBytes = stackalloc byte[byteCount];
            ReadByteArray(stringBytes);
            return Encoding.Unicode.GetString(stringBytes);
        }

        internal long ReadInt64()
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            ReadByteArray(buffer);
            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }
    }
}
