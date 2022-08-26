// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal partial class TdsParserStateObject
    {
        private SNIHandle _sessionHandle = null;              // the SNI handle we're to work on

        internal bool _pendingData = false;
        internal bool _errorTokenReceived = false;               // Keep track of whether an error was received for the result.
                                                                 // This is reset upon each done token - there can be
        // SNI variables                                                     // multiple resultsets in one batch.
        private SNIPacket _sniPacket = null;                // Will have to re-vamp this for MARS
        internal SNIPacket _sniAsyncAttnPacket = null;                // Packet to use to send Attn
        private WritePacketCache _writePacketCache = new WritePacketCache(); // Store write packets that are ready to be re-used
        private Dictionary<IntPtr, SNIPacket> _pendingWritePackets = new Dictionary<IntPtr, SNIPacket>(); // Stores write packets that have been sent to SNI, but have not yet finished writing (i.e. we are waiting for SNI's callback)

        // Async variables
        private GCHandle _gcHandle;                                    // keeps this object alive until we're closed.

        // Timeout variables
        internal bool _attentionReceived = false;               // NOTE: Received is not volatile as it is only ever accessed\modified by TryRun its callees (i.e. single threaded access)

        // This variable is used to prevent sending an attention by another thread that is not the
        // current owner of the stateObj.  I currently do not know how this can happen.  Mark added
        // the code but does not remember either.  At some point, we need to research killing this
        // logic.
        private volatile int _allowObjectID;

        internal bool _hasOpenResult = false;


        // Used for blanking out password in trace.
        internal int _tracePasswordOffset = 0;
        internal int _tracePasswordLength = 0;
        internal int _traceChangePasswordOffset = 0;
        internal int _traceChangePasswordLength = 0;

        internal bool _receivedColMetaData;      // Used to keep track of when to fire StatementCompleted  event.


        //////////////////
        // Constructors //
        //////////////////

        internal TdsParserStateObject(TdsParser parser, SNIHandle physicalConnection, bool async)
        {
            // Construct a MARS session
            Debug.Assert(null != parser, "no parser?");
            _parser = parser;
            SniContext = SniContext.Snix_GetMarsSession;

            Debug.Assert(null != _parser._physicalStateObj, "no physical session?");
            Debug.Assert(null != _parser._physicalStateObj._inBuff, "no in buffer?");
            Debug.Assert(null != _parser._physicalStateObj._outBuff, "no out buffer?");
            Debug.Assert(_parser._physicalStateObj._outBuff.Length ==
                         _parser._physicalStateObj._inBuff.Length, "Unexpected unequal buffers.");

            // Determine packet size based on physical connection buffer lengths.
            SetPacketSize(_parser._physicalStateObj._outBuff.Length);

            SNINativeMethodWrapper.ConsumerInfo myInfo = CreateConsumerInfo(async);
            SQLDNSInfo cachedDNSInfo;
            bool ret = SQLFallbackDNSCache.Instance.GetDNSInfo(_parser.FQDNforDNSCache, out cachedDNSInfo);

            _sessionHandle = new SNIHandle(myInfo, physicalConnection, _parser.Connection.ConnectionOptions.IPAddressPreference, cachedDNSInfo);
            if (_sessionHandle.Status != TdsEnums.SNI_SUCCESS)
            {
                AddError(parser.ProcessSNIError(this));
                ThrowExceptionAndWarning();
            }

            // we post a callback that represents the call to dispose; once the
            // object is disposed, the next callback will cause the GC Handle to
            // be released.
            IncrementPendingCallbacks();
            _lastSuccessfulIOTimer = parser._physicalStateObj._lastSuccessfulIOTimer;
        }

        ////////////////
        // Properties //
        ////////////////
        internal SNIHandle Handle
        {
            get
            {
                return _sessionHandle;
            }
        }

        internal bool HasOpenResult
        {
            get => _hasOpenResult;
            set => _hasOpenResult = value;
        }
        
        internal bool HasPendingData
        {
            get => _pendingData;
            set => _pendingData = value;
        }
        
        internal UInt32 Status
        {
            get
            {
                if (_sessionHandle != null)
                {
                    return _sessionHandle.Status;
                }
                else
                { // SQL BU DT 395431.
                    return TdsEnums.SNI_UNINITIALIZED;
                }
            }
        }

        private partial struct NullBitmap
        {
            internal bool TryInitialize(TdsParserStateObject stateObj, int columnsCount)
            {
                _columnsCount = columnsCount;
                // 1-8 columns need 1 byte
                // 9-16: 2 bytes, and so on
                int bitmapArrayLength = (columnsCount + 7) / 8;

                // allow reuse of previously allocated bitmap
                if (_nullBitmap == null || _nullBitmap.Length != bitmapArrayLength)
                {
                    _nullBitmap = new byte[bitmapArrayLength];
                }

                // read the null bitmap compression information from TDS
                if (!stateObj.TryReadByteArray(_nullBitmap, _nullBitmap.Length))
                {
                    return false;
                }
                SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObject.NullBitmap.Initialize | INFO | ADV | State Object Id {0}, NBCROW bitmap received, column count = {1}", stateObj.ObjectID, columnsCount);
                SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParserStateObject.NullBitmap.Initialize | INFO | ADV | State Object Id {0}, NBCROW bitmap data. Null Bitmap {1}, Null bitmap length: {2}", stateObj.ObjectID, _nullBitmap, (ushort)_nullBitmap.Length);

                return true;
            }
        }

        /////////////////////
        // General methods //
        /////////////////////

        // This method is only called by the command or datareader as a result of a user initiated
        // cancel request.
        internal void Cancel(int objectID)
        {
            bool hasLock = false;
            try
            {
                // Keep looping until we either grabbed the lock (and therefore sent attention) or the connection closes\breaks
                while ((!hasLock) && (_parser.State != TdsParserState.Closed) && (_parser.State != TdsParserState.Broken))
                {

                    Monitor.TryEnter(this, _waitForCancellationLockPollTimeout, ref hasLock);
                    if (hasLock)
                    { // Lock for the time being - since we need to synchronize the attention send.
                      // At some point in the future, I hope to remove this.
                      // This lock is also protecting against concurrent close and async continuations

                        // don't allow objectID -1 since it is reserved for 'not associated with a command'
                        // yes, the 2^32-1 comand won't cancel - but it also won't cancel when we don't want it
                        if ((!_cancelled) && (objectID == _allowObjectID) && (objectID != -1))
                        {
                            _cancelled = true;

                            if (_pendingData && !_attentionSent)
                            {
                                bool hasParserLock = false;
                                // Keep looping until we have the parser lock (and so are allowed to write), or the conneciton closes\breaks
                                while ((!hasParserLock) && (_parser.State != TdsParserState.Closed) && (_parser.State != TdsParserState.Broken))
                                {
                                    try
                                    {
                                        _parser.Connection._parserLock.Wait(canReleaseFromAnyThread: false, timeout: _waitForCancellationLockPollTimeout, lockTaken: ref hasParserLock);
                                        if (hasParserLock)
                                        {
                                            _parser.Connection.ThreadHasParserLockForClose = true;
                                            SendAttention();
                                        }
                                    }
                                    finally
                                    {
                                        if (hasParserLock)
                                        {
                                            if (_parser.Connection.ThreadHasParserLockForClose)
                                            {
                                                _parser.Connection.ThreadHasParserLockForClose = false;
                                            }
                                            _parser.Connection._parserLock.Release();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (hasLock)
                {
                    Monitor.Exit(this);
                }
            }
        }

        private void ResetCancelAndProcessAttention()
        {
            // This method is shared by CloseSession initiated by DataReader.Close or completed
            // command execution, as well as the session reclaimation code for cases where the
            // DataReader is opened and then GC'ed.
            lock (this)
            {
                // Reset cancel state.
                _cancelled = false;
                _allowObjectID = -1;

                if (_attentionSent)
                {
                    // Make sure we're cleaning up the AttentionAck if Cancel happened before taking the lock.
                    // We serialize Cancel/CloseSession to prevent a race condition between these two states.
                    // The problem is that both sending and receiving attentions are time taking
                    // operations.
#if DEBUG
                    TdsParser.ReliabilitySection tdsReliabilitySection = new TdsParser.ReliabilitySection();

                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    {
                        tdsReliabilitySection.Start();
#endif //DEBUG
                        Parser.ProcessPendingAck(this);
#if DEBUG
                    }
                    finally
                    {
                        tdsReliabilitySection.Stop();
                    }
#endif //DEBUG
                }
                SetTimeoutStateStopped();
            }
        }

        private SNINativeMethodWrapper.ConsumerInfo CreateConsumerInfo(bool async)
        {
            SNINativeMethodWrapper.ConsumerInfo myInfo = new SNINativeMethodWrapper.ConsumerInfo();

            Debug.Assert(_outBuff.Length == _inBuff.Length, "Unexpected unequal buffers.");

            myInfo.defaultBufferSize = _outBuff.Length; // Obtain packet size from outBuff size.

            if (async)
            {
                myInfo.readDelegate = SNILoadHandle.SingletonInstance.ReadAsyncCallbackDispatcher;
                myInfo.writeDelegate = SNILoadHandle.SingletonInstance.WriteAsyncCallbackDispatcher;
                _gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
                myInfo.key = (IntPtr)_gcHandle;
            }
            return myInfo;
        }

        internal void CreatePhysicalSNIHandle(
            string serverName,
            bool ignoreSniOpenTimeout,
            long timerExpire,
            out byte[] instanceName,
            byte[] spnBuffer,
            bool flushCache,
            bool async,
            bool fParallel,
            TransparentNetworkResolutionState transparentNetworkResolutionState,
            int totalTimeout,
            SqlConnectionIPAddressPreference ipPreference,
            string cachedFQDN,
            bool tlsFirst = false,
            string hostNameInCertificate = "")
        {
            SNINativeMethodWrapper.ConsumerInfo myInfo = CreateConsumerInfo(async);

            // Translate to SNI timeout values (Int32 milliseconds)
            long timeout;
            if (Int64.MaxValue == timerExpire)
            {
                timeout = Int32.MaxValue;
            }
            else
            {
                timeout = ADP.TimerRemainingMilliseconds(timerExpire);
                if (timeout > Int32.MaxValue)
                {
                    timeout = Int32.MaxValue;
                }
                else if (0 > timeout)
                {
                    timeout = 0;
                }
            }

            // serverName : serverInfo.ExtendedServerName
            // may not use this serverName as key

            _ = SQLFallbackDNSCache.Instance.GetDNSInfo(cachedFQDN, out SQLDNSInfo cachedDNSInfo);

            _sessionHandle = new SNIHandle(myInfo, serverName, spnBuffer, ignoreSniOpenTimeout, checked((int)timeout),
                out instanceName, flushCache, !async, fParallel, transparentNetworkResolutionState, totalTimeout,
                ipPreference, cachedDNSInfo, tlsFirst, hostNameInCertificate);
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal int DecrementPendingCallbacks(bool release)
        {
            int remaining = Interlocked.Decrement(ref _pendingCallbacks);
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParserStateObject.DecrementPendingCallbacks|ADV> {0}, after decrementing _pendingCallbacks: {1}", ObjectID, _pendingCallbacks);

            if ((0 == remaining || release) && _gcHandle.IsAllocated)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParserStateObject.DecrementPendingCallbacks|ADV> {0}, FREEING HANDLE!", ObjectID);
                _gcHandle.Free();
            }

            // NOTE: TdsParserSessionPool may call DecrementPendingCallbacks on a TdsParserStateObject which is already disposed
            // This is not dangerous (since the stateObj is no longer in use), but we need to add a workaround in the assert for it
            Debug.Assert((remaining == -1 && _sessionHandle == null) || (0 <= remaining && remaining < 3), $"_pendingCallbacks values is invalid after decrementing: {remaining}");
            return remaining;
        }

        internal void Dispose()
        {

            SafeHandle packetHandle = _sniPacket;
            SafeHandle sessionHandle = _sessionHandle;
            SafeHandle asyncAttnPacket = _sniAsyncAttnPacket;
            _sniPacket = null;
            _sessionHandle = null;
            _sniAsyncAttnPacket = null;

            DisposeCounters();

            if (null != sessionHandle || null != packetHandle)
            {
                // Comment CloseMARSSession
                // UNDONE - if there are pending reads or writes on logical connections, we need to block
                // here for the callbacks!!!  This only applies to async.  Should be fixed by async fixes for
                // AD unload/exit.

                // TODO: Make this a BID trace point!
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                { }
                finally
                {
                    if (packetHandle != null)
                    {
                        packetHandle.Dispose();
                    }
                    if (asyncAttnPacket != null)
                    {
                        asyncAttnPacket.Dispose();
                    }
                    if (sessionHandle != null)
                    {
                        sessionHandle.Dispose();
                        DecrementPendingCallbacks(true); // Will dispose of GC handle.
                    }
                }
            }

            if (_writePacketCache != null)
            {
                lock (_writePacketLockObject)
                {
                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    { }
                    finally
                    {
                        _writePacketCache.Dispose();
                        // Do not set _writePacketCache to null, just in case a WriteAsyncCallback completes after this point
                    }
                }
            }
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal int IncrementPendingCallbacks()
        {
            int remaining = Interlocked.Increment(ref _pendingCallbacks);

            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParserStateObject.IncrementPendingCallbacks|ADV> {0}, after incrementing _pendingCallbacks: {1}", ObjectID, _pendingCallbacks);
            Debug.Assert(0 < remaining && remaining <= 3, $"_pendingCallbacks values is invalid after incrementing: {remaining}");
            return remaining;
        }

        internal void StartSession(int objectID)
        {
            _allowObjectID = objectID;
        }

        ///////////////////////////////////////
        // Buffer read methods - data values //
        ///////////////////////////////////////

        // look at the next byte without pulling it off the wire, don't just returun _inBytesUsed since we may
        // have to go to the network to get the next byte.
        internal bool TryPeekByte(out byte value)
        {
            if (!TryReadByte(out value))
            {
                return false;
            }

            // now do fixup
            _inBytesPacket++;
            _inBytesUsed--;

            AssertValidState();
            return true;
        }

        // Takes a byte array, an offset, and a len and fills the array from the offset to len number of
        // bytes from the in buffer.
        public bool TryReadByteArray(Span<byte> buff, int len)
        {
            return TryReadByteArray(buff, len, out _);
        }

        // NOTE: This method must be retriable WITHOUT replaying a snapshot
        // Every time you call this method increment the offset and decrease len by the value of totalRead
        public bool TryReadByteArray(Span<byte> buff, int len, out int totalRead)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadByteArray");  // you need to setup for a thread abort somewhere before you call this method
            totalRead = 0;

#if DEBUG
            if (_snapshot != null && _snapshot.DoPend())
            {
                _networkPacketTaskSource = new TaskCompletionSource<object>();
                Thread.MemoryBarrier();

                if (_forcePendingReadsToWaitForUser)
                {
                    _realNetworkPacketTaskSource = new TaskCompletionSource<object>();
                    _realNetworkPacketTaskSource.SetResult(null);
                }
                else
                {
                    _networkPacketTaskSource.TrySetResult(null);
                }
                return false;
            }
#endif

            Debug.Assert(buff.IsEmpty || buff.Length >= len, "Invalid length sent to ReadByteArray()!");

            // loop through and read up to array length
            while (len > 0)
            {
                if ((_inBytesPacket == 0) || (_inBytesUsed == _inBytesRead))
                {
                    if (!TryPrepareBuffer())
                    {
                        return false;
                    }
                }

                int bytesToRead = Math.Min(len, Math.Min(_inBytesPacket, _inBytesRead - _inBytesUsed));
                Debug.Assert(bytesToRead > 0, "0 byte read in TryReadByteArray");
                if (!buff.IsEmpty)
                {
                    ReadOnlySpan<byte> copyFrom = new ReadOnlySpan<byte>(_inBuff, _inBytesUsed, bytesToRead);
                    Span<byte> copyTo = buff.Slice(totalRead, bytesToRead);
                    copyFrom.CopyTo(copyTo);
                }

                totalRead += bytesToRead;
                _inBytesUsed += bytesToRead;
                _inBytesPacket -= bytesToRead;
                len -= bytesToRead;

                AssertValidState();
            }

            return true;
        }

        // Takes no arguments and returns a byte from the buffer.  If the buffer is empty, it is filled
        // before the byte is returned.
        internal bool TryReadByte(out byte value)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadByte");  // you need to setup for a thread abort somewhere before you call this method
            Debug.Assert(_inBytesUsed >= 0 && _inBytesUsed <= _inBytesRead, "ERROR - TDSParser: _inBytesUsed < 0 or _inBytesUsed > _inBytesRead");
            value = 0;

#if DEBUG
            if (_snapshot != null && _snapshot.DoPend())
            {
                _networkPacketTaskSource = new TaskCompletionSource<object>();
                Thread.MemoryBarrier();

                if (_forcePendingReadsToWaitForUser)
                {
                    _realNetworkPacketTaskSource = new TaskCompletionSource<object>();
                    _realNetworkPacketTaskSource.SetResult(null);
                }
                else
                {
                    _networkPacketTaskSource.TrySetResult(null);
                }
                return false;
            }
#endif

            if ((_inBytesPacket == 0) || (_inBytesUsed == _inBytesRead))
            {
                if (!TryPrepareBuffer())
                {
                    return false;
                }
            }

            // decrement the number of bytes left in the packet
            _inBytesPacket--;

            Debug.Assert(_inBytesPacket >= 0, "ERROR - TDSParser: _inBytesPacket < 0");

            // return the byte from the buffer and increment the counter for number of bytes used in the in buffer
            value = (_inBuff[_inBytesUsed++]);

            AssertValidState();
            return true;
        }

        internal bool TryReadChar(out char value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");

            byte[] buffer;
            int offset;
            if (((_inBytesUsed + 2) > _inBytesRead) || (_inBytesPacket < 2))
            {
                // If the char isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                if (!TryReadByteArray(_bTmp, 2))
                {
                    value = '\0';
                    return false;
                }

                buffer = _bTmp;
                offset = 0;
            }
            else
            {
                // The entire char is in the packet and in the buffer, so just return it
                // and take care of the counters.

                buffer = _inBuff;
                offset = _inBytesUsed;

                _inBytesUsed += 2;
                _inBytesPacket -= 2;
            }

            AssertValidState();
            value = (char)((buffer[offset + 1] << 8) + buffer[offset]);
            return true;
        }

        internal bool TryReadInt16(out short value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");

            byte[] buffer;
            int offset;
            if (((_inBytesUsed + 2) > _inBytesRead) || (_inBytesPacket < 2))
            {
                // If the int16 isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                if (!TryReadByteArray(_bTmp, 2))
                {
                    value = default(short);
                    return false;
                }

                buffer = _bTmp;
                offset = 0;
            }
            else
            {
                // The entire int16 is in the packet and in the buffer, so just return it
                // and take care of the counters.

                buffer = _inBuff;
                offset = _inBytesUsed;

                _inBytesUsed += 2;
                _inBytesPacket -= 2;
            }

            AssertValidState();
            value = (Int16)((buffer[offset + 1] << 8) + buffer[offset]);
            return true;
        }

        internal bool TryReadInt32(out int value)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadInt32");  // you need to setup for a thread abort somewhere before you call this method
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            if (((_inBytesUsed + 4) > _inBytesRead) || (_inBytesPacket < 4))
            {
                // If the int isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                if (!TryReadByteArray(_bTmp, 4))
                {
                    value = 0;
                    return false;
                }

                AssertValidState();
                value = BitConverter.ToInt32(_bTmp, 0);
                return true;
            }
            else
            {
                // The entire int is in the packet and in the buffer, so just return it
                // and take care of the counters.

                value = BitConverter.ToInt32(_inBuff, _inBytesUsed);

                _inBytesUsed += 4;
                _inBytesPacket -= 4;

                AssertValidState();
                return true;
            }
        }

        // This method is safe to call when doing async without snapshot
        internal bool TryReadInt64(out long value)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadInt64");  // you need to setup for a thread abort somewhere before you call this method
            if ((_inBytesPacket == 0) || (_inBytesUsed == _inBytesRead))
            {
                if (!TryPrepareBuffer())
                {
                    value = 0;
                    return false;
                }
            }

            if ((_bTmpRead > 0) || (((_inBytesUsed + 8) > _inBytesRead) || (_inBytesPacket < 8)))
            {
                // If the long isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                int bytesRead = 0;
                if (!TryReadByteArray(_bTmp.AsSpan(start: _bTmpRead), 8 - _bTmpRead, out bytesRead))
                {
                    Debug.Assert(_bTmpRead + bytesRead <= 8, "Read more data than required");
                    _bTmpRead += bytesRead;
                    value = 0;
                    return false;
                }
                else
                {
                    Debug.Assert(_bTmpRead + bytesRead == 8, "TryReadByteArray returned true without reading all data required");
                    _bTmpRead = 0;
                    AssertValidState();
                    value = BitConverter.ToInt64(_bTmp, 0);
                    return true;
                }
            }
            else
            {
                // The entire long is in the packet and in the buffer, so just return it
                // and take care of the counters.

                value = BitConverter.ToInt64(_inBuff, _inBytesUsed);

                _inBytesUsed += 8;
                _inBytesPacket -= 8;

                AssertValidState();
                return true;
            }
        }

        internal bool TryReadUInt16(out ushort value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");

            byte[] buffer;
            int offset;
            if (((_inBytesUsed + 2) > _inBytesRead) || (_inBytesPacket < 2))
            {
                // If the uint16 isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                if (!TryReadByteArray(_bTmp, 2))
                {
                    value = default(ushort);
                    return false;
                }

                buffer = _bTmp;
                offset = 0;
            }
            else
            {
                // The entire uint16 is in the packet and in the buffer, so just return it
                // and take care of the counters.

                buffer = _inBuff;
                offset = _inBytesUsed;

                _inBytesUsed += 2;
                _inBytesPacket -= 2;
            }

            AssertValidState();
            value = (UInt16)((buffer[offset + 1] << 8) + buffer[offset]);
            return true;
        }

        // This method is safe to call when doing async without replay
        internal bool TryReadUInt32(out uint value)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadUInt32");  // you need to setup for a thread abort somewhere before you call this method
            if ((_inBytesPacket == 0) || (_inBytesUsed == _inBytesRead))
            {
                if (!TryPrepareBuffer())
                {
                    value = 0;
                    return false;
                }
            }

            if ((_bTmpRead > 0) || (((_inBytesUsed + 4) > _inBytesRead) || (_inBytesPacket < 4)))
            {
                // If the int isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                int bytesRead = 0;
                if (!TryReadByteArray(_bTmp.AsSpan(start: _bTmpRead), 4 - _bTmpRead, out bytesRead))
                {
                    Debug.Assert(_bTmpRead + bytesRead <= 4, "Read more data than required");
                    _bTmpRead += bytesRead;
                    value = 0;
                    return false;
                }
                else
                {
                    Debug.Assert(_bTmpRead + bytesRead == 4, "TryReadByteArray returned true without reading all data required");
                    _bTmpRead = 0;
                    AssertValidState();
                    value = BitConverter.ToUInt32(_bTmp, 0);
                    return true;
                }
            }
            else
            {
                // The entire int is in the packet and in the buffer, so just return it
                // and take care of the counters.

                value = BitConverter.ToUInt32(_inBuff, _inBytesUsed);

                _inBytesUsed += 4;
                _inBytesPacket -= 4;

                AssertValidState();
                return true;
            }
        }

        internal bool TryReadSingle(out float value)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadSingle");  // you need to setup for a thread abort somewhere before you call this method
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            if (((_inBytesUsed + 4) > _inBytesRead) || (_inBytesPacket < 4))
            {
                // If the float isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                if (!TryReadByteArray(_bTmp, 4))
                {
                    value = default(float);
                    return false;
                }

                AssertValidState();
                value = BitConverter.ToSingle(_bTmp, 0);
                return true;
            }
            else
            {
                // The entire float is in the packet and in the buffer, so just return it
                // and take care of the counters.

                value = BitConverter.ToSingle(_inBuff, _inBytesUsed);

                _inBytesUsed += 4;
                _inBytesPacket -= 4;

                AssertValidState();
                return true;
            }
        }

        internal bool TryReadDouble(out double value)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadDouble");  // you need to setup for a thread abort somewhere before you call this method
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            if (((_inBytesUsed + 8) > _inBytesRead) || (_inBytesPacket < 8))
            {
                // If the double isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                if (!TryReadByteArray(_bTmp, 8))
                {
                    value = default(double);
                    return false;
                }

                AssertValidState();
                value = BitConverter.ToDouble(_bTmp, 0);
                return true;
            }
            else
            {
                // The entire double is in the packet and in the buffer, so just return it
                // and take care of the counters.

                value = BitConverter.ToDouble(_inBuff, _inBytesUsed);

                _inBytesUsed += 8;
                _inBytesPacket -= 8;

                AssertValidState();
                return true;
            }
        }

        internal bool TryReadString(int length, out string value)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadString");  // you need to setup for a thread abort somewhere before you call this method
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            int cBytes = length << 1;
            byte[] buf;
            int offset = 0;

            if (((_inBytesUsed + cBytes) > _inBytesRead) || (_inBytesPacket < cBytes))
            {
                if (_bTmp == null || _bTmp.Length < cBytes)
                {
                    _bTmp = new byte[cBytes];
                }

                if (!TryReadByteArray(_bTmp, cBytes))
                {
                    value = null;
                    return false;
                }

                // assign local to point to parser scratch buffer
                buf = _bTmp;

                AssertValidState();
            }
            else
            {
                // assign local to point to _inBuff
                buf = _inBuff;
                offset = _inBytesUsed;
                _inBytesUsed += cBytes;
                _inBytesPacket -= cBytes;

                AssertValidState();
            }

            value = System.Text.Encoding.Unicode.GetString(buf, offset, cBytes);
            return true;
        }

        internal bool TryReadStringWithEncoding(int length, System.Text.Encoding encoding, bool isPlp, out string value)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadStringWithEncoding");  // you need to setup for a thread abort somewhere before you call this method
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");

            if (null == encoding)
            {
                // Bug 462435:CR: TdsParser.DrainData(stateObj) hitting timeout exception after Connection Resiliency change
                // http://vstfdevdiv:8080/web/wi.aspx?pcguid=22f9acc9-569a-41ff-b6ac-fac1b6370209&id=462435
                // Need to skip the current column before throwing the error - this ensures that the state shared between this and the data reader is consistent when calling DrainData
                if (isPlp)
                {
                    ulong ignored;
                    if (!_parser.TrySkipPlpValue((ulong)length, this, out ignored))
                    {
                        value = null;
                        return false;
                    }
                }
                else
                {
                    if (!TrySkipBytes(length))
                    {
                        value = null;
                        return false;
                    }
                }

                _parser.ThrowUnsupportedCollationEncountered(this);
            }
            byte[] buf = null;
            int offset = 0;

            if (isPlp)
            {
                if (!TryReadPlpBytes(ref buf, 0, Int32.MaxValue, out length))
                {
                    value = null;
                    return false;
                }

                AssertValidState();
            }
            else
            {
                if (((_inBytesUsed + length) > _inBytesRead) || (_inBytesPacket < length))
                {
                    if (_bTmp == null || _bTmp.Length < length)
                    {
                        _bTmp = new byte[length];
                    }

                    if (!TryReadByteArray(_bTmp, length))
                    {
                        value = null;
                        return false;
                    }

                    // assign local to point to parser scratch buffer
                    buf = _bTmp;

                    AssertValidState();
                }
                else
                {
                    // assign local to point to _inBuff
                    buf = _inBuff;
                    offset = _inBytesUsed;
                    _inBytesUsed += length;
                    _inBytesPacket -= length;

                    AssertValidState();
                }
            }

            // BCL optimizes to not use char[] underneath
            value = encoding.GetString(buf, offset, length);
            return true;
        }

        internal ulong ReadPlpLength(bool returnPlpNullIfNull)
        {
            ulong value;
            Debug.Assert(_syncOverAsync, "Should not attempt pends in a synchronous call");
            bool result = TryReadPlpLength(returnPlpNullIfNull, out value);
            if (!result)
            { throw SQL.SynchronousCallMayNotPend(); }
            return value;
        }

        // Reads the length of either the entire data or the length of the next chunk in a
        //   partially length prefixed data
        // After this call, call  ReadPlpBytes/ReadPlpUnicodeChars untill the specified length of data
        // is consumed. Repeat this until ReadPlpLength returns 0 in order to read the
        // entire stream.
        // When this function returns 0, it means the data stream is read completely and the
        // plp state in the tdsparser is cleaned.
        internal bool TryReadPlpLength(bool returnPlpNullIfNull, out ulong lengthLeft)
        {
            uint chunklen;
            // bool firstchunk = false;
            bool isNull = false;

            Debug.Assert(_longlenleft == 0, "Out of synch length read request");
            if (_longlen == 0)
            {
                // First chunk is being read. Find out what type of chunk it is
                long value;
                if (!TryReadInt64(out value))
                {
                    lengthLeft = 0;
                    return false;
                }
                _longlen = (ulong)value;
                // firstchunk = true;
            }

            if (_longlen == TdsEnums.SQL_PLP_NULL)
            {
                _longlen = 0;
                _longlenleft = 0;
                isNull = true;
            }
            else
            {
                // Data is coming in uint chunks, read length of next chunk
                if (!TryReadUInt32(out chunklen))
                {
                    lengthLeft = 0;
                    return false;
                }
                if (chunklen == TdsEnums.SQL_PLP_CHUNK_TERMINATOR)
                {
                    _longlenleft = 0;
                    _longlen = 0;
                }
                else
                {
                    _longlenleft = (ulong)chunklen;
                }
            }

            AssertValidState();

            if (isNull && returnPlpNullIfNull)
            {
                lengthLeft = TdsEnums.SQL_PLP_NULL;
                return true;
            }

            lengthLeft = _longlenleft;
            return true;
        }

        internal int ReadPlpBytesChunk(byte[] buff, int offset, int len)
        {
            Debug.Assert(_syncOverAsync, "Should not attempt pends in a synchronous call");
            Debug.Assert(_longlenleft > 0, "Read when no data available");

            int value;
            int bytesToRead = (int)Math.Min(_longlenleft, (ulong)len);
            bool result = TryReadByteArray(buff.AsSpan(start: offset), bytesToRead, out value);
            _longlenleft -= (ulong)bytesToRead;
            if (!result)
            {
                throw SQL.SynchronousCallMayNotPend();
            }
            return value;
        }

        // Reads the requested number of bytes from a plp data stream, or the entire data if
        // requested length is -1 or larger than the actual length of data. First call to this method
        //  should be preceeded by a call to ReadPlpLength or ReadDataLength.
        // Returns the actual bytes read.
        // NOTE: This method must be retriable WITHOUT replaying a snapshot
        // Every time you call this method increment the offst and decrease len by the value of totalBytesRead
        internal bool TryReadPlpBytes(ref byte[] buff, int offst, int len, out int totalBytesRead)
        {
            int bytesRead = 0;
            int bytesLeft;
            byte[] newbuf;
            ulong ignored;

            if (_longlen == 0)
            {
                Debug.Assert(_longlenleft == 0);
                if (buff == null)
                {
                    buff = new byte[0];
                }

                AssertValidState();
                totalBytesRead = 0;
                return true;       // No data
            }

            Debug.Assert((_longlen != TdsEnums.SQL_PLP_NULL),
                    "Out of sync plp read request");

            Debug.Assert((buff == null && offst == 0) || (buff.Length >= offst + len), "Invalid length sent to ReadPlpBytes()!");
            bytesLeft = len;

            // If total length is known up front, allocate the whole buffer in one shot instead of realloc'ing and copying over each time
            if (buff == null && _longlen != TdsEnums.SQL_PLP_UNKNOWNLEN)
            {
                buff = new byte[(int)Math.Min((int)_longlen, len)];
            }

            if (_longlenleft == 0)
            {
                if (!TryReadPlpLength(false, out ignored))
                {
                    totalBytesRead = 0;
                    return false;
                }
                if (_longlenleft == 0)
                { // Data read complete
                    totalBytesRead = 0;
                    return true;
                }
            }

            if (buff == null)
            {
                buff = new byte[_longlenleft];
            }

            totalBytesRead = 0;

            while (bytesLeft > 0)
            {
                int bytesToRead = (int)Math.Min(_longlenleft, (ulong)bytesLeft);
                if (buff.Length < (offst + bytesToRead))
                {
                    // Grow the array
                    newbuf = new byte[offst + bytesToRead];
                    Buffer.BlockCopy(buff, 0, newbuf, 0, offst);
                    buff = newbuf;
                }

                bool result = TryReadByteArray(buff.AsSpan(start: offst), bytesToRead, out bytesRead);
                Debug.Assert(bytesRead <= bytesLeft, "Read more bytes than we needed");
                Debug.Assert((ulong)bytesRead <= _longlenleft, "Read more bytes than is available");

                bytesLeft -= bytesRead;
                offst += bytesRead;
                totalBytesRead += bytesRead;
                _longlenleft -= (ulong)bytesRead;
                if (!result)
                {
                    return false;
                }

                if (_longlenleft == 0)
                { // Read the next chunk or cleanup state if hit the end
                    if (!TryReadPlpLength(false, out ignored))
                    {
                        return false;
                    }
                }

                AssertValidState();

                // Catch the point where we read the entire plp data stream and clean up state
                if (_longlenleft == 0)   // Data read complete
                    break;
            }
            return true;
        }


        /////////////////////////////////////////
        // Value Skip Logic                    //
        /////////////////////////////////////////


        // Reads bytes from the buffer but doesn't return them, in effect simply deleting them.
        // Does not handle plp fields, need to use SkipPlpBytesValue for those.
        // Does not handle null values or NBC bitmask, ensure the value is not null before calling this method
        internal bool TrySkipLongBytes(long num)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            int cbSkip = 0;

            while (num > 0)
            {
                cbSkip = (int)Math.Min((long)Int32.MaxValue, num);
                if (!TryReadByteArray(Span<byte>.Empty, cbSkip))
                {
                    return false;
                }
                num -= (long)cbSkip;
            }

            return true;
        }

        // Reads bytes from the buffer but doesn't return them, in effect simply deleting them.
        internal bool TrySkipBytes(int num)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            return TryReadByteArray(Span<byte>.Empty, num);
        }

        /////////////////////////////////////////
        // Network/Packet Reading & Processing //
        /////////////////////////////////////////

        internal void SetSnapshot()
        {
            _snapshot = new StateSnapshot(this);
            _snapshot.Snap();
            _snapshotReplay = false;
        }

        internal void ResetSnapshot()
        {
            _snapshot = null;
            _snapshotReplay = false;
        }

