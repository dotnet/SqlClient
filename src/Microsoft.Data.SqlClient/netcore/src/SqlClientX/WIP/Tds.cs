using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.WIP
{
    internal interface ITokenProcessorCollection : ICollection<ITokenProcessor>
    {
        void RetriveProcessor(TdsToken token);
    }
    
    internal interface ITokenProcessor
    {
        void ProcessToken(TdsToken token);
    }

    internal abstract class TdsTokenStream : Stream
    {
        public TdsToken Token { get; internal set;  }

        public abstract byte SupportedToken { get; }

        public TdsTokenStream(TdsToken token)
        {
            if (token.TokenType != SupportedToken)
            {
                throw new InvalidOperationException($"Token {token.TokenType} is not supported by this stream.");
            }
            Token = token;
        }

        public override bool CanRead => throw new NotImplementedException();

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }

    internal class Tds : IDisposable, IAsyncDisposable
    {
        private TdsWriteStream _writeStream;
        private TdsReadStream _readStream;

        
        internal TdsToken PositionedAt { get; private set; }
        private bool ExpectData { get; set; } = true;

        /// <summary>
        /// Read until we arrive at a token.
        /// </summary>
        /// <returns></returns>
        public bool ReadToken()
        {
            // Read the token and the length. Do this by peeking at the next byte. 
            // if it is a valid token, then great, we can read the token and the length.
            // and the Tds Parser will position itself after the byte position of the token. 

            // At this point, read the token type and the length only. Do not read the data from the token
            if (ExpectData)
            {
                TdsToken token = _readStream.ProcessToken();
                PositionedAt = token;
            }
            return false;
        }

        public SqlLoginAck ReadLoginAckToken()
        {

        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}
