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
    internal partial class TdsParserStateObject
    {
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
#if NETFRAMEWORK
            if (SqlClientEventSource.Log.IsAdvancedTraceOn())
            {
                // If we have tracePassword variables set, we are flushing TDSLogin and so we need to
                // blank out password in buffer.  Buffer has already been sent to netlib, so no danger
                // of losing info.
                if (_tracePasswordOffset != 0)
                {
                    for (int i = _tracePasswordOffset; i < _tracePasswordOffset +
                        _tracePasswordLength; i++)
                    {
                        _outBuff[i] = 0;
                    }

                    // Reset state.
                    _tracePasswordOffset = 0;
                    _tracePasswordLength = 0;
                }
                if (_traceChangePasswordOffset != 0)
                {
                    for (int i = _traceChangePasswordOffset; i < _traceChangePasswordOffset +
                        _traceChangePasswordLength; i++)
                    {
                        _outBuff[i] = 0;
                    }

                    // Reset state.
                    _traceChangePasswordOffset = 0;
                    _traceChangePasswordLength = 0;
                }
            }
            SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParser.WritePacket | INFO | ADV | State Object Id {0}, Packet sent. Out buffer: {1}, Out Bytes Used: {2}", ObjectID, _outBuff, _outBytesUsed);
#endif
        }
    }
}
