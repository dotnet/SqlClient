// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using Microsoft.Data.SqlTypes;

namespace Microsoft.Data.SqlClient
{
    // Caches the bytes returned from partial length prefixed datatypes, like XML
    internal sealed class SqlCachedBuffer : INullable
    {
        public static readonly SqlCachedBuffer Null = new();
        private const int MaxChunkSize = 2048; // Arbitrary value for chunk size. Revisit this later for better perf

        private readonly List<byte[]> _cachedBytes;

        private SqlCachedBuffer()
        {
            // For constructing Null
        }

        private SqlCachedBuffer(List<byte[]> cachedBytes)
        {
            _cachedBytes = cachedBytes;
        }

        internal List<byte[]> CachedBytes => _cachedBytes;

        /// <summary>
        /// Reads off from the network buffer and caches bytes. Only reads one column value in the current row.
        /// </summary>
        internal static TdsOperationStatus TryCreate(SqlMetaDataPriv metadata, TdsParser parser, TdsParserStateObject stateObj, out SqlCachedBuffer buffer)
        {
            buffer = null;

            (bool isAvailable, bool isStarting, bool isContinuing) = stateObj.GetSnapshotStatuses();

            List<byte[]> cachedBytes = null;
            if (isAvailable)
            {
                cachedBytes = stateObj.TryTakeSnapshotStorage() as List<byte[]>;
                if (cachedBytes != null && !isStarting && !isContinuing) 
                {
                    stateObj.SetSnapshotStorage(null);
                }
            }
 
            if (cachedBytes == null)
            {
                cachedBytes = new List<byte[]>();
            }


            // the very first length is already read.
            TdsOperationStatus result = parser.TryPlpBytesLeft(stateObj, out ulong plplength);
            if (result != TdsOperationStatus.Done)
            {
                return result;
            }


            // For now we  only handle Plp data from the parser directly.
            Debug.Assert(metadata.metaType.IsPlp, "SqlCachedBuffer call on a non-plp data");
            do
            {
                if (plplength == 0)
                {
                    break;
                }
                do
                {
                    bool returnAfterAdd = false;
                    int cb = (plplength > (ulong)MaxChunkSize) ? MaxChunkSize : (int)plplength;
                    byte[] byteArr = new byte[cb];
                    // pass false for the writeDataSizeToSnapshot parameter because we want to only take data
                    // from the current packet and not try to do a continue-capable multi packet read
                    result = stateObj.TryReadPlpBytes(ref byteArr, 0, cb, out cb, writeDataSizeToSnapshot: false, compatibilityMode: false);
                    if (result != TdsOperationStatus.Done)
                    {
                        if (result == TdsOperationStatus.NeedMoreData && isAvailable && cb == byteArr.Length)
                        {
                            // succeeded in getting the data but failed to find the next plp length
                            returnAfterAdd = true;
                        }
                        else
                        {
                            return result;
                        }
                    }

                    Debug.Assert(cb == byteArr.Length);
                    if (cachedBytes.Count == 0)
                    {
                        // Add the Byte order mark if needed if we read the first array
                        AddByteOrderMark(byteArr, cachedBytes);
                    }
                    cachedBytes.Add(byteArr);
                    plplength -= (ulong)cb;

                    if (returnAfterAdd)
                    {
                        if (isStarting || isContinuing)
                        {
                            stateObj.SetSnapshotStorage(cachedBytes);
                        }
                        return result;
                    }

                } while (plplength > 0);

                result = parser.TryPlpBytesLeft(stateObj, out plplength);
                if (result != TdsOperationStatus.Done)
                {
                    return result;
                }
            } while (plplength > 0);
            Debug.Assert(stateObj._longlen == 0 && stateObj._longlenleft == 0);

            buffer = new SqlCachedBuffer(cachedBytes);
            return TdsOperationStatus.Done;
        }

        private static void AddByteOrderMark(byte[] byteArr, List<byte[]> cachedBytes)
        {
            // Need to find out if we should add byte order mark or not. 
            // We need to add this if we are getting ntext xml, not if we are getting binary xml
            // Binary Xml always begins with the bytes 0xDF and 0xFF
            // If we aren't getting these, then we are getting Unicode xml
            if ((byteArr.Length < 2) || (byteArr[0] != 0xDF) || (byteArr[1] != 0xFF))
            {
                Debug.Assert(cachedBytes.Count == 0);
                cachedBytes.Add(TdsEnums.XMLUNICODEBOMBYTES);
            }
        }

        internal Stream ToStream()
        {
            return new SqlCachedStream(this);
        }

        override public string ToString()
        {
            if (IsNull)
            {
                throw new SqlNullValueException();
            }

            if (_cachedBytes.Count == 0)
            {
                return string.Empty;
            }
            SqlXml sxml = new(ToStream());
            return sxml.Value;
        }

        internal SqlString ToSqlString()
        {
            if (IsNull)
            {
                return SqlString.Null;
            }

            string str = ToString();
            return new SqlString(str);
        }

        internal SqlXml ToSqlXml()
        {
            SqlXml sx = new(ToStream());
            return sx;
        }

        // Prevent inlining so that reflection calls are not moved to caller that may be in a different assembly that may have a different grant set.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal XmlReader ToXmlReader()
        {
            return SqlTypeWorkarounds.SqlXmlCreateSqlXmlReader(ToStream(), closeInput: false, async: false);
        }

        public bool IsNull => _cachedBytes == null;
    }
}