#if DEBUG
        StackTrace _lastStack;
#endif

        internal bool TryReadNetworkPacket()
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to TryReadNetworkPacket");  // you need to setup for a thread abort somewhere before you call this method

#if DEBUG
            Debug.Assert(!_shouldHaveEnoughData || _attentionSent, "Caller said there should be enough data, but we are currently reading a packet");
#endif

            if (_snapshot != null)
            {
                if (_snapshotReplay)
                {
                    if (_snapshot.Replay())
                    {
#if DEBUG
                        if (_checkNetworkPacketRetryStacks)
                        {
                            _snapshot.CheckStack(new StackTrace());
                        }
#endif
                        SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.ReadNetworkPacket|{0}|ADV> Async packet replay{0}", "INFO");
                        return true;
                    }
#if DEBUG
                    else
                    {
                        if (_checkNetworkPacketRetryStacks)
                        {
                            _lastStack = new StackTrace();
                        }
                    }
#endif
                }

                // previous buffer is in snapshot
                _inBuff = new byte[_inBuff.Length];
            }

            if (_syncOverAsync)
            {
                ReadSniSyncOverAsync();
                return true;
            }

            ReadSni(new TaskCompletionSource<object>());

#if DEBUG
            if (_failAsyncPends)
            {
                throw new InvalidOperationException("Attempted to pend a read when _failAsyncPends test hook was enabled");
            }
            if (_forceSyncOverAsyncAfterFirstPend)
            {
                _syncOverAsync = true;
            }
