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
        private struct RuntimeHelpers
        {
            /// <summary>
            /// This is a no-op in netcore version. Only needed for merging with netfx codebase.
            /// </summary>
            [Conditional("NETFRAMEWORK")]
            internal static void PrepareConstrainedRegions()
            {
            }
        }

        //////////////////
        // Constructors //
        //////////////////

        protected TdsParserStateObject(TdsParser parser, TdsParserStateObject physicalConnection, bool async)
        {
            // Construct a MARS session
            Debug.Assert(parser != null, "no parser?");
            _parser = parser;
            _onTimeoutAsync = OnTimeoutAsync;
            SniContext = SniContext.Snix_GetMarsSession;

            Debug.Assert(_parser._physicalStateObj != null, "no physical session?");
            Debug.Assert(_parser._physicalStateObj._inBuff != null, "no in buffer?");
            Debug.Assert(_parser._physicalStateObj._outBuff != null, "no out buffer?");
            Debug.Assert(_parser._physicalStateObj._outBuff.Length ==
                         _parser._physicalStateObj._inBuff.Length, "Unexpected unequal buffers.");

            // Determine packet size based on physical connection buffer lengths.
            SetPacketSize(_parser._physicalStateObj._outBuff.Length);

            CreateSessionHandle(physicalConnection, async);

            if (IsFailedHandle())
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

        /////////////////////
        // General methods //
        /////////////////////

        internal abstract uint EnableSsl(ref uint info, bool tlsFirst, string serverCertificateFilename);

        internal abstract uint WaitForSSLHandShakeToComplete(out int protocolVersion);

        internal abstract uint CheckConnection();

        internal int DecrementPendingCallbacks(bool release)
        {
            int remaining = Interlocked.Decrement(ref _pendingCallbacks);
            SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObject.DecrementPendingCallbacks | ADV | State Object Id {0}, after decrementing _pendingCallbacks: {1}", _objectID, _pendingCallbacks);
            
            FreeGcHandle(remaining, release);

            // NOTE: TdsParserSessionPool may call DecrementPendingCallbacks on a TdsParserStateObject which is already disposed
            // This is not dangerous (since the stateObj is no longer in use), but we need to add a workaround in the assert for it
            Debug.Assert((remaining == -1 && SessionHandle.IsNull) || (0 <= remaining && remaining < 3), $"_pendingCallbacks values is invalid after decrementing: {remaining}");
            return remaining;
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

            uint error = TdsEnums.SNI_SUCCESS;
            SniContext = SniContext.Snix_Connect;
            try
            {
                Interlocked.Increment(ref _readingCount);
                error = CheckConnection();
            }
            finally
            {
                Interlocked.Decrement(ref _readingCount);
            }
            return (error == TdsEnums.SNI_SUCCESS) || (error == TdsEnums.SNI_WAIT_TIMEOUT);
        }

        // This method should only be called by ReadSni!  If not - it may have problems with timeouts!
        private void ReadSniError(TdsParserStateObject stateObj, uint error)
        {
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
                    AddError(new SqlError(TdsEnums.TIMEOUT_EXPIRED, 0x00, TdsEnums.MIN_ERROR_CLASS, _parser.Server, _parser.Connection.TimeoutErrorInternal.GetErrorMessage(), "", 0, TdsEnums.SNI_WAIT_TIMEOUT));

                    if (!stateObj._attentionSent)
                    {
                        if (stateObj.Parser.State == TdsParserState.OpenLoggedIn)
                        {
                            stateObj.SendAttention(mustTakeWriteLock: true);

                            PacketHandle syncReadPacket = default;
                            bool readFromNetwork = true;
                            RuntimeHelpers.PrepareConstrainedRegions();
                            bool shouldDecrement = false;
                            try
                            {
                                Interlocked.Increment(ref _readingCount);
                                shouldDecrement = true;
                                readFromNetwork = !PartialPacketContainsCompletePacket();
                                if (readFromNetwork)
                                {
                                    syncReadPacket = ReadSyncOverAsync(stateObj.GetTimeoutRemaining(), out error);
                                }
                                else
                                {
                                    error = TdsEnums.SNI_SUCCESS;
                                }

                                Interlocked.Decrement(ref _readingCount);
                                shouldDecrement = false;

                                if (TdsEnums.SNI_SUCCESS == error)
                                {
                                    // We will end up letting the run method deal with the expected done:done_attn token stream.
                                    stateObj.ProcessSniPacket(syncReadPacket, TdsEnums.SNI_SUCCESS);
                                    return;
                                }
                                else
                                {
                                    Debug.Assert(!readFromNetwork || !IsValidPacket(syncReadPacket), "unexpected syncReadPacket without corresponding SNIPacketRelease");
                                    fail = true; // Subsequent read failed, time to give up.
                                }
                            }
                            finally
                            {
                                if (shouldDecrement)
                                {
                                    Interlocked.Decrement(ref _readingCount);
                                }

                                if (readFromNetwork && !IsPacketEmpty(syncReadPacket))
                                {
                                    // Be sure to release packet, otherwise it will be leaked by native.
                                    ReleasePacket(syncReadPacket);
                                }
                            }
                        }
                        else
                        {
                            if (_parser._loginWithFailover)
                            {
                                // For DbMirroring Failover during login, never break the connection, just close the TdsParser
                                _parser.Disconnect();
                            }
                            else if ((_parser.State == TdsParserState.OpenNotLoggedIn) && (_parser.Connection.ConnectionOptions.MultiSubnetFailover))
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

        private uint GetSniPacket(PacketHandle packet, ref uint dataSize)
        {
            return SniPacketGetData(packet, _inBuff, ref dataSize);
        }

        private void SetBufferSecureStrings()
        {
            if (_securePasswords != null)
            {
                for (int i = 0; i < _securePasswords.Length; i++)
                {
                    if (_securePasswords[i] != null)
                    {
                        IntPtr str = IntPtr.Zero;
                        try
                        {
                            str = Marshal.SecureStringToBSTR(_securePasswords[i]);
                            byte[] data = new byte[_securePasswords[i].Length * 2];
                            Marshal.Copy(str, data, 0, _securePasswords[i].Length * 2);
                            if (!BitConverter.IsLittleEndian)
                            {
                                Span<byte> span = data.AsSpan();
                                for (int ii = 0; ii < _securePasswords[i].Length * 2; ii += 2)
                                {
                                    short value = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(ii));
                                    BinaryPrimitives.WriteInt16BigEndian(span.Slice(ii), value);
                                }
                            }
                            TdsParserStaticMethods.ObfuscatePassword(data);
                            data.CopyTo(_outBuff, _securePasswordOffsetsInBuffer[i]);
                        }
                        finally
                        {
                            Marshal.ZeroFreeBSTR(str);
                        }
                    }
                }
            }
        }

        public void ReadAsyncCallback(PacketHandle packet, uint error) =>
            ReadAsyncCallback(IntPtr.Zero, packet, error);

        public void ReadAsyncCallback(IntPtr key, PacketHandle packet, uint error)
        {
            // Key never used.
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
            if ((s_forcePendingReadsToWaitForUser) && (_realNetworkPacketTaskSource != null))
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
                Debug.Assert((packet.Type == 0 && PartialPacketContainsCompletePacket()) || (CheckPacket(packet, source) && source != null), "AsyncResult null on callback");

                if (_parser.MARSOn)
                {
                    // Only take reset lock on MARS and Async.
                    CheckSetResetConnectionState(error, CallbackType.Read);
                }

                ChangeNetworkPacketTimeout(Timeout.Infinite, Timeout.Infinite);

                // The timer thread may be unreliable under high contention scenarios. It cannot be
                // assumed that the timeout has happened on the timer thread callback. Check the timeout
                // synchronously and then call OnTimeoutSync to force an atomic change of state.
                if (TimeoutHasExpired)
                {
                    OnTimeoutSync(asyncClose: true);
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
                // pendingCallbacks may be 2 after decrementing, this indicates that a fatal timeout is occurring, and therefore we shouldn't complete the task
                int pendingCallbacks = DecrementPendingCallbacks(false); // may dispose of GC handle.
                if ((processFinallyBlock) && (source != null) && (pendingCallbacks < 2))
                {
                    if (error == 0)
                    {
                        if (_executionContext != null)
                        {
                            ExecutionContext.Run(_executionContext, s_readAsyncCallbackComplete, source);
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
                            ExecutionContext.Run(_executionContext, state => ReadAsyncCallbackCaptureException((TaskCompletionSource<object>)state), source);
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

        public void WriteAsyncCallback(PacketHandle packet, uint sniError) =>
            WriteAsyncCallback(IntPtr.Zero, packet, sniError);

        public void WriteAsyncCallback(IntPtr key, PacketHandle packet, uint sniError)
        { // Key never used.
            RemovePacketFromPendingList(packet);
            try
            {
                if (sniError != TdsEnums.SNI_SUCCESS)
                {
                    SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObject.WriteAsyncCallback | Info | State Object Id {0}, Write async returned error code {1}", _objectID, (int)sniError);
                    try
                    {
                        AddError(_parser.ProcessSNIError(this));
                        ThrowExceptionAndWarning(asyncClose: true);
                    }
                    catch (Exception e)
                    {
                        TaskCompletionSource<object> writeCompletionSource = _writeCompletionSource;
                        if (writeCompletionSource != null)
                        {
                            writeCompletionSource.TrySetException(e);
                        }
                        else
                        {
                            _delayedWriteAsyncCallbackException = e;

                            // Ensure that _delayedWriteAsyncCallbackException is set before checking _writeCompletionSource
                            Interlocked.MemoryBarrier();

                            // Double check that _writeCompletionSource hasn't been created in the meantime
                            writeCompletionSource = _writeCompletionSource;
                            if (writeCompletionSource != null)
                            {
                                Exception delayedException = Interlocked.Exchange(ref _delayedWriteAsyncCallbackException, null);
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
                        TaskCompletionSource<object> writeCompletionSource = _writeCompletionSource;
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
            TaskCompletionSource<object> completionSource = _writeCompletionSource;
            if (_asyncWriteCount == 0 && completionSource != null)
            {
                completionSource.TrySetResult(null);
            }
        }

        /////////////////////////////////////////
        // Network/Packet Writing & Processing //
        /////////////////////////////////////////

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
                state == TdsParserState.OpenLoggedIn
                    && !_bulkCopyOpperationInProgress // ignore the condition checking for bulk copy
                    && _outBytesUsed == (_outputHeaderLen +
                    BinaryPrimitives.ReadInt32LittleEndian(_outBuff.AsSpan(_outputHeaderLen)))
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
                // If we have been canceled, then ensure that we write the ATTN packet as well
#if NET
                task = AsyncHelper.CreateContinuationTask(task, CancelWritePacket);
#else
                task = AsyncHelper.CreateContinuationTask(task, CancelWritePacket, _parser.Connection);
#endif
            }

            return task;
        }

#pragma warning disable 420 // a reference to a volatile field will not be treated as volatile

        private Task SNIWritePacket(PacketHandle packet, out uint sniError, bool canAccumulate, bool callerHasConnectionLock, bool asyncClose = false)
        {
            // Check for a stored exception
            Exception delayedException = Interlocked.Exchange(ref _delayedWriteAsyncCallbackException, null);
            if (delayedException != null)
            {
                throw delayedException;
            }

            Task task = null;
            _writeCompletionSource = null;
            PacketHandle packetPointer = EmptyReadPacket;
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
            try
            {
            }
            finally
            {
                sniError = WritePacket(packet, sync);
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
                    Interlocked.MemoryBarrier();

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
                            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObject.SNIWritePacket | Info | State Object Id {0}, Write async returned error code {1}", _objectID, (int)error);
                            AddError(_parser.ProcessSNIError(this));
                            ThrowExceptionAndWarning(false, asyncClose);
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
                        Debug.Assert(IsValidPacket(packetPointer), "Packet added to list has an invalid pointer, can not remove from pending list");
                        RemovePacketFromPendingList(packetPointer);
                    }
                }
                else
                {
                    SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObject.SNIWritePacket | Info | State Object Id {0}, Write async returned error code {1}", _objectID, (int)sniError);
                    AddError(_parser.ProcessSNIError(this));
                    ThrowExceptionAndWarning(callerHasConnectionLock, asyncClose);
                }
                AssertValidState();
            }
            return task;
        }

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

                RuntimeHelpers.PrepareConstrainedRegions();
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

        internal abstract PacketHandle CreateAndSetAttentionPacket();

        internal abstract void SetPacketData(PacketHandle packet, byte[] buffer, int bytesUsed);

        private Task WriteSni(bool canAccumulate)
        {
            // Prepare packet, and write to packet.
            PacketHandle packet = GetResetWritePacket(_outBytesUsed);

            SetBufferSecureStrings();
            SetPacketData(packet, _outBuff, _outBytesUsed);

            Debug.Assert(Parser.Connection._parserLock.ThreadMayHaveLock(), "Thread is writing without taking the connection lock");
            Task task = SNIWritePacket(packet, out _, canAccumulate, callerHasConnectionLock: true);

            // Check to see if the timeout has occurred.  This time out code is special case code to allow BCP writes to timeout. Eventually we should make all writes timeout.
            if (_bulkCopyOpperationInProgress && 0 == GetTimeoutRemaining())
            {
                _parser.Connection.ThreadHasParserLockForClose = true;
                try
                {
                    Debug.Assert(_parser.Connection != null, "SqlConnectionInternalTds handler can not be null at this point.");
                    AddError(new SqlError(TdsEnums.TIMEOUT_EXPIRED, 0x00, TdsEnums.MIN_ERROR_CLASS, _parser.Server, _parser.Connection.TimeoutErrorInternal.GetErrorMessage(), "", 0, TdsEnums.SNI_WAIT_TIMEOUT));
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
            if (_parser.State == TdsParserState.OpenNotLoggedIn &&
                _parser.EncryptionOptions == EncryptionOptions.LOGIN)
            {
                // If no error occurred, and we are Open but not logged in, and
                // our encryptionOption state is login, remove the SSL Provider.
                // We only need encrypt the very first packet of the login message to the server.

                // We wanted to encrypt entire login channel, but there is
                // currently no mechanism to communicate this.  Removing encryption post 1st packet
                // is a hard-coded agreement between client and server.  We need some mechanism or
                // common change to be able to make this change in a non-breaking fashion.
                _parser.RemoveEncryption();                        // Remove the SSL Provider.
                _parser.EncryptionOptions = EncryptionOptions.OFF; // Turn encryption off.

                // Since this packet was associated with encryption, dispose and re-create.
                ClearAllWritePackets();
            }

            SniWriteStatisticsAndTracing();

            ResetBuffer();

            AssertValidState();
            return task;
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
