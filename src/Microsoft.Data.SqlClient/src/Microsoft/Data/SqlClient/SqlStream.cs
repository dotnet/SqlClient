// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Data.Common;
using Microsoft.Data.SqlTypes;

namespace Microsoft.Data.SqlClient
{
    sealed internal class SqlStream : Stream
    {
        private SqlDataReader _reader; // reader we will stream off
        private readonly int _columnOrdinal;
        private long _bytesCol;
        private int _bom;
        private byte[] _bufferedData;
        private readonly bool _processAllRows;
        private readonly bool _advanceReader;
        private bool _readFirstRow = false;
        private bool _endOfColumn = false;

        internal SqlStream(SqlDataReader reader, bool addByteOrderMark, bool processAllRows) :
            this(0, reader, addByteOrderMark, processAllRows, true)
        {
        }

        internal SqlStream(int columnOrdinal, SqlDataReader reader, bool addByteOrderMark, bool processAllRows, bool advanceReader)
        {
            _columnOrdinal = columnOrdinal;
            _reader = reader;
            _bom = addByteOrderMark ? 0xfeff : 0;
            _processAllRows = processAllRows;
            _advanceReader = advanceReader;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw ADP.NotSupported();

        public override long Position
        {
            get => throw ADP.NotSupported();
            set => throw ADP.NotSupported();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _advanceReader && _reader != null && !_reader.IsClosed)
                {
                    _reader.Close();
                }
                _reader = null;
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override void Flush() => throw ADP.NotSupported();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int intCount = 0;
            int cBufferedData = 0;

            if (_reader == null)
            {
                throw ADP.StreamClosed();
            }
            if (buffer == null)
            {
                throw ADP.ArgumentNull(nameof(buffer));
            }
            if ((offset < 0) || (count < 0))
            {
                throw ADP.ArgumentOutOfRange(string.Empty, (offset < 0 ? nameof(offset) : nameof(count)));
            }
            if (buffer.Length - offset < count)
            {
                throw ADP.ArgumentOutOfRange(nameof(count));
            }

            // Need to find out if we should add byte order mark or not. 
            // We need to add this if we are getting ntext xml, not if we are getting binary xml
            // Binary Xml always begins with the bytes 0xDF and 0xFF
            // If we aren't getting these, then we are getting unicode xml
            if (_bom > 0)
            {
                // Read and buffer the first two bytes
                _bufferedData = new byte[2];
                cBufferedData = ReadBytes(_bufferedData, 0, 2);
                // Check to see if we should add the byte order mark
                if ((cBufferedData < 2) || ((_bufferedData[0] == 0xDF) && (_bufferedData[1] == 0xFF)))
                {
                    _bom = 0;
                }
                while (count > 0)
                {
                    if (_bom > 0)
                    {
                        buffer[offset] = (byte)_bom;
                        _bom >>= 8;
                        offset++;
                        count--;
                        intCount++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (cBufferedData > 0)
            {
                while (count > 0)
                {
                    buffer[offset++] = _bufferedData[0];
                    intCount++;
                    count--;
                    if ((cBufferedData > 1) && (count > 0))
                    {
                        buffer[offset++] = _bufferedData[1];
                        intCount++;
                        count--;
                        break;
                    }
                }
                _bufferedData = null;
            }

            intCount += ReadBytes(buffer, offset, count);

            return intCount;
        }

        private static bool AdvanceToNextRow(SqlDataReader reader)
        {
            Debug.Assert(reader != null && !reader.IsClosed);

            // this method skips empty result sets
            do
            {
                if (reader.Read())
                {
                    return true;
                }
            } while (reader.NextResult());

            // no more rows
            return false;
        }

        private int ReadBytes(byte[] buffer, int offset, int count)
        {
            bool gotData = true;
            int intCount = 0;

            if (_reader.IsClosed || _endOfColumn)
            {
                return 0;
            }
            try
            {
                while (count > 0)
                {
                    // if no bytes were read, get the next row
                    if (_advanceReader && (0 == _bytesCol))
                    {
                        gotData = false;

                        if (_readFirstRow && !_processAllRows)
                        {
                            // for XML column, stop processing after the first row
                            // no op here - reader is closed after the end of this loop
                        }
                        else if (AdvanceToNextRow(_reader))
                        {
                            _readFirstRow = true;

                            if (_reader.IsDBNull(_columnOrdinal))
                            {
                                // Handle row with DBNULL as empty data
                                // for XML column, processing is stopped on the next loop since _readFirstRow is true
                                continue;
                            }

                            // the value is not null, read it
                            gotData = true;
                        }
                        // else AdvanceToNextRow has returned false - no more rows or result sets remained, stop processing
                    }

                    if (gotData)
                    {
                        int cb = (int)_reader.GetBytesInternal(_columnOrdinal, _bytesCol, buffer, offset, count);
                        if (cb < count)
                        {
                            _bytesCol = 0;
                            gotData = false;
                            if (!_advanceReader)
                            {
                                _endOfColumn = true;
                            }
                        }
                        else
                        {
                            Debug.Assert(cb == count);
                            _bytesCol += cb;
                        }

                        // we are guaranteed that cb is < Int32.Max since we always pass in count which is of type Int32 to
                        // our getbytes interface
                        count -= cb;
                        offset += cb;
                        intCount += cb;
                    }
                    else
                    {
                        break; // no more data available, we are done
                    }
                }
                if (!gotData && _advanceReader)
                {
                    _reader.Close();    // Need to close the reader if we are done reading
                }
            }
            catch (Exception e)
            {
                if (_advanceReader && ADP.IsCatchableExceptionType(e))
                {
                    _reader.Close();
                }
                throw;
            }

            return intCount;
        }

        internal XmlReader ToXmlReader(bool async = false)
        {
            return SqlTypeWorkarounds.SqlXmlCreateSqlXmlReader(this, closeInput: true, async: async);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw ADP.NotSupported();

        public override void SetLength(long value) => throw ADP.NotSupported();

        public override void Write(byte[] buffer, int offset, int count) => throw ADP.NotSupported();
    }


    // XmlTextReader does not read all the bytes off the network buffers, so we have to cache it here in the random access
    // case. This causes double buffering and is a perf hit, but this is not the high perf way for accessing this type of data.
    // In the case of sequential access, we do not have to do any buffering since the XmlTextReader we return can become 
    // invalid as soon as we move off the current column.
    sealed internal class SqlCachedStream : Stream
    {
        private int _currentPosition;   // Position within the current array byte
        private int _currentArrayIndex; // Index into the _cachedBytes List
        private List<byte[]> _cachedBytes;
        private long _totalLength;

        // Reads off from the network buffer and caches bytes. Only reads one column value in the current row.
        internal SqlCachedStream(SqlCachedBuffer sqlBuf)
        {
            _cachedBytes = sqlBuf.CachedBytes;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => TotalLength;

        public override long Position
        {
            get
            {
                long pos = 0;
                if (_currentArrayIndex > 0)
                {
                    for (int ii = 0; ii < _currentArrayIndex; ii++)
                    {
                        pos += _cachedBytes[ii].Length;
                    }
                }
                pos += _currentPosition;
                return pos;
            }
            set
            {
                if (_cachedBytes == null)
                {
                    throw ADP.StreamClosed(ADP.ParameterSetPosition);
                }
                SetInternalPosition(value, ADP.ParameterSetPosition);
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _cachedBytes != null)
                {
                    _cachedBytes.Clear();
                }

                _cachedBytes = null;
                _currentPosition = 0;
                _currentArrayIndex = 0;
                _totalLength = 0;
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override void Flush() => throw ADP.NotSupported();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int cb;
            int intCount = 0;

            if (_cachedBytes == null)
            {
                throw ADP.StreamClosed();
            }

            if (buffer == null)
            {
                throw ADP.ArgumentNull(nameof(buffer));
            }

            if ((offset < 0) || (count < 0))
            {
                throw ADP.ArgumentOutOfRange(string.Empty, (offset < 0 ? nameof(offset) : nameof(count)));
            }

            if (buffer.Length - offset < count)
            {
                throw ADP.ArgumentOutOfRange(nameof(count));
            }

            if (_cachedBytes.Count <= _currentArrayIndex)
            {
                return 0;       // Everything is read!
            }

            while (count > 0)
            {
                if (_cachedBytes[_currentArrayIndex].Length <= _currentPosition)
                {
                    _currentArrayIndex++;       // We are done reading this chunk, go to next
                    if (_cachedBytes.Count > _currentArrayIndex)
                    {
                        _currentPosition = 0;
                    }
                    else
                    {
                        break;
                    }
                }
                cb = _cachedBytes[_currentArrayIndex].Length - _currentPosition;
                if (cb > count)
                {
                    cb = count;
                }

                Buffer.BlockCopy(_cachedBytes[_currentArrayIndex], _currentPosition, buffer, offset, cb);
                _currentPosition += cb;
                count -= cb;
                offset += cb;
                intCount += cb;
            }

            return intCount;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long pos = 0;

            if (_cachedBytes == null)
            {
                throw ADP.StreamClosed();
            }

            switch (origin)
            {
                case SeekOrigin.Begin:
                    SetInternalPosition(offset, nameof(offset));
                    break;

                case SeekOrigin.Current:
                    pos = offset + Position;
                    SetInternalPosition(pos, nameof(offset));
                    break;

                case SeekOrigin.End:
                    pos = TotalLength + offset;
                    SetInternalPosition(pos, nameof(offset));
                    break;

                default:
                    throw ADP.InvalidSeekOrigin(nameof(offset));
            }
            return pos;
        }

        public override void SetLength(long value) => throw ADP.NotSupported();

        public override void Write(byte[] buffer, int offset, int count) => throw ADP.NotSupported();

        private void SetInternalPosition(long lPos, string argumentName)
        {
            long pos = lPos;

            if (pos < 0)
            {
                throw new ArgumentOutOfRangeException(argumentName);
            }
            for (int ii = 0; ii < _cachedBytes.Count; ii++)
            {
                if (pos > _cachedBytes[ii].Length)
                {
                    pos -= _cachedBytes[ii].Length;
                }
                else
                {
                    _currentArrayIndex = ii;
                    _currentPosition = (int)pos;
                    return;
                }
            }
            if (pos > 0)
            {
                throw new ArgumentOutOfRangeException(argumentName);
            }
        }

        private long TotalLength
        {
            get
            {
                if ((_totalLength == 0) && (_cachedBytes != null))
                {
                    long pos = 0;
                    for (int ii = 0; ii < _cachedBytes.Count; ii++)
                    {
                        pos += _cachedBytes[ii].Length;
                    }
                    _totalLength = pos;
                }
                return _totalLength;
            }
        }
    }

    sealed internal class SqlStreamingXml
    {
        private readonly int _columnOrdinal;   // changing this is only done through the ctor, so it is safe to be readonly
        private SqlDataReader _reader;         // reader we will stream off, becomes null when closed
        private XmlReader _xmlReader;          // XmlReader over the current column, becomes null when closed

        private string _currentTextNode;       // rolling buffer of text to deliver
        private int _textNodeIndex;            // index in _currentTextNode
        private char? _pendingHighSurrogate;   // pending high surrogate for split surrogate pairs
        private long _charsReturned;           // total chars returned
        private bool _canReadChunk;            // XmlReader.CanReadValueChunk

        public SqlStreamingXml(int columnOrdinal, SqlDataReader reader)
        {
            _columnOrdinal = columnOrdinal;
            _reader = reader;
        }

        public int ColumnOrdinal => _columnOrdinal;

        public void Close()
        {
            _xmlReader?.Dispose();
            _xmlReader = null;
            _reader = null;

            _currentTextNode = null;
            _textNodeIndex = 0;
            _pendingHighSurrogate = null;
            _charsReturned = 0;
            _canReadChunk = false;
        }

        public long GetChars(long dataIndex, char[] buffer, int bufferIndex, int length)
        {
            if (_reader == null)
            {
                throw new ObjectDisposedException(nameof(SqlStreamingXml));
            }

            if (buffer == null)
            {
                return -1;
            }

            if (length == 0)
            {
                return 0;
            }

            if (dataIndex < _charsReturned)
            {
                throw new InvalidOperationException($"Non-sequential read: requested {dataIndex}, already returned {_charsReturned}");
            }

            EnsureReaderInitialized();

            // Skip to requested dataIndex
            long skip = dataIndex - _charsReturned;
            while (skip > 0)
            {
                char discard;
                if (!TryReadNextChar(out discard))
                {
                    return 0; // EOF
                }

                skip--;
                _charsReturned++;
            }

            // Read chars into buffer
            int copied = 0;
            while (copied < length)
            {
                char c;
                if (!TryReadNextChar(out c))
                {
                    break;
                }

                buffer[bufferIndex + copied] = c;
                copied++;
                _charsReturned++;
            }

            return copied;
        }

        /// <summary>
        /// Initializes the XML reader if it has not already been initialized, ensuring it is ready for reading
        /// operations.
        /// </summary>
        /// <remarks>
        /// This method prepares the XML reader for use by creating and assigning a new instance
        /// if necessary. It should be called before attempting to read XML data to guarantee that the reader is
        /// available and properly configured.
        /// </remarks>
        private void EnsureReaderInitialized()
        {
            if (_xmlReader != null)
            {
                return;
            }

            var sqlStream = new SqlStream(_columnOrdinal, _reader, addByteOrderMark: true, processAllRows: false, advanceReader: false);
            _xmlReader = sqlStream.ToXmlReader();
            _canReadChunk = _xmlReader.CanReadValueChunk;
        }

        /// <summary>
        /// Progressively fetches the next char from the XmlReader, filling the current text node buffer as necessary.
        /// Handles surrogate pairs that may be split across text nodes.
        /// </summary>
        private bool TryReadNextChar(out char c)
        {
            // Deliver pending high surrogate first
            if (_pendingHighSurrogate.HasValue)
            {
                c = _pendingHighSurrogate.Value;
                _pendingHighSurrogate = null;
                return true;
            }

            // Deliver from current text node
            if (_currentTextNode != null && _textNodeIndex < _currentTextNode.Length)
            {
                char next = _currentTextNode[_textNodeIndex++];
                if (char.IsHighSurrogate(next))
                {
                    // Surrogate Pairs could not be split across text nodes
                    c = next;
                    _pendingHighSurrogate = _currentTextNode[_textNodeIndex++];
                    return true;
                }
                else
                {
                    c = next;
                    return true;
                }
            }

            // Fill/Refill current text node, then recurse to deliver the next char from one single node at a time;
            // will not read entire xml column if requested substring is met.
            while (_xmlReader.Read())
            {
                // Not using XmlWriter since this maintains better control of allocations and prevents an intermediate buffer copy.
                switch (_xmlReader.NodeType)
                {
                    case XmlNodeType.Element:
                        _currentTextNode = BuildStartOrEmptyTag();
                        _textNodeIndex = 0;
                        return TryReadNextChar(out c);

                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        _currentTextNode = ReadAllText();
                        _textNodeIndex = 0;
                        return TryReadNextChar(out c);

                    case XmlNodeType.ProcessingInstruction:
                        _currentTextNode = $"<?{_xmlReader.Name} {_xmlReader.Value}?>";
                        _textNodeIndex = 0;
                        return TryReadNextChar(out c);

                    case XmlNodeType.Comment:
                        _currentTextNode = $"<!--{_xmlReader.Value}-->";
                        _textNodeIndex = 0;
                        return TryReadNextChar(out c);

                    case XmlNodeType.EndElement:
                        _currentTextNode = BuildEndTag();
                        _textNodeIndex = 0;
                        return TryReadNextChar(out c);

                    default:
                        // Skip EntityReference, DocumentType, XmlDeclaration which are normalized out by SQL Server
                        continue;
                }
            }

            // Ensure we don't return any stale chars after EOF
            c = '\0';
            return false; // EOF
        }

        /// <summary>
        /// Reads all text content from the current node of the underlying XML reader and returns it as a string.
        /// </summary>
        /// <remarks>
        /// If the XML reader supports reading in chunks, this method reads the text in segments
        /// to improve performance. Otherwise, it retrieves the value directly from the XML reader.
        /// </remarks>
        /// <returns>A string containing all text read from the XML reader. Returns an empty string if no text is available.</returns>
        private string ReadAllText()
        {
            if (_canReadChunk)
            {
                char[] buffer = new char[8192];
                int read;
                StringBuilder stringBuilder = new StringBuilder();
                while ((read = _xmlReader.ReadValueChunk(buffer, 0, buffer.Length)) > 0)
                {
                    stringBuilder.Append(buffer, 0, read); // only valid chars
                }
                return stringBuilder.ToString();
            }
            else
            {
                return _xmlReader.Value ?? string.Empty; // never null -> avoids trailing \0
            }
        }

        /// <summary>
        /// Constructs an XML start tag or an empty element tag for the current node of the underlying XML reader,
        /// including the namespace prefix and any attributes if present.
        /// </summary>
        /// <remarks>
        /// If the current XML node contains attributes, they are included in the generated tag.
        /// If the node is an empty element, a self-closing tag is returned; otherwise, a standard opening tag is
        /// produced. The method does not advance the position of the XML reader.
        /// </remarks>
        /// <returns>A string that represents the XML start tag or a self-closing empty element tag, including all attributes of
        /// the current node.</returns>
        private string BuildStartOrEmptyTag()
        {
            string prefix = _xmlReader.Prefix;
            string tagName = string.IsNullOrEmpty(prefix) ? _xmlReader.LocalName : $"{prefix}:{_xmlReader.LocalName}";
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append('<').Append(tagName);

            if (_xmlReader.HasAttributes)
            {
                for (int i = 0; i < _xmlReader.AttributeCount; i++)
                {
                    _xmlReader.MoveToAttribute(i);
                    string attrPrefix = _xmlReader.Prefix;
                    string attrName = string.IsNullOrEmpty(attrPrefix) ? _xmlReader.LocalName : $"{attrPrefix}:{_xmlReader.LocalName}";
                    stringBuilder.Append(' ').Append(attrName).Append("=\"").Append(EscapeAttribute(_xmlReader.Value)).Append('"');
                }
                _xmlReader.MoveToElement();
            }

            if (_xmlReader.IsEmptyElement)
            {
                stringBuilder.Append(" />");
            }
            else
            {
                stringBuilder.Append('>');
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Builds the closing XML tag for the current element, including the namespace prefix if present.
        /// </summary>
        /// <remarks>
        /// The returned tag is constructed using the prefix and local name from the underlying
        /// XML reader. If the element has no namespace prefix, only the local name is used in the tag.
        /// </remarks>
        /// <returns>A string that represents the closing tag of the current XML element, formatted with the appropriate
        /// namespace prefix if one exists.</returns>
        private string BuildEndTag()
        {
            string prefix = _xmlReader.Prefix;
            string tagName = string.IsNullOrEmpty(prefix) ? _xmlReader.LocalName : $"{prefix}:{_xmlReader.LocalName}";
            return $"</{tagName}>";
        }

        /// <summary>
        /// Escapes special characters in the provided string to ensure it is safe for use in XML attributes.
        /// </summary>
        /// <remarks><![CDATA[
        /// This method specifically escapes the characters '&', '<', '>', and '"'. It does not
        /// escape single quotes as they are not required for SQL Server attributes. The method uses a StringBuilder for
        /// efficient string manipulation.
        /// ]]></remarks>
        /// <param name="value">The string to be escaped. This string may contain special XML characters that need to be replaced with their
        /// corresponding entity references.</param>
        /// <returns>A string with special XML characters replaced by their corresponding entity references. If the input string
        /// is null or empty, an empty string is returned.</returns>
        private string EscapeAttribute(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Only create a StringBuilder if we find a character that needs escaping, to avoid unnecessary allocations
            StringBuilder sb = null;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                string replacement = c switch
                {
                    '&' => "&amp;",
                    '<' => "&lt;",
                    '>' => "&gt;",
                    '"' => "&quot;",
                    //'\'' => "&apos;", SQL Server does not escape single quotes in attributes
                    _ => null
                };

                if (replacement != null)
                {
                    sb ??= new StringBuilder(value.Length + 8);
                    sb.Append(value, 0, i);
                    sb.Append(replacement);

                    for (i = i + 1; i < value.Length; i++)
                    {
                        c = value[i];
                        replacement = c switch
                        {
                            '&' => "&amp;",
                            '<' => "&lt;",
                            '>' => "&gt;",
                            '"' => "&quot;",
                            //'\'' => "&apos;", SQL Server does not escape single quotes in attributes
                            _ => null
                        };

                        if (replacement != null)
                        {
                            sb.Append(replacement);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                    }

                    return sb.ToString();
                }
            }

            return value;
        }
    }
}