#endif
            Debug.Assert((_snapshot != null) ^ _asyncReadWithoutSnapshot, "Must have either _snapshot set up or _asyncReadWithoutSnapshot enabled (but not both) to pend a read");

            return false;
        }

        internal void PrepareReplaySnapshot()
        {
            _networkPacketTaskSource = null;
            _snapshot.PrepareReplay();
        }

        internal void ReadSniSyncOverAsync()
        {
            if (_parser.State == TdsParserState.Broken || _parser.State == TdsParserState.Closed)
            {
                throw ADP.ClosedConnectionError();
            }

            IntPtr readPacket = IntPtr.Zero;
            UInt32 error;

            RuntimeHelpers.PrepareConstrainedRegions();
            bool shouldDecrement = false;
            try
            {
                TdsParser.ReliabilitySection.Assert("unreliable call to ReadSniSync");  // you need to setup for a thread abort somewhere before you call this method

                Interlocked.Increment(ref _readingCount);
                shouldDecrement = true;

                SNIHandle handle = Handle;
                if (handle == null)
                {
                    throw ADP.ClosedConnectionError();
                }

                error = SNINativeMethodWrapper.SNIReadSyncOverAsync(handle, ref readPacket, GetTimeoutRemaining());

                Interlocked.Decrement(ref _readingCount);
                shouldDecrement = false;

                if (_parser.MARSOn)
                { // Only take reset lock on MARS and Async.
                    CheckSetResetConnectionState(error, CallbackType.Read);
                }

                if (TdsEnums.SNI_SUCCESS == error)
                { // Success - process results!
                    Debug.Assert(ADP.s_ptrZero != readPacket, "ReadNetworkPacket cannot be null in synchronous operation!");
                    ProcessSniPacket(readPacket, 0);
#if DEBUG
                    if (_forcePendingReadsToWaitForUser)
                    {
                        _networkPacketTaskSource = new TaskCompletionSource<object>();
                        Thread.MemoryBarrier();
                        _networkPacketTaskSource.Task.Wait();
                        _networkPacketTaskSource = null;
                    }
#endif
                }
                else
                { // Failure!
                    Debug.Assert(IntPtr.Zero == readPacket, "unexpected readPacket without corresponding SNIPacketRelease");
                    ReadSniError(this, error);
                }
            }
            finally
            {
                if (shouldDecrement)
                {
                    Interlocked.Decrement(ref _readingCount);
                }

                if (readPacket != IntPtr.Zero)
                {
                    // Be sure to release packet, otherwise it will be leaked by native.
                    SNINativeMethodWrapper.SNIPacketRelease(readPacket);
                }

                AssertValidState();
            }
        }

        internal void OnConnectionClosed()
        {
            // the stateObj is not null, so the async invocation that registered this callback
            // via the SqlReferenceCollection has not yet completed.  We will look for a 
            // _networkPacketTaskSource and mark it faulted.  If we don't find it, then
            // TdsParserStateObject.ReadSni will abort when it does look to see if the parser
            // is open.  If we do, then when the call that created it completes and a continuation
            // is registered, we will ensure the completion is called.

            // Note, this effort is necessary because when the app domain is being unloaded,
            // we don't get callback from SNI.

            // first mark parser broken.  This is to ensure that ReadSni will abort if it has
            // not yet executed.
            Parser.State = TdsParserState.Broken;
            Parser.Connection.BreakConnection();

            // Ensure that changing state occurs before checking _networkPacketTaskSource 
            Thread.MemoryBarrier();

            // then check for networkPacketTaskSource
            var taskSource = _networkPacketTaskSource;
            if (taskSource != null)
            {
                taskSource.TrySetException(ADP.ExceptionWithStackTrace(ADP.ClosedConnectionError()));
            }

            taskSource = _writeCompletionSource;
            if (taskSource != null)
            {
                taskSource.TrySetException(ADP.ExceptionWithStackTrace(ADP.ClosedConnectionError()));
            }

        }

        public void SetTimeoutStateStopped()
        {
            Interlocked.Exchange(ref _timeoutState, TimeoutState.Stopped);
            _timeoutIdentityValue = 0;
        }

        public bool IsTimeoutStateExpired
        {
            get
            {
                int state = _timeoutState;
                return state == TimeoutState.ExpiredAsync || state == TimeoutState.ExpiredSync;
            }
        }

        private void OnTimeoutAsync(object state)
        {
            if (_enforceTimeoutDelay)
            {
                Thread.Sleep(_enforcedTimeoutDelayInMilliSeconds);
            }

            int currentIdentityValue = _timeoutIdentityValue;
            TimeoutState timeoutState = (TimeoutState)state;
            if (timeoutState.IdentityValue == _timeoutIdentityValue)
            {
                // the return value is not useful here because no choice is going to be made using it 
                // we only want to make this call to set the state knowing that it will be seen later
                OnTimeoutCore(TimeoutState.Running, TimeoutState.ExpiredAsync);
            }
            else
            {
                Debug.WriteLine($"OnTimeoutAsync called with identity state={timeoutState.IdentityValue} but current identity is {currentIdentityValue} so it is being ignored");
            }
        }

        private bool OnTimeoutSync()
        {
            return OnTimeoutCore(TimeoutState.Running, TimeoutState.ExpiredSync);
        }

        /// <summary>
        /// attempts to change the timout state from the expected state to the target state and if it succeeds
        /// will setup the the stateobject into the timeout expired state
        /// </summary>
        /// <param name="expectedState">the state that is the expected current state, state will change only if this is correct</param>
        /// <param name="targetState">the state that will be changed to if the expected state is correct</param>
        /// <returns>boolean value indicating whether the call changed the timeout state</returns>
        private bool OnTimeoutCore(int expectedState, int targetState)
        {
            Debug.Assert(targetState == TimeoutState.ExpiredAsync || targetState == TimeoutState.ExpiredSync, "OnTimeoutCore must have an expiry state as the targetState");

            bool retval = false;
            if (Interlocked.CompareExchange(ref _timeoutState, targetState, expectedState) == expectedState)
            {
                retval = true;
                // lock protects against Close and Cancel
                lock (this)
                {
                    if (!_attentionSent)
                    {
                        AddError(new SqlError(TdsEnums.TIMEOUT_EXPIRED, (byte)0x00, TdsEnums.MIN_ERROR_CLASS, _parser.Server, _parser.Connection.TimeoutErrorInternal.GetErrorMessage(), "", 0, TdsEnums.SNI_WAIT_TIMEOUT));

                        // Grab a reference to the _networkPacketTaskSource in case it becomes null while we are trying to use it
                        TaskCompletionSource<object> source = _networkPacketTaskSource;

                        if (_parser.Connection.IsInPool)
                        {
                            // Dev11 Bug 390048 : Timing issue between OnTimeout and ReadAsyncCallback results in SqlClient's packet parsing going out of sync          
                            // We should never timeout if the connection is currently in the pool: the safest thing to do here is to doom the connection to avoid corruption
                            Debug.Assert(_parser.Connection.IsConnectionDoomed, "Timeout occurred while the connection is in the pool");
                            _parser.State = TdsParserState.Broken;
                            _parser.Connection.BreakConnection();
                            if (source != null)
                            {
                                source.TrySetCanceled();
                            }
                        }
                        else if (_parser.State == TdsParserState.OpenLoggedIn)
                        {
                            try
                            {
                                SendAttention(mustTakeWriteLock: true);
                            }
                            catch (Exception e)
                            {
                                if (!ADP.IsCatchableExceptionType(e))
                                {
                                    throw;
                                }
                                // if unable to send attention, cancel the _networkPacketTaskSource to
                                // request the parser be broken.  SNIWritePacket errors will already
                                // be in the _errors collection.
                                if (source != null)
                                {
                                    source.TrySetCanceled();
                                }
                            }
                        }

                        // If we still haven't received a packet then we don't want to actually close the connection
                        // from another thread, so complete the pending operation as cancelled, informing them to break it
                        if (source != null)
                        {
                            Task.Delay(AttentionTimeoutSeconds * 1000).ContinueWith(_ =>
                            {
                                // Only break the connection if the read didn't finish
                                if (!source.Task.IsCompleted)
                                {
                                    int pendingCallback = IncrementPendingCallbacks();
                                    RuntimeHelpers.PrepareConstrainedRegions();
                                    try
                                    {
                                        // If pendingCallback is at 3, then ReadAsyncCallback hasn't been called yet
                                        // So it is safe for us to break the connection and cancel the Task (since we are not sure that ReadAsyncCallback will ever be called)
                                        if ((pendingCallback == 3) && (!source.Task.IsCompleted))
                                        {
                                            Debug.Assert(source == _networkPacketTaskSource, "_networkPacketTaskSource which is being timed is not the current task source");

                                            // Try to throw the timeout exception and store it in the task
                                            bool exceptionStored = false;
                                            try
                                            {
                                                CheckThrowSNIException();
                                            }
                                            catch (Exception ex)
                                            {
                                                if (source.TrySetException(ex))
                                                {
                                                    exceptionStored = true;
                                                }
                                            }

                                            // Ensure that the connection is no longer usable 
                                            // This is needed since the timeout error added above is non-fatal (and so throwing it won't break the connection)
                                            _parser.State = TdsParserState.Broken;
                                            _parser.Connection.BreakConnection();

                                            // If we didn't get an exception (something else observed it?) then ensure that the task is cancelled
                                            if (!exceptionStored)
                                            {
                                                source.TrySetCanceled();
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        DecrementPendingCallbacks(release: false);
                                    }
                                }
                            });
                        }
                    }
                }
            }
            return retval;
        }

        internal void ReadSni(TaskCompletionSource<object> completion)
        {
            Debug.Assert(_networkPacketTaskSource == null || ((_asyncReadWithoutSnapshot) && (_networkPacketTaskSource.Task.IsCompleted)), "Pending async call or failed to replay snapshot when calling ReadSni");
            _networkPacketTaskSource = completion;

            // Ensure that setting the completion source is completed before checking the state
            Thread.MemoryBarrier();

            // We must check after assigning _networkPacketTaskSource to avoid races with
            // SqlCommand.OnConnectionClosed
            if (_parser.State == TdsParserState.Broken || _parser.State == TdsParserState.Closed)
            {
                throw ADP.ClosedConnectionError();
            }

#if DEBUG
            if (_forcePendingReadsToWaitForUser)
            {
                _realNetworkPacketTaskSource = new TaskCompletionSource<object>();
            }
#endif

            IntPtr readPacket = IntPtr.Zero;
            UInt32 error = 0;

            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                Debug.Assert(completion != null, "Async on but null asyncResult passed");

                // if the state is currently stopped then change it to running and allocate a new identity value from 
                // the identity source. The identity value is used to correlate timer callback events to the currently
                // running timeout and prevents a late timer callback affecting a result it does not relate to
                int previousTimeoutState = Interlocked.CompareExchange(ref _timeoutState, TimeoutState.Running, TimeoutState.Stopped);
                Debug.Assert(previousTimeoutState == TimeoutState.Stopped, "previous timeout state was not Stopped");
                if (previousTimeoutState == TimeoutState.Stopped)
                {
                    Debug.Assert(_timeoutIdentityValue == 0, "timer was previously stopped without resetting the _identityValue");
                    _timeoutIdentityValue = Interlocked.Increment(ref _timeoutIdentitySource);
                }

                _networkPacketTimeout?.Dispose();

                _networkPacketTimeout = new Timer(
                    new TimerCallback(OnTimeoutAsync),
                    new TimeoutState(_timeoutIdentityValue),
                    Timeout.Infinite,
                    Timeout.Infinite
                );

                // -1 == Infinite
                //  0 == Already timed out (NOTE: To simulate the same behavior as sync we will only timeout on 0 if we receive an IO Pending from SNI)
                // >0 == Actual timeout remaining
                int msecsRemaining = GetTimeoutRemaining();
                if (msecsRemaining > 0)
                {
                    ChangeNetworkPacketTimeout(msecsRemaining, Timeout.Infinite);
                }

                SNIHandle handle = null;

                RuntimeHelpers.PrepareConstrainedRegions();
                try
                { }
                finally
                {
                    Interlocked.Increment(ref _readingCount);

                    handle = Handle;
                    if (handle != null)
                    {

                        IncrementPendingCallbacks();

                        error = SNINativeMethodWrapper.SNIReadAsync(handle, ref readPacket);

                        if (!(TdsEnums.SNI_SUCCESS == error || TdsEnums.SNI_SUCCESS_IO_PENDING == error))
                        {
                            DecrementPendingCallbacks(false); // Failure - we won't receive callback!
                        }
                    }

                    Interlocked.Decrement(ref _readingCount);
                }

                if (handle == null)
                {
                    throw ADP.ClosedConnectionError();
                }

                if (TdsEnums.SNI_SUCCESS == error)
                { // Success - process results!
                    Debug.Assert(ADP.s_ptrZero != readPacket, "ReadNetworkPacket should not have been null on this async operation!");
                    ReadAsyncCallback(ADP.s_ptrZero, readPacket, 0);
                }
                else if (TdsEnums.SNI_SUCCESS_IO_PENDING != error)
                { // FAILURE!
                    Debug.Assert(IntPtr.Zero == readPacket, "unexpected readPacket without corresponding SNIPacketRelease");
                    ReadSniError(this, error);
#if DEBUG
                    if ((_forcePendingReadsToWaitForUser) && (_realNetworkPacketTaskSource != null))
                    {
                        _realNetworkPacketTaskSource.TrySetResult(null);
                    }
                    else
#endif
                    {
                        _networkPacketTaskSource.TrySetResult(null);
                    }
                    // Disable timeout timer on error
                    SetTimeoutStateStopped();
                    ChangeNetworkPacketTimeout(Timeout.Infinite, Timeout.Infinite);
                }
                else if (msecsRemaining == 0)
                {
                    // Got IO Pending, but we have no time left to wait
                    // disable the timer and set the error state by calling OnTimeoutSync
                    ChangeNetworkPacketTimeout(Timeout.Infinite, Timeout.Infinite);
                    OnTimeoutSync();
                }
                // DO NOT HANDLE PENDING READ HERE - which is TdsEnums.SNI_SUCCESS_IO_PENDING state.
                // That is handled by user who initiated async read, or by ReadNetworkPacket which is sync over async.
            }
            finally
            {
                if (readPacket != IntPtr.Zero)
                {
                    // Be sure to release packet, otherwise it will be leaked by native.
                    SNINativeMethodWrapper.SNIPacketRelease(readPacket);
                }

                AssertValidState();
            }
        }

        /// <summary>
        /// Checks to see if the underlying connection is still alive (used by connection pool resilency)
        /// NOTE: This is not safe to do on a connection that is currently in use
        /// NOTE: This will mark the connection as broken if it is found to be dead
        /// </summary>
        /// <param name="throwOnException">If true then an exception will be thrown if the connection is found to be dead, otherwise no exception will be thrown</param>
        /// <returns>True if the connection is still alive, otherwise false</returns>
        internal bool IsConnectionAlive(bool throwOnException)
        {
            Debug.Assert(_parser.Connection == null || _parser.Connection.Pool != null, "Shouldn't be calling IsConnectionAlive on non-pooled connections");
            bool isAlive = true;

            if (DateTime.UtcNow.Ticks - _lastSuccessfulIOTimer._value > CheckConnectionWindow)
            {
                if ((_parser == null) || ((_parser.State == TdsParserState.Broken) || (_parser.State == TdsParserState.Closed)))
                {
                    isAlive = false;
                    if (throwOnException)
                    {
                        throw SQL.ConnectionDoomed();
                    }
                }
                else if ((_pendingCallbacks > 1) || ((_parser.Connection != null) && (!_parser.Connection.IsInPool)))
                {
                    // This connection is currently in use, assume that the connection is 'alive'
                    // NOTE: SNICheckConnection is not currently supported for connections that are in use
                    Debug.Assert(true, "Call to IsConnectionAlive while connection is in use");
                }
                else
                {
                    UInt32 error;
                    IntPtr readPacket = IntPtr.Zero;

                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    {
                        TdsParser.ReliabilitySection.Assert("unreliable call to IsConnectionAlive");  // you need to setup for a thread abort somewhere before you call this method


                        SniContext = SniContext.Snix_Connect;
                        error = SNINativeMethodWrapper.SNICheckConnection(Handle);

                        if ((error != TdsEnums.SNI_SUCCESS) && (error != TdsEnums.SNI_WAIT_TIMEOUT))
                        {
                            // Connection is dead
                            SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.IsConnectionAlive|Info> received error {0} on idle connection", (int)error);

                            isAlive = false;
                            if (throwOnException)
                            {
                                // Get the error from SNI so that we can throw the correct exception
                                AddError(_parser.ProcessSNIError(this));
                                ThrowExceptionAndWarning();
                            }
                        }
                        else
                        {
                            _lastSuccessfulIOTimer._value = DateTime.UtcNow.Ticks;
                        }
                    }
                    finally
                    {
                        if (readPacket != IntPtr.Zero)
                        {
                            // Be sure to release packet, otherwise it will be leaked by native.
                            SNINativeMethodWrapper.SNIPacketRelease(readPacket);
                        }

                    }
                }
            }

            return isAlive;
        }

        /// <summary>
        /// Checks to see if the underlying connection is still valid (used by idle connection resiliency - for active connections) 
        /// NOTE: This is not safe to do on a connection that is currently in use
        /// NOTE: This will mark the connection as broken if it is found to be dead
        /// </summary>
        /// <returns>True if the connection is still alive, otherwise false</returns>
        internal bool ValidateSNIConnection()
        {
            if ((_parser == null) || ((_parser.State == TdsParserState.Broken) || (_parser.State == TdsParserState.Closed)))
            {
                return false;
            }

            if (DateTime.UtcNow.Ticks - _lastSuccessfulIOTimer._value <= CheckConnectionWindow)
            {
                return true;
            }

            UInt32 error = TdsEnums.SNI_SUCCESS;
            SniContext = SniContext.Snix_Connect;
            try
            {
                Interlocked.Increment(ref _readingCount);
                SNIHandle handle = Handle;
                if (handle != null)
                {
                    error = SNINativeMethodWrapper.SNICheckConnection(handle);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _readingCount);
            }
            return (error == TdsEnums.SNI_SUCCESS) || (error == TdsEnums.SNI_WAIT_TIMEOUT);
        }

        // This method should only be called by ReadSni!  If not - it may have problems with timeouts!
        private void ReadSniError(TdsParserStateObject stateObj, UInt32 error)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadSniSyncError");  // you need to setup for a thread abort somewhere before you call this method

            if (TdsEnums.SNI_WAIT_TIMEOUT == error)
            {
                Debug.Assert(_syncOverAsync, "Should never reach here with async on!");
                bool fail = false;

                if (IsTimeoutStateExpired)
                { // This is now our second timeout - time to give up.
                    fail = true;
                }
                else
                {
                    stateObj.SetTimeoutStateStopped();
                    Debug.Assert(_parser.Connection != null, "SqlConnectionInternalTds handler can not be null at this point.");
                    AddError(new SqlError(TdsEnums.TIMEOUT_EXPIRED, (byte)0x00, TdsEnums.MIN_ERROR_CLASS, _parser.Server, _parser.Connection.TimeoutErrorInternal.GetErrorMessage(), "", 0, TdsEnums.SNI_WAIT_TIMEOUT));

                    if (!stateObj._attentionSent)
                    {
                        if (stateObj.Parser.State == TdsParserState.OpenLoggedIn)
                        {
                            stateObj.SendAttention(mustTakeWriteLock: true);

                            IntPtr syncReadPacket = IntPtr.Zero;
                            RuntimeHelpers.PrepareConstrainedRegions();
                            bool shouldDecrement = false;
                            try
                            {
                                Interlocked.Increment(ref _readingCount);
                                shouldDecrement = true;

                                SNIHandle handle = Handle;
                                if (handle == null)
                                {
                                    throw ADP.ClosedConnectionError();
                                }

                                error = SNINativeMethodWrapper.SNIReadSyncOverAsync(handle, ref syncReadPacket, stateObj.GetTimeoutRemaining());

                                Interlocked.Decrement(ref _readingCount);
                                shouldDecrement = false;

                                if (TdsEnums.SNI_SUCCESS == error)
                                {
                                    // We will end up letting the run method deal with the expected done:done_attn token stream.
                                    stateObj.ProcessSniPacket(syncReadPacket, 0);
                                    return;
                                }
                                else
                                {
                                    Debug.Assert(IntPtr.Zero == syncReadPacket, "unexpected syncReadPacket without corresponding SNIPacketRelease");
                                    fail = true; // Subsequent read failed, time to give up.
                                }
                            }
                            finally
                            {
                                if (shouldDecrement)
                                {
                                    Interlocked.Decrement(ref _readingCount);
                                }

                                if (syncReadPacket != IntPtr.Zero)
                                {
                                    // Be sure to release packet, otherwise it will be leaked by native.
                                    SNINativeMethodWrapper.SNIPacketRelease(syncReadPacket);
                                }
                            }
                        }
                        else
                        {
                            if (_parser._loginWithFailover)
                            {
                                // For DB Mirroring Failover during login, never break the connection, just close the TdsParser (Devdiv 846298)
                                _parser.Disconnect();
                            }
                            else if ((_parser.State == TdsParserState.OpenNotLoggedIn) && (_parser.Connection.ConnectionOptions.MultiSubnetFailover || _parser.Connection.ConnectionOptions.TransparentNetworkIPResolution))
                            {
                                // For MultiSubnet Failover during login, never break the connection, just close the TdsParser
                                _parser.Disconnect();
                            }
                            else
                                fail = true; // We aren't yet logged in - just fail.
                        }
                    }
                }

                if (fail)
                {
                    _parser.State = TdsParserState.Broken; // We failed subsequent read, we have to quit!
                    _parser.Connection.BreakConnection();
                }
            }
            else
            {
                // Caution: ProcessSNIError  always  returns a fatal error!
                AddError(_parser.ProcessSNIError(stateObj));
            }
            ThrowExceptionAndWarning();

            AssertValidState();
        }

        // TODO: - does this need to be MUSTRUN???
        public void ProcessSniPacket(IntPtr packet, UInt32 error)
        {
            if (error != 0)
            {
                if ((_parser.State == TdsParserState.Closed) || (_parser.State == TdsParserState.Broken))
                {
                    // Do nothing with callback if closed or broken and error not 0 - callback can occur
                    // after connection has been closed.  PROBLEM IN NETLIB - DESIGN FLAW.
                    return;
                }

                AddError(_parser.ProcessSNIError(this));
                AssertValidState();
            }
            else
            {
                UInt32 dataSize = 0;
                UInt32 getDataError = SNINativeMethodWrapper.SNIPacketGetData(packet, _inBuff, ref dataSize);

                if (getDataError == TdsEnums.SNI_SUCCESS)
                {
                    if (_inBuff.Length < dataSize)
                    {
                        Debug.Assert(true, "Unexpected dataSize on Read");
                        throw SQL.InvalidInternalPacketSize(StringsHelper.GetString(Strings.SqlMisc_InvalidArraySizeMessage));
                    }

                    _lastSuccessfulIOTimer._value = DateTime.UtcNow.Ticks;
                    _inBytesRead = (int)dataSize;
                    _inBytesUsed = 0;

                    if (_snapshot != null)
                    {
                        _snapshot.PushBuffer(_inBuff, _inBytesRead);
                        if (_snapshotReplay)
                        {
                            _snapshot.Replay();
#if DEBUG
                            _snapshot.AssertCurrent();
#endif
                        }
                    }
                    SniReadStatisticsAndTracing();
                    SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParser.ReadNetworkPacketAsyncCallback | INFO | ADV | State Object Id {0}, Packet read. In Buffer {1}, In Bytes Read: {2}", ObjectID, _inBuff, (ushort)_inBytesRead);
                    AssertValidState();
                }
                else
                {
                    throw SQL.ParsingError(ParsingErrorState.ProcessSniPacketFailed);
                }
            }
        }

        private void ChangeNetworkPacketTimeout(int dueTime, int period)
        {
            Timer networkPacketTimeout = _networkPacketTimeout;
            if (networkPacketTimeout != null)
            {
                try
                {
                    networkPacketTimeout.Change(dueTime, period);
                }
                catch (ObjectDisposedException)
                {
                    // _networkPacketTimeout is set to null before Disposing, but there is still a slight chance
                    // that object was disposed after we took a copy
                }
            }
        }

        public void ReadAsyncCallback(IntPtr key, IntPtr packet, UInt32 error)
        { // Key never used.
            // Note - it's possible that when native calls managed that an asynchronous exception
            // could occur in the native->managed transition, which would
            // have two impacts:
            // 1) user event not called
            // 2) DecrementPendingCallbacks not called, which would mean this object would be leaked due
            //    to the outstanding GCRoot until AppDomain.Unload.
            // We live with the above for the time being due to the constraints of the current
            // reliability infrastructure provided by the CLR.

            TaskCompletionSource<object> source = _networkPacketTaskSource;
#if DEBUG
            if ((_forcePendingReadsToWaitForUser) && (_realNetworkPacketTaskSource != null))
            {
                source = _realNetworkPacketTaskSource;
            }
#endif

            // The mars physical connection can get a callback
            // with a packet but no result after the connection is closed.
            if (source == null && _parser._pMarsPhysicalConObj == this)
            {
                return;
            }

            RuntimeHelpers.PrepareConstrainedRegions();
            bool processFinallyBlock = true;
            try
            {
                Debug.Assert(IntPtr.Zero == packet || IntPtr.Zero != packet && source != null, "AsyncResult null on callback");
                if (_parser.MARSOn)
                { // Only take reset lock on MARS and Async.
                    CheckSetResetConnectionState(error, CallbackType.Read);
                }

                ChangeNetworkPacketTimeout(Timeout.Infinite, Timeout.Infinite);

                // The timer thread may be unreliable under high contention scenarios. It cannot be
                // assumed that the timeout has happened on the timer thread callback. Check the timeout
                // synchrnously and then call OnTimeoutSync to force an atomic change of state.
                if (TimeoutHasExpired)
                {
                    OnTimeoutSync();
                }

                // try to change to the stopped state but only do so if currently in the running state
                // and use cmpexch so that all changes out of the running state are atomic
                int previousState = Interlocked.CompareExchange(ref _timeoutState, TimeoutState.Stopped, TimeoutState.Running);

                // if the state is anything other than running then this query has reached an end so
                // set the correlation _timeoutIdentityValue to 0 to prevent late callbacks executing
                if (_timeoutState != TimeoutState.Running)
                {
                    _timeoutIdentityValue = 0;
                }

                ProcessSniPacket(packet, error);
            }
            catch (Exception e)
            {
                processFinallyBlock = ADP.IsCatchableExceptionType(e);
                throw;
            }
            finally
            {
                // pendingCallbacks may be 2 after decrementing, this indicates that a fatal timeout is occuring, and therefore we shouldn't complete the task
                int pendingCallbacks = DecrementPendingCallbacks(false); // may dispose of GC handle.
                if ((processFinallyBlock) && (source != null) && (pendingCallbacks < 2))
                {
                    if (error == 0)
                    {
                        if (_executionContext != null)
                        {
                            ExecutionContext.Run(_executionContext, (state) => source.TrySetResult(null), null);
                        }
                        else
                        {
                            source.TrySetResult(null);
                        }
                    }
                    else
                    {
                        if (_executionContext != null)
                        {
                            ExecutionContext.Run(_executionContext, (state) => ReadAsyncCallbackCaptureException(source), null);
                        }
                        else
                        {
                            ReadAsyncCallbackCaptureException(source);
                        }
                    }
                }

                AssertValidState();
            }
        }

        private void ReadAsyncCallbackCaptureException(TaskCompletionSource<object> source)
        {
            bool captureSuccess = false;
            try
            {
                if (_hasErrorOrWarning)
                {
                    // Do the close on another thread, since we don't want to block the callback thread
                    ThrowExceptionAndWarning(asyncClose: true);
                }
                else if ((_parser.State == TdsParserState.Closed) || (_parser.State == TdsParserState.Broken))
                {
                    // Connection was closed by another thread before we parsed the packet, so no error was added to the collection
                    throw ADP.ClosedConnectionError();
                }
            }
            catch (Exception ex)
            {
                if (source.TrySetException(ex))
                {
                    // There was an exception, and it was successfully stored in the task
                    captureSuccess = true;
                }
            }

            if (!captureSuccess)
            {
                // Either there was no exception, or the task was already completed
                // This is unusual, but possible if a fatal timeout occurred on another thread (which should mean that the connection is now broken)
                Debug.Assert(_parser.State == TdsParserState.Broken || _parser.State == TdsParserState.Closed || _parser.Connection.IsConnectionDoomed, "Failed to capture exception while the connection was still healthy");

                // The safest thing to do is to ensure that the connection is broken and attempt to cancel the task
                // This must be done from another thread to not block the callback thread                
                Task.Factory.StartNew(() =>
                {
                    _parser.State = TdsParserState.Broken;
                    _parser.Connection.BreakConnection();
                    source.TrySetCanceled();
                });
            }
        }

