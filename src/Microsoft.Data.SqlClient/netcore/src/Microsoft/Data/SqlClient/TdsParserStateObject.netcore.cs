// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Buffers.Binary;
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

        private static readonly ContextCallback s_readAsyncCallbackComplete = ReadAsyncCallbackComplete;

        // Timeout variables
        private readonly WeakReference _cancellationOwner = new WeakReference(null);

        // Async

        //////////////////
        // Constructors //
        //////////////////

        internal TdsParserStateObject(TdsParser parser, TdsParserStateObject physicalConnection, bool async)
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

        ////////////////
        // Properties //
        ////////////////

        internal abstract uint Status
        {
            get;
        }

        internal abstract Guid? SessionId { get; }

        internal abstract SessionHandle SessionHandle
        {
            get;
        }

        /////////////////////
        // General methods //
        /////////////////////

        // This method is only called by the command or datareader as a result of a user initiated
        // cancel request.
        internal void Cancel(object caller)
        {
            Debug.Assert(caller != null, "Null caller for Cancel!");
            Debug.Assert(caller is SqlCommand || caller is SqlDataReader, "Calling API with invalid caller type: " + caller.GetType());

            bool hasLock = false;
            try
            {
                // Keep looping until we either grabbed the lock (and therefore sent attention) or the connection closes\breaks
                while ((!hasLock) && (_parser.State != TdsParserState.Closed) && (_parser.State != TdsParserState.Broken))
                {
                    Monitor.TryEnter(this, WaitForCancellationLockPollTimeout, ref hasLock);
                    if (hasLock)
                    { // Lock for the time being - since we need to synchronize the attention send.
                      // This lock is also protecting against concurrent close and async continuations

                        // Ensure that, once we have the lock, that we are still the owner
                        if ((!_cancelled) && (_cancellationOwner.Target == caller))
                        {
                            _cancelled = true;

                            if (HasPendingData && !_attentionSent)
                            {
                                bool hasParserLock = false;
                                // Keep looping until we have the parser lock (and so are allowed to write), or the connection closes\breaks
                                while ((!hasParserLock) && (_parser.State != TdsParserState.Closed) && (_parser.State != TdsParserState.Broken))
                                {
                                    try
                                    {
                                        _parser.Connection._parserLock.Wait(canReleaseFromAnyThread: false, timeout: WaitForCancellationLockPollTimeout, lockTaken: ref hasParserLock);
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
            // command execution, as well as the session reclamation code for cases where the
            // DataReader is opened and then GC'ed.
            lock (this)
            {
                // Reset cancel state.
                _cancelled = false;
                _cancellationOwner.Target = null;

                if (_attentionSent)
                {
                    // Make sure we're cleaning up the AttentionAck if Cancel happened before taking the lock.
                    // We serialize Cancel/CloseSession to prevent a race condition between these two states.
                    // The problem is that both sending and receiving attentions are time taking
                    // operations.
                    Parser.ProcessPendingAck(this);
                }
                SetTimeoutStateStopped();
            }
        }

        internal abstract void CreatePhysicalSNIHandle(
            string serverName,
            TimeoutTimer timeout,
            out byte[] instanceName,
            ref string[] spns,
            bool flushCache,
            bool async,
            bool fParallel,
            SqlConnectionIPAddressPreference iPAddressPreference,
            string cachedFQDN,
            ref SQLDNSInfo pendingDNSInfo,
            string serverSPN,
            bool isIntegratedSecurity = false,
            bool tlsFirst = false,
            string hostNameInCertificate = "",
            string serverCertificateFilename = "");

        internal abstract void AssignPendingDNSInfo(string userProtocol, string DNSCacheKey, ref SQLDNSInfo pendingDNSInfo);

        internal abstract bool IsFailedHandle();

        protected abstract void CreateSessionHandle(TdsParserStateObject physicalConnection, bool async);

        protected abstract void FreeGcHandle(int remaining, bool release);

        internal abstract uint EnableSsl(ref uint info, bool tlsFirst, string serverCertificateFilename);

        internal abstract uint WaitForSSLHandShakeToComplete(out int protocolVersion);

        internal abstract void Dispose();

        internal abstract void DisposePacketCache();

        internal abstract bool IsPacketEmpty(PacketHandle readPacket);

        internal abstract PacketHandle ReadSyncOverAsync(int timeoutRemaining, out uint error);

        internal abstract PacketHandle ReadAsync(SessionHandle handle, out uint error);

        internal abstract uint CheckConnection();

        internal abstract void ReleasePacket(PacketHandle syncReadPacket);

        protected abstract uint SniPacketGetData(PacketHandle packet, byte[] _inBuff, ref uint dataSize);

        internal abstract PacketHandle GetResetWritePacket(int dataSize);

        internal abstract void ClearAllWritePackets();

        internal abstract PacketHandle AddPacketToPendingList(PacketHandle packet);

        protected abstract void RemovePacketFromPendingList(PacketHandle pointer);

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

        internal int IncrementPendingCallbacks()
        {
            int remaining = Interlocked.Increment(ref _pendingCallbacks);
            SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObject.IncrementPendingCallbacks | ADV | State Object Id {0}, after incrementing _pendingCallbacks: {1}", _objectID, _pendingCallbacks);
            Debug.Assert(0 < remaining && remaining <= 3, $"_pendingCallbacks values is invalid after incrementing: {remaining}");
            return remaining;
        }

        internal void StartSession(object cancellationOwner)
        {
            _cancellationOwner.Target = cancellationOwner;
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

        private static void ReadAsyncCallbackComplete(object state)
        {
            TaskCompletionSource<object> source = (TaskCompletionSource<object>)state;
            source.TrySetResult(null);
        }

        protected abstract bool CheckPacket(PacketHandle packet, TaskCompletionSource<object> source);

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

        //
        // Takes a secure string and offsets and saves them for a write latter when the information is written out to SNI Packet
        //  This method is provided to better handle the life cycle of the clear text of the secure string
        //  This method also ensures that the clear text is not held in the unpined managed buffer so that it avoids getting moved around by CLR garbage collector
        //  TdsParserStaticMethods.EncryptPassword operation is also done in the unmanaged buffer for the clear text later
        //
        internal void WriteSecureString(SecureString secureString)
        {
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

        internal void ResetSecurePasswordsInformation()
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
            Exception delayedException = Interlocked.Exchange(ref _delayedWriteAsyncCallbackException, null);
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
            Interlocked.MemoryBarrier();

            // Now that we have set _writeCompletionSource, check if parser is closed or broken
            if ((_parser.State == TdsParserState.Closed) || (_parser.State == TdsParserState.Broken))
            {
                throw ADP.ClosedConnectionError();
            }

            // Check for stored exceptions
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

            return task;
        }

        // Takes in a single byte and writes it to the buffer.  If the buffer is full, it is flushed
        // and then the buffer is re-initialized in flush() and then the byte is put in the buffer.
        internal void WriteByte(byte b)
        {
            Debug.Assert(_outBytesUsed <= _outBuff.Length, "ERROR - TDSParser: _outBytesUsed > _outBuff.Length");

            // check to make sure we haven't used the full amount of space available in the buffer, if so, flush it
            if (_outBytesUsed == _outBuff.Length)
            {
                WritePacket(TdsEnums.SOFTFLUSH, canAccumulate: true);
            }
            // set byte in buffer and increment the counter for number of bytes used in the out buffer
            _outBuff[_outBytesUsed++] = b;
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
                task = AsyncHelper.CreateContinuationTask(task, CancelWritePacket);
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

        internal abstract bool IsValidPacket(PacketHandle packetPointer);

        internal abstract uint WritePacket(PacketHandle packet, bool sync);

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

        private void SniReadStatisticsAndTracing()
        {
            SqlStatistics statistics = Parser.Statistics;
            if (statistics != null)
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
            if (statistics != null)
            {
                statistics.SafeIncrement(ref statistics._buffersSent);
                statistics.SafeAdd(ref statistics._bytesSent, _outBytesUsed);
                statistics.RequestNetworkServerTimer();
            }
        }

        [Conditional("DEBUG")]
        private void AssertValidState()
        {
            if (_inBytesUsed < 0 || _inBytesRead < 0)
            {
                Debug.Fail($"Invalid TDS Parser State: either _inBytesUsed or _inBytesRead is negative: {_inBytesUsed}, {_inBytesRead}");
            }
            else if (_inBytesUsed > _inBytesRead)
            {
                Debug.Fail($"Invalid TDS Parser State: _inBytesUsed > _inBytesRead: {_inBytesUsed} > {_inBytesRead}");
            }

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

        protected abstract PacketHandle EmptyReadPacket { get; }

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
            TdsParser parser = _parser;
            if ((parser != null) && (parser.State != TdsParserState.Closed) && (parser.State != TdsParserState.Broken))
            {
                // Async reads
                Debug.Assert(_snapshot == null && _snapshotStatus == SnapshotStatus.NotActive, "StateObj has leftover snapshot state");
                Debug.Assert(!_asyncReadWithoutSnapshot, "StateObj has AsyncReadWithoutSnapshot still enabled");
                Debug.Assert(_executionContext == null, "StateObj has a stored execution context from an async read");
                // Async writes
                Debug.Assert(_asyncWriteCount == 0, "StateObj still has outstanding async writes");
                Debug.Assert(_delayedWriteAsyncCallbackException == null, "StateObj has an unobserved exceptions from an async write");
                // Attention\Cancellation\Timeouts
                Debug.Assert(!HasReceivedAttention && !_attentionSent && !_attentionSending, $"StateObj is still dealing with attention: Sent: {_attentionSent}, Received: {HasReceivedAttention}, Sending: {_attentionSending}");
                Debug.Assert(!_cancelled, "StateObj still has cancellation set");
                Debug.Assert(_timeoutState == TimeoutState.Stopped, "StateObj still has internal timeout set");
                // Errors and Warnings
                Debug.Assert(!_hasErrorOrWarning, "StateObj still has stored errors or warnings");
            }
        }

#if DEBUG
        internal void CompletePendingReadWithSuccess(bool resetForcePendingReadsToWait)
        {
            TaskCompletionSource<object> realNetworkPacketTaskSource = _realNetworkPacketTaskSource;
            TaskCompletionSource<object> networkPacketTaskSource = _networkPacketTaskSource;

            Debug.Assert(s_forcePendingReadsToWaitForUser, "Not forcing pends to wait for user - can't force complete");
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
                        s_forcePendingReadsToWaitForUser = false;
                    }

                    networkPacketTaskSource.TrySetResult(null);
                }
            }
        }

        internal void CompletePendingReadWithFailure(int errorCode, bool resetForcePendingReadsToWait)
        {
            TaskCompletionSource<object> realNetworkPacketTaskSource = _realNetworkPacketTaskSource;
            TaskCompletionSource<object> networkPacketTaskSource = _networkPacketTaskSource;

            Debug.Assert(s_forcePendingReadsToWaitForUser, "Not forcing pends to wait for user - can't force complete");
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
                        s_forcePendingReadsToWaitForUser = false;
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
    }
}
