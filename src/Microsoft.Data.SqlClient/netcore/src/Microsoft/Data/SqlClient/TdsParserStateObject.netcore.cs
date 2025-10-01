// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient
{
    internal abstract partial class TdsParserStateObject
    {
        /////////////////////////////////////////
        // Network/Packet Writing & Processing //
        /////////////////////////////////////////

        // Sends an attention signal - executing thread will consume attn.
        internal void SendAttention(bool mustTakeWriteLock = false, bool asyncClose = false)
        {
            if (!_attentionSent)
            {
                // Dumps contents of buffer to OOB write (currently only used for
                // attentions.  There is no body for this message
                // Doesn't touch this._outBytesUsed
                if (_parser.State == TdsParserState.Closed || _parser.State == TdsParserState.Broken)
                {
                    return;
                }

                PacketHandle attnPacket = CreateAndSetAttentionPacket();

                try
                {
                    // Dev11 #344723: SqlClient stress test suspends System_Data!Tcp::ReadSync via a call to SqlDataReader::Close
                    // Set _attentionSending to true before sending attention and reset after setting _attentionSent
                    // This prevents a race condition between receiving the attention ACK and setting _attentionSent
                    _attentionSending = true;
#if DEBUG
                    if (!s_skipSendAttention)
                    {
#endif
                        // Take lock and send attention
                        bool releaseLock = false;
                        if ((mustTakeWriteLock) && (!_parser.Connection.ThreadHasParserLockForClose))
                        {
                            releaseLock = true;
                            _parser.Connection._parserLock.Wait(canReleaseFromAnyThread: false);
                            _parser.Connection.ThreadHasParserLockForClose = true;
                        }
                        try
                        {
                            // Check again (just in case the connection was closed while we were waiting)
                            if (_parser.State == TdsParserState.Closed || _parser.State == TdsParserState.Broken)
                            {
                                return;
                            }

                            _parser._asyncWrite = false; // stop async write
                            SNIWritePacket(attnPacket, out _, canAccumulate: false, callerHasConnectionLock: false, asyncClose);
                            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObject.SendAttention | Info | State Object Id {0}, Sent Attention.", _objectID);
                        }
                        finally
                        {
                            if (releaseLock)
                            {
                                _parser.Connection.ThreadHasParserLockForClose = false;
                                _parser.Connection._parserLock.Release();
                            }
                        }
#if DEBUG
                    }
#endif

                    SetTimeoutSeconds(AttentionTimeoutSeconds); // Initialize new attention timeout of 5 seconds.
                    _attentionSent = true;
                }
                finally
                {
                    _attentionSending = false;
                }

                SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParserStateObject.SendAttention | INFO | ADV | State Object Id {0}, Packet sent. Out Buffer: {1}, Out Bytes Used: {2}", _objectID, _outBuff, _outBytesUsed);
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObject.SendAttention | Info | State Object Id {0}, Attention sent to the server.", _objectID);

                AssertValidState();
            }
        }

        //////////////////////////////////////////////
        // Statistics, Tracing, and related methods //
        //////////////////////////////////////////////

        private void SniWriteStatisticsAndTracing()
        {
            SqlStatistics statistics = _parser.Statistics;
            if (statistics != null)
            {
                statistics.SafeIncrement(ref statistics._buffersSent);
                statistics.SafeAdd(ref statistics._bytesSent, _outBytesUsed);
                statistics.RequestNetworkServerTimer();
            }
        }
    }
}