#pragma warning disable 420 // a reference to a volatile field will not be treated as volatile

        public void WriteAsyncCallback(IntPtr key, IntPtr packet, UInt32 sniError)
        { // Key never used.
            RemovePacketFromPendingList(packet);
            try
            {
                if (sniError != TdsEnums.SNI_SUCCESS)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.WriteAsyncCallback|Info> write async returned error code {0}", (int)sniError);
                    try
                    {
                        AddError(_parser.ProcessSNIError(this));
                        ThrowExceptionAndWarning(asyncClose: true);
                    }
                    catch (Exception e)
                    {
                        var writeCompletionSource = _writeCompletionSource;
                        if (writeCompletionSource != null)
                        {
                            writeCompletionSource.TrySetException(e);
                        }
                        else
                        {
                            _delayedWriteAsyncCallbackException = e;

                            // Ensure that _delayedWriteAsyncCallbackException is set before checking _writeCompletionSource
                            Thread.MemoryBarrier();

                            // Double check that _writeCompletionSource hasn't been created in the meantime
                            writeCompletionSource = _writeCompletionSource;
                            if (writeCompletionSource != null)
                            {
                                var delayedException = Interlocked.Exchange(ref _delayedWriteAsyncCallbackException, null);
                                if (delayedException != null)
                                {
                                    writeCompletionSource.TrySetException(delayedException);
                                }
                            }
                        }

                        return;
                    }
                }
                else
                {
                    _lastSuccessfulIOTimer._value = DateTime.UtcNow.Ticks;
                }
            }
            finally
            {
#if DEBUG
                if (SqlCommand.DebugForceAsyncWriteDelay > 0)
                {
                    new Timer(obj =>
                    {
                        Interlocked.Decrement(ref _asyncWriteCount);
                        var writeCompletionSource = _writeCompletionSource;
                        if (_asyncWriteCount == 0 && writeCompletionSource != null)
                        {
                            writeCompletionSource.TrySetResult(null);
                        }
                    }, null, SqlCommand.DebugForceAsyncWriteDelay, Timeout.Infinite);
                }
                else
                {
#else
                {
#endif
                    Interlocked.Decrement(ref _asyncWriteCount);
                }
            }
#if DEBUG
            if (SqlCommand.DebugForceAsyncWriteDelay > 0)
            {
                return;
            }
#endif
            var completionSource = _writeCompletionSource;
            if (_asyncWriteCount == 0 && completionSource != null)
            {
                completionSource.TrySetResult(null);
            }
        }

#pragma warning restore 420

        /////////////////////////////////////////
        // Network/Packet Writing & Processing //
        /////////////////////////////////////////


        //
        // Takes a secure string and offsets and saves them for a write latter when the information is written out to SNI Packet
        //  This method is provided to better handle the life cycle of the clear text of the secure string
        //  This method also ensures that the clear text is not held in the unpined managed buffer so that it avoids getting moved around by CLR garbage collector
        //  TdsParserStaticMethods.EncryptPassword operation is also done in the unmanaged buffer for the clear text later
        //
        internal void WriteSecureString(SecureString secureString)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to WriteSecureString");  // you need to setup for a thread abort somewhere before you call this method

            Debug.Assert(_securePasswords[0] == null || _securePasswords[1] == null, "There are more than two secure passwords");

            int index = _securePasswords[0] != null ? 1 : 0;

            _securePasswords[index] = secureString;
            _securePasswordOffsetsInBuffer[index] = _outBytesUsed;

            // loop through and write the entire array
            int lengthInBytes = secureString.Length * 2;

            // It is guaranteed both secure password and secure change password should fit into the first packet
            // Given current TDS format and implementation it is not possible that one of secure string is the last item and exactly fill up the output buffer
            //  if this ever happens and it is correct situation, the packet needs to be written out after _outBytesUsed is update
            Debug.Assert((_outBytesUsed + lengthInBytes) < _outBuff.Length, "Passwords cannot be split into two different packet or the last item which fully fill up _outBuff!!!");

            _outBytesUsed += lengthInBytes;
        }

        // ResetSecurePasswordsInformation: clears information regarding secure passwords when login is done; called from TdsParser.TdsLogin
        internal void ResetSecurePasswordsInfomation()
        {
            for (int i = 0; i < _securePasswords.Length; ++i)
            {
                _securePasswords[i] = null;
                _securePasswordOffsetsInBuffer[i] = 0;
            }
        }

        internal Task WaitForAccumulatedWrites()
        {
            // Checked for stored exceptions
#pragma warning disable 420 // A reference to a volatile field will not be treated as volatile - Disabling since the Interlocked APIs are volatile aware
            var delayedException = Interlocked.Exchange(ref _delayedWriteAsyncCallbackException, null);
            if (delayedException != null)
            {
                throw delayedException;
            }
#pragma warning restore 420 

            if (_asyncWriteCount == 0)
            {
                return null;
            }

            _writeCompletionSource = new TaskCompletionSource<object>();
            Task task = _writeCompletionSource.Task;

            // Ensure that _writeCompletionSource is set before checking state
            Thread.MemoryBarrier();

            // Now that we have set _writeCompletionSource, check if parser is closed or broken
            if ((_parser.State == TdsParserState.Closed) || (_parser.State == TdsParserState.Broken))
            {
                throw ADP.ClosedConnectionError();
            }

            // Check for stored exceptions
#pragma warning disable 420 // A reference to a volatile field will not be treated as volatile - Disabling since the Interlocked APIs are volatile aware
            delayedException = Interlocked.Exchange(ref _delayedWriteAsyncCallbackException, null);
            if (delayedException != null)
            {
                throw delayedException;
            }
#pragma warning restore 420 

            // If there are no outstanding writes, see if we can shortcut and return null
            if ((_asyncWriteCount == 0) && ((!task.IsCompleted) || (task.Exception == null)))
            {
                task = null;
            }

            return task;
        }

        // Takes in a single byte and writes it to the buffer.  If the buffer is full, it is flushed
        // and then the buffer is re-initialized in flush() and then the byte is put in the buffer.
        internal void WriteByte(byte b)
        {
            TdsParser.ReliabilitySection.Assert("unreliable call to WriteByte");  // you need to setup for a thread abort somewhere before you call this method

            Debug.Assert(_outBytesUsed <= _outBuff.Length, "ERROR - TDSParser: _outBytesUsed > _outBuff.Length");

            // check to make sure we haven't used the full amount of space available in the buffer, if so, flush it
            if (_outBytesUsed == _outBuff.Length)
            {
                WritePacket(TdsEnums.SOFTFLUSH, canAccumulate: true);
            }
            // set byte in buffer and increment the counter for number of bytes used in the out buffer
            _outBuff[_outBytesUsed++] = b;
        }

        //
        // Takes a byte array and writes it to the buffer.
        //
        internal Task WriteByteArray(Byte[] b, int len, int offsetBuffer, bool canAccumulate = true, TaskCompletionSource<object> completion = null)
        {
            try
            {
                TdsParser.ReliabilitySection.Assert("unreliable call to WriteByteArray");  // you need to setup for a thread abort somewhere before you call this method

                bool async = _parser._asyncWrite;  // NOTE: We are capturing this now for the assert after the Task is returned, since WritePacket will turn off async if there is an exception
                Debug.Assert(async || _asyncWriteCount == 0);
                // Do we have to send out in packet size chunks, or can we rely on netlib layer to break it up?
                // would prefer to to do something like:
                //
                // if (len > what we have room for || len > out buf)
                //   flush buffer
                //   UnsafeNativeMethods.Write(b)
                //

                int offset = offsetBuffer;

                Debug.Assert(b.Length >= len, "Invalid length sent to WriteByteArray()!");

                // loop through and write the entire array
                do
                {
                    if ((_outBytesUsed + len) > _outBuff.Length)
                    {
                        // If the remainder of the string won't fit into the buffer, then we have to put
                        // whatever we can into the buffer, and flush that so we can then put more into
                        // the buffer on the next loop of the while.

                        int remainder = _outBuff.Length - _outBytesUsed;

                        // write the remainder
                        Buffer.BlockCopy(b, offset, _outBuff, _outBytesUsed, remainder);

                        // handle counters
                        offset += remainder;
                        _outBytesUsed += remainder;
                        len -= remainder;

                        Task packetTask = WritePacket(TdsEnums.SOFTFLUSH, canAccumulate);

                        if (packetTask != null)
                        {
                            Task task = null;
                            Debug.Assert(async, "Returned task in sync mode");
                            if (completion == null)
                            {
                                completion = new TaskCompletionSource<object>();
                                task = completion.Task; // we only care about return from topmost call, so do not access Task property in other cases
                            }
                            WriteByteArraySetupContinuation(b, len, completion, offset, packetTask);
                            return task;
                        }

                    }
                    else
                    { //((stateObj._outBytesUsed + len) <= stateObj._outBuff.Length )
                        // Else the remainder of the string will fit into the buffer, so copy it into the
                        // buffer and then break out of the loop.

                        Buffer.BlockCopy(b, offset, _outBuff, _outBytesUsed, len);

                        // handle out buffer bytes used counter
                        _outBytesUsed += len;
                        break;
                    }
                } while (len > 0);

                if (completion != null)
                {
                    completion.SetResult(null);
                }
                return null;
            }
            catch (Exception e)
            {
                if (completion != null)
                {
                    completion.SetException(e);
                    return null;
                }
                else
                {
                    throw;
                }
            }
        }

        // This is in its own method to avoid always allocating the lambda in WriteByteArray
        private void WriteByteArraySetupContinuation(byte[] b, int len, TaskCompletionSource<object> completion, int offset, Task packetTask)
        {
            AsyncHelper.ContinueTask(packetTask, completion,
                () => WriteByteArray(b, len: len, offsetBuffer: offset, canAccumulate: false, completion: completion),
                connectionToDoom: _parser.Connection
            );
        }

        // Dumps contents of buffer to SNI for network write.
        internal Task WritePacket(byte flushMode, bool canAccumulate = false)
        {
            TdsParserState state = _parser.State;
            if ((state == TdsParserState.Closed) || (state == TdsParserState.Broken))
            {
                throw ADP.ClosedConnectionError();
            }

            if (
                // This appears to be an optimization to avoid writing empty packets in 2005
                // However, since we don't know the version prior to login Is2005OrNewer was always false prior to login
                // So removing the Is2005OrNewer check causes issues since the login packet happens to meet the rest of the conditions below
                // So we need to avoid this check prior to login completing
                state == TdsParserState.OpenLoggedIn &&
                !_bulkCopyOpperationInProgress // ignore the condition checking for bulk copy (SQL BU 414551)
                    && _outBytesUsed == (_outputHeaderLen + BitConverter.ToInt32(_outBuff, _outputHeaderLen))
                    && _outputPacketCount == 0
                || _outBytesUsed == _outputHeaderLen
                    && _outputPacketCount == 0)
            {
                return null;
            }

            byte status;
            byte packetNumber = _outputPacketNumber;

            // Set Status byte based whether this is end of message or not
            bool willCancel = (_cancelled) && (_parser._asyncWrite);
            if (willCancel)
            {
                status = TdsEnums.ST_EOM | TdsEnums.ST_IGNORE;
                ResetPacketCounters();
            }
            else if (TdsEnums.HARDFLUSH == flushMode)
            {
                status = TdsEnums.ST_EOM;
                ResetPacketCounters();
            }
            else if (TdsEnums.SOFTFLUSH == flushMode)
            {
                status = TdsEnums.ST_BATCH;
                _outputPacketNumber++;
                _outputPacketCount++;
            }
            else
            {
                status = TdsEnums.ST_EOM;
                Debug.Fail($"Unexpected argument {flushMode,-2:x2} to WritePacket");
            }

            _outBuff[0] = _outputMessageType;         // Message Type
            _outBuff[1] = status;
            _outBuff[2] = (byte)(_outBytesUsed >> 8); // length - upper byte
            _outBuff[3] = (byte)(_outBytesUsed & 0xff); // length - lower byte
            _outBuff[4] = 0;                          // channel
            _outBuff[5] = 0;
            _outBuff[6] = packetNumber;               // packet
            _outBuff[7] = 0;                          // window

            Task task = null;
            _parser.CheckResetConnection(this);       // HAS SIDE EFFECTS - re-org at a later time if possible

            task = WriteSni(canAccumulate);
            AssertValidState();

            if (willCancel)
            {
                // If we have been cancelled, then ensure that we write the ATTN packet as well
                task = AsyncHelper.CreateContinuationTask(task, CancelWritePacket, _parser.Connection);
            }

            return task;
        }

        private void CancelWritePacket()
        {
            Debug.Assert(_cancelled, "Should not call CancelWritePacket if _cancelled is not set");

            _parser.Connection.ThreadHasParserLockForClose = true;      // In case of error, let the connection know that we are holding the lock
            try
            {
                // Send the attention and wait for the ATTN_ACK
                SendAttention();
                ResetCancelAndProcessAttention();

                // Let the caller know that we've given up
                throw SQL.OperationCancelled();
            }
            finally
            {
                _parser.Connection.ThreadHasParserLockForClose = false;
            }
        }

#pragma warning disable 420 // a reference to a volatile field will not be treated as volatile

        private Task SNIWritePacket(SNIHandle handle, SNIPacket packet, out UInt32 sniError, bool canAccumulate, bool callerHasConnectionLock)
        {
            // Check for a stored exception
            var delayedException = Interlocked.Exchange(ref _delayedWriteAsyncCallbackException, null);
            if (delayedException != null)
            {
                throw delayedException;
            }

            Task task = null;
            _writeCompletionSource = null;
            IntPtr packetPointer = IntPtr.Zero;
            bool sync = !_parser._asyncWrite;
            if (sync && _asyncWriteCount > 0)
            { // for example, SendAttention while there are writes pending
                Task waitForWrites = WaitForAccumulatedWrites();
                if (waitForWrites != null)
                {
                    try
                    {
                        waitForWrites.Wait();
                    }
                    catch (AggregateException ae)
                    {
                        throw ae.InnerException;
                    }
                }
                Debug.Assert(_asyncWriteCount == 0, "All async write should be finished");
            }
            if (!sync)
            {
                // Add packet to the pending list (since the callback can happen any time after we call SNIWritePacket)
                packetPointer = AddPacketToPendingList(packet);
            }
            // Async operation completion may be delayed (success pending).
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
            }
            finally
            {
                sniError = SNINativeMethodWrapper.SNIWritePacket(handle, packet, sync);
            }
            if (sniError == TdsEnums.SNI_SUCCESS_IO_PENDING)
            {
                Debug.Assert(!sync, "Completion should be handled in SniManagedWrapper");
                Interlocked.Increment(ref _asyncWriteCount);
                Debug.Assert(_asyncWriteCount >= 0);
                if (!canAccumulate)
                {
                    // Create completion source (for callback to complete)
                    _writeCompletionSource = new TaskCompletionSource<object>();
                    task = _writeCompletionSource.Task;

                    // Ensure that setting _writeCompletionSource completes before checking _delayedWriteAsyncCallbackException
                    Thread.MemoryBarrier();

                    // Check for a stored exception
                    delayedException = Interlocked.Exchange(ref _delayedWriteAsyncCallbackException, null);
                    if (delayedException != null)
                    {
                        throw delayedException;
                    }

                    // If there are no outstanding writes, see if we can shortcut and return null
                    if ((_asyncWriteCount == 0) && ((!task.IsCompleted) || (task.Exception == null)))
                    {
                        task = null;
                    }
                }
            }
#if DEBUG
            else if (!sync && !canAccumulate && SqlCommand.DebugForceAsyncWriteDelay > 0)
            {
                // Executed synchronously - callback will not be called 
                TaskCompletionSource<object> completion = new TaskCompletionSource<object>();
                uint error = sniError;
                new Timer(obj =>
                {
                    try
                    {
                        if (_parser.MARSOn)
                        { // Only take reset lock on MARS.
                            CheckSetResetConnectionState(error, CallbackType.Write);
                        }

                        if (error != TdsEnums.SNI_SUCCESS)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.WritePacket|Info> write async returned error code {0}", (int)error);

                            AddError(_parser.ProcessSNIError(this));
                            ThrowExceptionAndWarning();
                        }
                        AssertValidState();
                        completion.SetResult(null);
                    }
                    catch (Exception e)
                    {
                        completion.SetException(e);
                    }
                }, null, SqlCommand.DebugForceAsyncWriteDelay, Timeout.Infinite);
                task = completion.Task;
            }

#endif
            else
            {

                if (_parser.MARSOn)
                { // Only take reset lock on MARS.
                    CheckSetResetConnectionState(sniError, CallbackType.Write);
                }

                if (sniError == TdsEnums.SNI_SUCCESS)
                {
                    _lastSuccessfulIOTimer._value = DateTime.UtcNow.Ticks;

                    if (!sync)
                    {
                        // Since there will be no callback, remove the packet from the pending list
                        Debug.Assert(packetPointer != IntPtr.Zero, "Packet added to list has an invalid pointer, can not remove from pending list");
                        RemovePacketFromPendingList(packetPointer);
                    }
                }
                else
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.WritePacket|Info> write async returned error code {0}", (int)sniError);
                    AddError(_parser.ProcessSNIError(this));
                    ThrowExceptionAndWarning(callerHasConnectionLock);
                }
                AssertValidState();
            }
            return task;
        }

#pragma warning restore 420 

        // Sends an attention signal - executing thread will consume attn.
        internal void SendAttention(bool mustTakeWriteLock = false)
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

                SNIPacket attnPacket = new SNIPacket(Handle);
                _sniAsyncAttnPacket = attnPacket;

                SNINativeMethodWrapper.SNIPacketSetData(attnPacket, SQL.AttentionHeader, TdsEnums.HEADER_LEN, null, null);

                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    // Dev11 #344723: SqlClient stress test suspends System_Data!Tcp::ReadSync via a call to SqlDataReader::Close
                    // Set _attentionSending to true before sending attention and reset after setting _attentionSent
                    // This prevents a race condition between receiving the attention ACK and setting _attentionSent
                    _attentionSending = true;

#if DEBUG
                    if (!_skipSendAttention)
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

                            UInt32 sniError;
                            _parser._asyncWrite = false; // stop async write 
                            SNIWritePacket(Handle, attnPacket, out sniError, canAccumulate: false, callerHasConnectionLock: false);
                            SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.SendAttention|{0}> Send Attention ASync.", "Info");
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

                SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParser.WritePacket | INFO | ADV | State Object Id {0}, Packet sent. Out Buffer {1}, Out Bytes Used {2}", ObjectID, _outBuff, (ushort)_outBytesUsed);
                SqlClientEventSource.Log.TryTraceEvent("TdsParser.SendAttention | INFO | Attention sent to the server.");

                AssertValidState();
            }
        }

        private Task WriteSni(bool canAccumulate)
        {
            // Prepare packet, and write to packet.
            SNIPacket packet = GetResetWritePacket();
            SNINativeMethodWrapper.SNIPacketSetData(packet, _outBuff, _outBytesUsed, _securePasswords, _securePasswordOffsetsInBuffer);

            uint sniError;
            Debug.Assert(Parser.Connection._parserLock.ThreadMayHaveLock(), "Thread is writing without taking the connection lock");
            Task task = SNIWritePacket(Handle, packet, out sniError, canAccumulate, callerHasConnectionLock: true);

            // Check to see if the timeout has occurred.  This time out code is special case code to allow BCP writes to timeout to fix bug 350558, eventually we should make all writes timeout.
            if (_bulkCopyOpperationInProgress && 0 == GetTimeoutRemaining())
            {
                _parser.Connection.ThreadHasParserLockForClose = true;
                try
                {
                    Debug.Assert(_parser.Connection != null, "SqlConnectionInternalTds handler can not be null at this point.");
                    AddError(new SqlError(TdsEnums.TIMEOUT_EXPIRED, (byte)0x00, TdsEnums.MIN_ERROR_CLASS, _parser.Server, _parser.Connection.TimeoutErrorInternal.GetErrorMessage(), "", 0, TdsEnums.SNI_WAIT_TIMEOUT));
                    _bulkCopyWriteTimeout = true;
                    SendAttention();
                    _parser.ProcessPendingAck(this);
                    ThrowExceptionAndWarning();
                }
                finally
                {
                    _parser.Connection.ThreadHasParserLockForClose = false;
                }
            }

            // Special case logic for encryption removal.
            // TODO UNDONE BUG - we really should move this so we don't have to do this for each write!
            if (_parser.State == TdsParserState.OpenNotLoggedIn &&
                (_parser.EncryptionOptions & EncryptionOptions.OPTIONS_MASK) == EncryptionOptions.LOGIN)
            {
                // If no error occurred, and we are Open but not logged in, and
                // our encryptionOption state is login, remove the SSL Provider.
                // We only need encrypt the very first packet of the login message to the server.

                // SQL BU DT 332481 - we wanted to encrypt entire login channel, but there is
                // currently no mechanism to communicate this.  Removing encryption post 1st packet
                // is a hard-coded agreement between client and server.  We need some mechanism or
                // common change to be able to make this change in a non-breaking fasion.
                _parser.RemoveEncryption();                        // Remove the SSL Provider.
                _parser.EncryptionOptions = EncryptionOptions.OFF | (_parser.EncryptionOptions & ~EncryptionOptions.OPTIONS_MASK); // Turn encryption off.

                // Since this packet was associated with encryption, dispose and re-create.
                ClearAllWritePackets();
            }

            SniWriteStatisticsAndTracing();

            ResetBuffer();

            AssertValidState();
            return task;
        }

        internal SNIPacket GetResetWritePacket()
        {
            if (_sniPacket != null)
            {
                SNINativeMethodWrapper.SNIPacketReset(Handle, SNINativeMethodWrapper.IOType.WRITE, _sniPacket, SNINativeMethodWrapper.ConsumerNumber.SNI_Consumer_SNI);
            }
            else
            {
                lock (_writePacketLockObject)
                {
                    _sniPacket = _writePacketCache.Take(Handle);
                }
            }
            return _sniPacket;
        }

        internal void ClearAllWritePackets()
        {
            if (_sniPacket != null)
            {
                _sniPacket.Dispose();
                _sniPacket = null;
            }
            lock (_writePacketLockObject)
            {
                Debug.Assert(_pendingWritePackets.Count == 0 && _asyncWriteCount == 0, "Should not clear all write packets if there are packets pending");
                _writePacketCache.Clear();
            }
        }

        private IntPtr AddPacketToPendingList(SNIPacket packet)
        {
            Debug.Assert(packet == _sniPacket, "Adding a packet other than the current packet to the pending list");
            _sniPacket = null;
            IntPtr pointer = packet.DangerousGetHandle();

            lock (_writePacketLockObject)
            {
                _pendingWritePackets.Add(pointer, packet);
            }

            return pointer;
        }

        private void RemovePacketFromPendingList(IntPtr pointer)
        {
            SNIPacket recoveredPacket;

            lock (_writePacketLockObject)
            {
                if (_pendingWritePackets.TryGetValue(pointer, out recoveredPacket))
                {
                    _pendingWritePackets.Remove(pointer);
                    _writePacketCache.Add(recoveredPacket);
                }
#if DEBUG
                else
                {
                    Debug.Assert(false, "Removing a packet from the pending list that was never added to it");
                }
#endif
            }
        }

        //////////////////////////////////////////////
        // Statistics, Tracing, and related methods //
        //////////////////////////////////////////////

        private void SniReadStatisticsAndTracing()
        {
            SqlStatistics statistics = Parser.Statistics;
            if (null != statistics)
            {
                if (statistics.WaitForReply)
                {
                    statistics.SafeIncrement(ref statistics._serverRoundtrips);
                    statistics.ReleaseAndUpdateNetworkServerTimer();
                }

                statistics.SafeAdd(ref statistics._bytesReceived, _inBytesRead);
                statistics.SafeIncrement(ref statistics._buffersReceived);
            }
        }

        private void SniWriteStatisticsAndTracing()
        {
            SqlStatistics statistics = _parser.Statistics;
            if (null != statistics)
            {
                statistics.SafeIncrement(ref statistics._buffersSent);
                statistics.SafeAdd(ref statistics._bytesSent, _outBytesUsed);
                statistics.RequestNetworkServerTimer();
            }
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
            SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParser.WritePacket | INFO | ADV | State Object Id {0}, Packet sent. Out buffer: {1}, Out Bytes Used: {2}", ObjectID, _outBuff, (ushort)_outBytesUsed);
        }

        [Conditional("DEBUG")]
        void AssertValidState()
        {
            if (_inBytesUsed < 0 || _inBytesRead < 0)
            {
                Debug.Fail($"Invalid TDS Parser State: either _inBytesUsed or _inBytesRead is negative: {_inBytesUsed}, {_inBytesRead}");
            }
            else if (_inBytesUsed > _inBytesRead)
            {
                Debug.Fail($"Invalid TDS Parser State: _inBytesUsed > _inBytesRead: {_inBytesUsed} > {_inBytesRead}");
            }

            // TODO: add more state validations here, remember to call AssertValidState every place the relevant fields change

            Debug.Assert(_inBytesPacket >= 0, "Packet must not be negative");
        }


        //////////////////////////////////////////////
        // Errors and Warnings                      //
        //////////////////////////////////////////////

        /// <summary>
        /// True if there is at least one error or warning (not counting the pre-attention errors\warnings)
        /// </summary>
        internal bool HasErrorOrWarning
        {
            get
            {
                return _hasErrorOrWarning;
            }
        }

        /// <summary>
        /// Adds an error to the error collection
        /// </summary>
        /// <param name="error"></param>
        internal void AddError(SqlError error)
        {
            Debug.Assert(error != null, "Trying to add a null error");

            // Switch to sync once we see an error
            _syncOverAsync = true;

            lock (_errorAndWarningsLock)
            {
                _hasErrorOrWarning = true;
                if (_errors == null)
                {
                    _errors = new SqlErrorCollection();
                }
                _errors.Add(error);
            }
        }

        /// <summary>
        /// Gets the number of errors currently in the error collection
        /// </summary>
        internal int ErrorCount
        {
            get
            {
                int count = 0;
                lock (_errorAndWarningsLock)
                {
                    if (_errors != null)
                    {
                        count = _errors.Count;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Adds an warning to the warning collection
        /// </summary>
        /// <param name="error"></param>
        internal void AddWarning(SqlError error)
        {
            Debug.Assert(error != null, "Trying to add a null error");

            // Switch to sync once we see a warning
            _syncOverAsync = true;

            lock (_errorAndWarningsLock)
            {
                _hasErrorOrWarning = true;
                if (_warnings == null)
                {
                    _warnings = new SqlErrorCollection();
                }
                _warnings.Add(error);
            }
        }

        /// <summary>
        /// Gets the number of warnings currently in the warning collection
        /// </summary>
        internal int WarningCount
        {
            get
            {
                int count = 0;
                lock (_errorAndWarningsLock)
                {
                    if (_warnings != null)
                    {
                        count = _warnings.Count;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Gets the number of errors currently in the pre-attention error collection
        /// </summary>
        internal int PreAttentionErrorCount
        {
            get
            {
                int count = 0;
                lock (_errorAndWarningsLock)
                {
                    if (_preAttentionErrors != null)
                    {
                        count = _preAttentionErrors.Count;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Gets the number of errors currently in the pre-attention warning collection
        /// </summary>
        internal int PreAttentionWarningCount
        {
            get
            {
                int count = 0;
                lock (_errorAndWarningsLock)
                {
                    if (_preAttentionWarnings != null)
                    {
                        count = _preAttentionWarnings.Count;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Gets the full list of errors and warnings (including the pre-attention ones), then wipes all error and warning lists
        /// </summary>
        /// <param name="broken">If true, the connection should be broken</param>
        /// <returns>An array containing all of the errors and warnings</returns>
        internal SqlErrorCollection GetFullErrorAndWarningCollection(out bool broken)
        {
            SqlErrorCollection allErrors = new SqlErrorCollection();
            broken = false;

            lock (_errorAndWarningsLock)
            {
                _hasErrorOrWarning = false;

                // Merge all error lists, then reset them
                AddErrorsToCollection(_errors, ref allErrors, ref broken);
                AddErrorsToCollection(_warnings, ref allErrors, ref broken);
                _errors = null;
                _warnings = null;

                // We also process the pre-attention error lists here since, if we are here and they are populated, then an error occurred while sending attention so we should show the errors now (otherwise they'd be lost)
                AddErrorsToCollection(_preAttentionErrors, ref allErrors, ref broken);
                AddErrorsToCollection(_preAttentionWarnings, ref allErrors, ref broken);
                _preAttentionErrors = null;
                _preAttentionWarnings = null;
            }

            return allErrors;
        }

        private void AddErrorsToCollection(SqlErrorCollection inCollection, ref SqlErrorCollection collectionToAddTo, ref bool broken)
        {
            if (inCollection != null)
            {
                foreach (SqlError error in inCollection)
                {
                    collectionToAddTo.Add(error);
                    broken |= (error.Class >= TdsEnums.FATAL_ERROR_CLASS);
                }
            }
        }

        /// <summary>
        /// Stores away current errors and warnings so that an attention can be processed
        /// </summary>
        internal void StoreErrorAndWarningForAttention()
        {
            lock (_errorAndWarningsLock)
            {
                Debug.Assert(_preAttentionErrors == null && _preAttentionWarnings == null, "Can't store errors for attention because there are already errors stored");

                _hasErrorOrWarning = false;

                _preAttentionErrors = _errors;
                _preAttentionWarnings = _warnings;

                _errors = null;
                _warnings = null;
            }
        }

        /// <summary>
        /// Restores errors and warnings that were stored in order to process an attention
        /// </summary>
        internal void RestoreErrorAndWarningAfterAttention()
        {
            lock (_errorAndWarningsLock)
            {
                Debug.Assert(_errors == null && _warnings == null, "Can't restore errors after attention because there are already other errors");

                _hasErrorOrWarning = (((_preAttentionErrors != null) && (_preAttentionErrors.Count > 0)) || ((_preAttentionWarnings != null) && (_preAttentionWarnings.Count > 0)));

                _errors = _preAttentionErrors;
                _warnings = _preAttentionWarnings;

                _preAttentionErrors = null;
                _preAttentionWarnings = null;
            }
        }

        /// <summary>
        /// Checks if an error is stored in _error and, if so, throws an error
        /// </summary>
        internal void CheckThrowSNIException()
        {
            if (HasErrorOrWarning)
            {
                ThrowExceptionAndWarning();
            }
        }

        /// <summary>
        /// Debug Only: Ensures that the TdsParserStateObject has no lingering state and can safely be re-used
        /// </summary>
        [Conditional("DEBUG")]
        internal void AssertStateIsClean()
        {
            // If our TdsParser is closed or broken, then we don't really care about our state
            var parser = _parser;
            if ((parser != null) && (parser.State != TdsParserState.Closed) && (parser.State != TdsParserState.Broken))
            {
                // Async reads
                Debug.Assert(_snapshot == null && !_snapshotReplay, "StateObj has leftover snapshot state");
                Debug.Assert(!_asyncReadWithoutSnapshot, "StateObj has AsyncReadWithoutSnapshot still enabled");
                Debug.Assert(_executionContext == null, "StateObj has a stored execution context from an async read");
                // Async writes
                Debug.Assert(_asyncWriteCount == 0, "StateObj still has outstanding async writes");
                Debug.Assert(_delayedWriteAsyncCallbackException == null, "StateObj has an unobserved exceptions from an async write");
                // Attention\Cancellation\Timeouts
                Debug.Assert(!_attentionReceived && !_attentionSent && !_attentionSending, $"StateObj is still dealing with attention: Sent: {_attentionSent}, Received: {_attentionReceived}, Sending: {_attentionSending}");
                Debug.Assert(!_cancelled, "StateObj still has cancellation set");
                Debug.Assert(_timeoutState == TimeoutState.Stopped, "StateObj still has internal timeout set");
                // Errors and Warnings
                Debug.Assert(!_hasErrorOrWarning, "StateObj still has stored errors or warnings");
            }
        }

#if DEBUG
        internal void CompletePendingReadWithSuccess(bool resetForcePendingReadsToWait)
        {
            var realNetworkPacketTaskSource = _realNetworkPacketTaskSource;
            var networkPacketTaskSource = _networkPacketTaskSource;

            Debug.Assert(_forcePendingReadsToWaitForUser, "Not forcing pends to wait for user - can't force complete");
            Debug.Assert(networkPacketTaskSource != null, "No pending read to complete");

            try
            {
                if (realNetworkPacketTaskSource != null)
                {
                    // Wait for the real read to complete
                    realNetworkPacketTaskSource.Task.Wait();
                }
            }
            finally
            {
                if (networkPacketTaskSource != null)
                {
                    if (resetForcePendingReadsToWait)
                    {
                        _forcePendingReadsToWaitForUser = false;
                    }

                    networkPacketTaskSource.TrySetResult(null);
                }
            }
        }

        internal void CompletePendingReadWithFailure(int errorCode, bool resetForcePendingReadsToWait)
        {
            var realNetworkPacketTaskSource = _realNetworkPacketTaskSource;
            var networkPacketTaskSource = _networkPacketTaskSource;

            Debug.Assert(_forcePendingReadsToWaitForUser, "Not forcing pends to wait for user - can't force complete");
            Debug.Assert(networkPacketTaskSource != null, "No pending read to complete");

            try
            {
                if (realNetworkPacketTaskSource != null)
                {
                    // Wait for the real read to complete
                    realNetworkPacketTaskSource.Task.Wait();
                }
            }
            finally
            {
                if (networkPacketTaskSource != null)
                {
                    if (resetForcePendingReadsToWait)
                    {
                        _forcePendingReadsToWaitForUser = false;
                    }

                    AddError(new SqlError(errorCode, 0x00, TdsEnums.FATAL_ERROR_CLASS, _parser.Server, string.Empty, string.Empty, 0));
                    try
                    {
                        ThrowExceptionAndWarning();
                    }
                    catch (Exception ex)
                    {
                        networkPacketTaskSource.TrySetException(ex);
                    }
                }
            }
        }
#endif

        internal void CloneCleanupAltMetaDataSetArray()
        {
            if (_snapshot != null)
            {
                _snapshot.CloneCleanupAltMetaDataSetArray();
            }
        }

        class PacketData
        {
            public byte[] Buffer;
            public int Read;
#if DEBUG
            public StackTrace Stack;
#endif
        }

        class StateSnapshot
        {
            private List<PacketData> _snapshotInBuffs;
            private int _snapshotInBuffCurrent = 0;
            private int _snapshotInBytesUsed = 0;
            private int _snapshotInBytesPacket = 0;
            private bool _snapshotPendingData = false;
            private bool _snapshotErrorTokenReceived = false;
            private bool _snapshotHasOpenResult = false;
            private bool _snapshotReceivedColumnMetadata = false;
            private bool _snapshotAttentionReceived;
            private byte _snapshotMessageStatus;

            private NullBitmap _snapshotNullBitmapInfo;
            private ulong _snapshotLongLen;
            private ulong _snapshotLongLenLeft;
            private _SqlMetaDataSet _snapshotCleanupMetaData;
            private _SqlMetaDataSetCollection _snapshotCleanupAltMetaDataSetArray;

            private readonly TdsParserStateObject _stateObj;

            public StateSnapshot(TdsParserStateObject state)
            {
                _snapshotInBuffs = new List<PacketData>();
                _stateObj = state;
            }

#if DEBUG
            private int _rollingPend = 0;
            private int _rollingPendCount = 0;

            internal bool DoPend()
            {
                if (_failAsyncPends || !_forceAllPends)
                {
                    return false;
                }

                if (_rollingPendCount == _rollingPend)
                {
                    _rollingPend++;
                    _rollingPendCount = 0;
                    return true;
                }

                _rollingPendCount++;
                return false;
            }
#endif

            internal void CloneNullBitmapInfo()
            {
                if (_stateObj._nullBitmapInfo.ReferenceEquals(_snapshotNullBitmapInfo))
                {
                    _stateObj._nullBitmapInfo = _stateObj._nullBitmapInfo.Clone();
                }
            }

            internal void CloneCleanupAltMetaDataSetArray()
            {
                if (_stateObj._cleanupAltMetaDataSetArray != null && object.ReferenceEquals(_snapshotCleanupAltMetaDataSetArray, _stateObj._cleanupAltMetaDataSetArray))
                {
                    _stateObj._cleanupAltMetaDataSetArray = (_SqlMetaDataSetCollection)_stateObj._cleanupAltMetaDataSetArray.Clone();
                }
            }

            internal void PushBuffer(byte[] buffer, int read)
            {
                Debug.Assert(!_snapshotInBuffs.Any(b => object.ReferenceEquals(b, buffer)));

                PacketData packetData = new PacketData();
                packetData.Buffer = buffer;
                packetData.Read = read;
#if DEBUG
                packetData.Stack = _stateObj._lastStack;
#endif

                _snapshotInBuffs.Add(packetData);
            }

#if DEBUG
            internal void AssertCurrent()
            {
                Debug.Assert(_snapshotInBuffCurrent == _snapshotInBuffs.Count, "Should not be reading new packets when not replaying last packet");
            }
            internal void CheckStack(StackTrace trace)
            {
                PacketData prev = _snapshotInBuffs[_snapshotInBuffCurrent - 1];
                if (prev.Stack == null)
                {
                    prev.Stack = trace;
                }
                else
                {
                    Debug.Assert(_stateObj._permitReplayStackTraceToDiffer || prev.Stack.ToString() == trace.ToString(), "The stack trace on subsequent replays should be the same");
                }
            }
#endif 

            internal bool Replay()
            {
                if (_snapshotInBuffCurrent < _snapshotInBuffs.Count)
                {
                    PacketData next = _snapshotInBuffs[_snapshotInBuffCurrent];
                    _stateObj._inBuff = next.Buffer;
                    _stateObj._inBytesUsed = 0;
                    _stateObj._inBytesRead = next.Read;
                    _snapshotInBuffCurrent++;
                    return true;
                }

                return false;
            }

            internal void Snap()
            {
                _snapshotInBuffs.Clear();
                _snapshotInBuffCurrent = 0;
                _snapshotInBytesUsed = _stateObj._inBytesUsed;
                _snapshotInBytesPacket = _stateObj._inBytesPacket;
                _snapshotPendingData = _stateObj._pendingData;
                _snapshotErrorTokenReceived = _stateObj._errorTokenReceived;
                _snapshotMessageStatus = _stateObj._messageStatus;
                // _nullBitmapInfo must be cloned before it is updated
                _snapshotNullBitmapInfo = _stateObj._nullBitmapInfo;
                _snapshotLongLen = _stateObj._longlen;
                _snapshotLongLenLeft = _stateObj._longlenleft;
                _snapshotCleanupMetaData = _stateObj._cleanupMetaData;
                // _cleanupAltMetaDataSetArray must be cloned bofore it is updated
                _snapshotCleanupAltMetaDataSetArray = _stateObj._cleanupAltMetaDataSetArray;
                _snapshotHasOpenResult = _stateObj._hasOpenResult;
                _snapshotReceivedColumnMetadata = _stateObj._receivedColMetaData;
                _snapshotAttentionReceived = _stateObj._attentionReceived;
#if DEBUG
                _rollingPend = 0;
                _rollingPendCount = 0;
                _stateObj._lastStack = null;
                Debug.Assert(_stateObj._bTmpRead == 0, "Has partially read data when snapshot taken");
                Debug.Assert(_stateObj._partialHeaderBytesRead == 0, "Has partially read header when shapshot taken");
#endif

                PushBuffer(_stateObj._inBuff, _stateObj._inBytesRead);
            }

            internal void ResetSnapshotState()
            {
                // go back to the beginning
                _snapshotInBuffCurrent = 0;

                Replay();

                _stateObj._inBytesUsed = _snapshotInBytesUsed;
                _stateObj._inBytesPacket = _snapshotInBytesPacket;
                _stateObj._pendingData = _snapshotPendingData;
                _stateObj._errorTokenReceived = _snapshotErrorTokenReceived;
                _stateObj._messageStatus = _snapshotMessageStatus;
                _stateObj._nullBitmapInfo = _snapshotNullBitmapInfo;
                _stateObj._cleanupMetaData = _snapshotCleanupMetaData;
                _stateObj._cleanupAltMetaDataSetArray = _snapshotCleanupAltMetaDataSetArray;

                // Make sure to go through the appropriate increment/decrement methods if changing HasOpenResult
                if (!_stateObj._hasOpenResult && _snapshotHasOpenResult)
                {
                    _stateObj.IncrementAndObtainOpenResultCount(_stateObj._executedUnderTransaction);
                }
                else if (_stateObj._hasOpenResult && !_snapshotHasOpenResult)
                {
                    _stateObj.DecrementOpenResultCount();
                }
                //else _stateObj._hasOpenResult is already == _snapshotHasOpenResult

                _stateObj._receivedColMetaData = _snapshotReceivedColumnMetadata;
                _stateObj._attentionReceived = _snapshotAttentionReceived;

                // Reset partially read state (these only need to be maintained if doing async without snapshot)
                _stateObj._bTmpRead = 0;
                _stateObj._partialHeaderBytesRead = 0;

                // reset plp state
                _stateObj._longlen = _snapshotLongLen;
                _stateObj._longlenleft = _snapshotLongLenLeft;

                _stateObj._snapshotReplay = true;

                _stateObj.AssertValidState();
            }

            internal void PrepareReplay()
            {
                ResetSnapshotState();
            }
        }

        /*

        // leave this in. comes handy if you have to do Console.WriteLine style debugging ;)
                private void DumpBuffer() {
                    Console.WriteLine("dumping buffer");
                    Console.WriteLine("_inBytesRead = {0}", _inBytesRead);
                    Console.WriteLine("_inBytesUsed = {0}", _inBytesUsed);
                    int cc = 0; // character counter
                    int i;
                    Console.WriteLine("used buffer:");
                    for (i=0; i< _inBytesUsed; i++) {
                        if (cc==16) {
                            Console.WriteLine();
                            cc = 0;
                        }
                        Console.Write("{0,-2:X2} ", _inBuff[i]);
                        cc++;
                    }
                    if (cc>0) {
                        Console.WriteLine();
                    }

                    cc = 0;
                    Console.WriteLine("unused buffer:");
                    for (i=_inBytesUsed; i<_inBytesRead; i++) {
                        if (cc==16) {
                            Console.WriteLine();
                            cc = 0;
                        }
                        Console.Write("{0,-2:X2} ", _inBuff[i]);
                        cc++;
                    }
                    if (cc>0) {
                        Console.WriteLine();
                    }
                }
        */
    }
}
