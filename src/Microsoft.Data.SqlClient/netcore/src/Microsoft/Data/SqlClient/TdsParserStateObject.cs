// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal abstract partial class TdsParserStateObject
    {
        private static readonly ContextCallback s_readAsyncCallbackComplete = ReadAsyncCallbackComplete;

        // Timeout variables
        private readonly WeakReference _cancellationOwner = new WeakReference(null);

		// Async
        private StateSnapshot _cachedSnapshot;
        

        //////////////////
        // Constructors //
        //////////////////

        internal TdsParserStateObject(TdsParser parser, TdsParserStateObject physicalConnection, bool async)
        {
            // Construct a MARS session
            Debug.Assert(null != parser, "no parser?");
            _parser = parser;
            _onTimeoutAsync = OnTimeoutAsync;
            SniContext = SniContext.Snix_GetMarsSession;

            Debug.Assert(null != _parser._physicalStateObj, "no physical session?");
            Debug.Assert(null != _parser._physicalStateObj._inBuff, "no in buffer?");
            Debug.Assert(null != _parser._physicalStateObj._outBuff, "no out buffer?");
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
        internal abstract uint DisableSsl();


        internal abstract uint EnableMars(ref uint info);

        internal abstract uint Status
        {
            get;
        }

        internal abstract SessionHandle SessionHandle
        {
            get;
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

                SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObject.NullBitmap.Initialize | INFO | ADV | State Object Id {0}, NBCROW bitmap received, column count = {1}", stateObj._objectID, columnsCount);
                SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParserStateObject.NullBitmap.Initialize | INFO | ADV | State Object Id {0}, Null Bitmap length {1}, NBCROW bitmap data: {2}", stateObj._objectID, (ushort)_nullBitmap.Length, _nullBitmap);
                return true;
            }
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
            bool ignoreSniOpenTimeout,
            long timerExpire,
            out byte[] instanceName,
            ref byte[][] spnBuffer,
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

        internal abstract uint SniGetConnectionId(ref Guid clientConnectionId);

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

        internal abstract uint SetConnectionBufferSize(ref uint unsignedPacketSize);

        internal abstract void ReleasePacket(PacketHandle syncReadPacket);

        protected abstract uint SNIPacketGetData(PacketHandle packet, byte[] _inBuff, ref uint dataSize);

        internal abstract PacketHandle GetResetWritePacket(int dataSize);

        internal abstract void ClearAllWritePackets();

        internal abstract PacketHandle AddPacketToPendingList(PacketHandle packet);

        protected abstract void RemovePacketFromPendingList(PacketHandle pointer);

        internal abstract uint GenerateSspiClientContext(byte[] receivedBuff, uint receivedLength, ref byte[] sendBuff, ref uint sendLength, byte[][] _sniSpnBuffer);

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

        

        

        /////////////////////////////////////////
        // Network/Packet Reading & Processing //
        /////////////////////////////////////////

        internal void SetSnapshot()
        {
            StateSnapshot snapshot = _snapshot;
            if (snapshot is null)
            {
                snapshot = Interlocked.Exchange(ref _cachedSnapshot, null) ?? new StateSnapshot();
            }
            else
            {
                snapshot.Clear();
            }
            _snapshot = snapshot;
            _snapshot.Snap(this);
            _snapshotReplay = false;
        }

        internal void ResetSnapshot()
        {
            if (_snapshot != null)
            {
                StateSnapshot snapshot = _snapshot;
                _snapshot = null;
                snapshot.Clear();
                Interlocked.CompareExchange(ref _cachedSnapshot, snapshot, null);
            }
            _snapshotReplay = false;
        }

#if DEBUG
        private string _lastStack;
#endif

        

        internal void ReadSniSyncOverAsync()
        {
            if (_parser.State == TdsParserState.Broken || _parser.State == TdsParserState.Closed)
            {
                throw ADP.ClosedConnectionError();
            }

            PacketHandle readPacket = default;

            uint error;

            bool shouldDecrement = false;
            try
            {
                Interlocked.Increment(ref _readingCount);
                shouldDecrement = true;

                readPacket = ReadSyncOverAsync(GetTimeoutRemaining(), out error);

                Interlocked.Decrement(ref _readingCount);
                shouldDecrement = false;

                if (_parser.MARSOn)
                { // Only take reset lock on MARS and Async.
                    CheckSetResetConnectionState(error, CallbackType.Read);
                }

                if (TdsEnums.SNI_SUCCESS == error)
                { // Success - process results!

                    Debug.Assert(!IsPacketEmpty(readPacket), "ReadNetworkPacket cannot be null in synchronous operation!");

                    ProcessSniPacket(readPacket, 0);
#if DEBUG
                    if (s_forcePendingReadsToWaitForUser)
                    {
                        _networkPacketTaskSource = new TaskCompletionSource<object>();
                        Interlocked.MemoryBarrier();
                        _networkPacketTaskSource.Task.Wait();
                        _networkPacketTaskSource = null;
                    }
#endif
                }
                else
                { // Failure!

                    Debug.Assert(!IsValidPacket(readPacket), "unexpected readPacket without corresponding SNIPacketRelease");

                    ReadSniError(this, error);
                }
            }
            finally
            {
                if (shouldDecrement)
                {
                    Interlocked.Decrement(ref _readingCount);
                }

                if (!IsPacketEmpty(readPacket))
                {
                    ReleasePacket(readPacket);
                }

                AssertValidState();
            }
        }
       
        internal void ReadSni(TaskCompletionSource<object> completion)
        {
            Debug.Assert(_networkPacketTaskSource == null || ((_asyncReadWithoutSnapshot) && (_networkPacketTaskSource.Task.IsCompleted)), "Pending async call or failed to replay snapshot when calling ReadSni");
            _networkPacketTaskSource = completion;

            // Ensure that setting the completion source is completed before checking the state
            Interlocked.MemoryBarrier();

            // We must check after assigning _networkPacketTaskSource to avoid races with
            // SqlCommand.OnConnectionClosed
            if (_parser.State == TdsParserState.Broken || _parser.State == TdsParserState.Closed)
            {
                throw ADP.ClosedConnectionError();
            }

#if DEBUG
            if (s_forcePendingReadsToWaitForUser)
            {
                _realNetworkPacketTaskSource = new TaskCompletionSource<object>();
            }
#endif


            PacketHandle readPacket = default;

            uint error = 0;

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

                _networkPacketTimeout = ADP.UnsafeCreateTimer(
                    _onTimeoutAsync,
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

                Interlocked.Increment(ref _readingCount);

                SessionHandle handle = SessionHandle;
                if (!handle.IsNull)
                {
                    IncrementPendingCallbacks();

                    readPacket = ReadAsync(handle, out error);

                    if (!(TdsEnums.SNI_SUCCESS == error || TdsEnums.SNI_SUCCESS_IO_PENDING == error))
                    {
                        DecrementPendingCallbacks(false); // Failure - we won't receive callback!
                    }
                }

                Interlocked.Decrement(ref _readingCount);

                if (handle.IsNull)
                {
                    throw ADP.ClosedConnectionError();
                }

                if (TdsEnums.SNI_SUCCESS == error)
                { // Success - process results!
                    Debug.Assert(IsValidPacket(readPacket), "ReadNetworkPacket should not have been null on this async operation!");
                    // Evaluate this condition for MANAGED_SNI. This may not be needed because the network call is happening Async and only the callback can receive a success.
                    ReadAsyncCallback(IntPtr.Zero, readPacket, 0);

                    // Only release packet for Managed SNI as for Native SNI packet is released in finally block.
                    if (TdsParserStateObjectFactory.UseManagedSNI && !IsPacketEmpty(readPacket))
                    {
                        ReleasePacket(readPacket);
                    }
                }
                else if (TdsEnums.SNI_SUCCESS_IO_PENDING != error)
                { // FAILURE!
                    Debug.Assert(IsPacketEmpty(readPacket), "unexpected readPacket without corresponding SNIPacketRelease");

                    ReadSniError(this, error);
#if DEBUG
                    if ((s_forcePendingReadsToWaitForUser) && (_realNetworkPacketTaskSource != null))
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
                if (!TdsParserStateObjectFactory.UseManagedSNI)
                {
                    if (!IsPacketEmpty(readPacket))
                    {
                        // Be sure to release packet, otherwise it will be leaked by native.
                        ReleasePacket(readPacket);
                    }
                }
                AssertValidState();
            }
        }

        /// <summary>
        /// Checks to see if the underlying connection is still alive (used by connection pool resiliency)
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
                    uint error;
                    SniContext = SniContext.Snix_Connect;

                    error = CheckConnection();
                    if ((error != TdsEnums.SNI_SUCCESS) && (error != TdsEnums.SNI_WAIT_TIMEOUT))
                    {
                        // Connection is dead
                        SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObject.IsConnectionAlive | Info | State Object Id {0}, received error {1} on idle connection", _objectID, (int)error);
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

                            bool shouldDecrement = false;
                            try
                            {
                                Interlocked.Increment(ref _readingCount);
                                shouldDecrement = true;

                                syncReadPacket = ReadSyncOverAsync(stateObj.GetTimeoutRemaining(), out error);

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
                                    Debug.Assert(!IsValidPacket(syncReadPacket), "unexpected syncReadPacket without corresponding SNIPacketRelease");
                                    fail = true; // Subsequent read failed, time to give up.
                                }
                            }
                            finally
                            {
                                if (shouldDecrement)
                                {
                                    Interlocked.Decrement(ref _readingCount);
                                }

                                if (!IsPacketEmpty(syncReadPacket))
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

        public void ProcessSniPacket(PacketHandle packet, uint error)
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
                uint dataSize = 0;

                uint getDataError = SNIPacketGetData(packet, _inBuff, ref dataSize);

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


                    AssertValidState();
                }
                else
                {
                    throw SQL.ParsingError();
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
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObject.ReadAsyncCallback | Info | State Object Id {0}, received error {1} on idle connection", _objectID, (int)error);

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

            bool processFinallyBlock = true;
            try
            {
                Debug.Assert(CheckPacket(packet, source) && source != null, "AsyncResult null on callback");

                if (_parser.MARSOn)
                { 
                    // Only take reset lock on MARS and Async.
                    CheckSetResetConnectionState(error, CallbackType.Read);
                }

                ChangeNetworkPacketTimeout(Timeout.Infinite, Timeout.Infinite);

                // The timer thread may be unreliable under high contention scenarios. It cannot be
                // assumed that the timeout has happened on the timer thread callback. Check the timeout
                // synchrnously and then call OnTimeoutSync to force an atomic change of state. 
                if (TimeoutHasExpired)
                {
                    OnTimeoutSync(true);
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

        // This is in its own method to avoid always allocating the lambda in WriteBytes
        private void WriteBytesSetupContinuation(byte[] array, int len, TaskCompletionSource<object> completion, int offset, Task packetTask)
        {
            AsyncHelper.ContinueTask(packetTask, completion,
               onSuccess: () => WriteBytes(ReadOnlySpan<byte>.Empty, len: len, offsetBuffer: offset, canAccumulate: false, completion: completion, array)
           );
        }


#pragma warning disable 0420 // a reference to a volatile field will not be treated as volatile

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
                Debug.Assert(!sync, "Completion should be handled in SniManagedWwrapper");
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

                            uint sniError;
                            _parser._asyncWrite = false; // stop async write
                            SNIWritePacket(attnPacket, out sniError, canAccumulate: false, callerHasConnectionLock: false, asyncClose);
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

                SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParserStateObject.SendAttention | INFO | ADV | State Object Id {0}, Packet sent. Out Buffer {1}, Out Bytes Used: {2}", _objectID, _outBuff, (ushort)_outBytesUsed);
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
            if (null != statistics)
            {
                statistics.SafeIncrement(ref statistics._buffersSent);
                statistics.SafeAdd(ref statistics._bytesSent, _outBytesUsed);
                statistics.RequestNetworkServerTimer();
            }
        }

        //////////////////////////////////////////////
        // Errors and Warnings                      //
        //////////////////////////////////////////////

        

        protected abstract PacketHandle EmptyReadPacket { get; }





        private sealed class StateSnapshot
        {
            private sealed class PLPData
            {
                public readonly ulong SnapshotLongLen;
                public readonly ulong SnapshotLongLenLeft;

                public PLPData(ulong snapshotLongLen, ulong snapshotLongLenLeft)
                {
                    SnapshotLongLen = snapshotLongLen;
                    SnapshotLongLenLeft = snapshotLongLenLeft;
                }
            }

            private sealed partial class PacketData
            {
                public byte[] Buffer;
                public int Read;
                public PacketData Prev;

                public void SetStack(string value)
                {
                    SetStackInternal(value);
                }
                partial void SetStackInternal(string value);

                public void Clear()
                {
                    Buffer = null;
                    Read = 0;
                    Prev = null;
                    SetStackInternal(null);
                }
            }

#if DEBUG
            private sealed partial class PacketData
            {
                public string Stack;

                partial void SetStackInternal(string value)
                {
                    Stack = value;
                }
            }

            private int _rollingPend = 0;
            private int _rollingPendCount = 0;
#endif
            private PacketData _snapshotInBuffList;
            private PacketData _sparePacket;
            private NullBitmap _snapshotNullBitmapInfo;
            private _SqlMetaDataSet _snapshotCleanupMetaData;
            private _SqlMetaDataSetCollection _snapshotCleanupAltMetaDataSetArray;

            internal byte[] _plpBuffer;
            private PLPData _plpData;
            private TdsParserStateObject _stateObj;

            private int _snapshotInBuffCount;
            private int _snapshotInBuffCurrent;
            private int _snapshotInBytesUsed;
            private int _snapshotInBytesPacket;
            private SnapshottedStateFlags _state;
            private byte _snapshotMessageStatus;

#if DEBUG
            internal bool DoPend()
            {
                if (s_failAsyncPends || !s_forceAllPends)
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

            internal void AssertCurrent()
            {
                Debug.Assert(_snapshotInBuffCurrent == _snapshotInBuffCount, "Should not be reading new packets when not replaying last packet");
            }

            internal void CheckStack(string trace)
            {
                PacketData prev = _snapshotInBuffList?.Prev;
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
#if DEBUG
                for (PacketData current = _snapshotInBuffList; current != null; current = current.Prev)
                {
                    Debug.Assert(!object.ReferenceEquals(current.Buffer, buffer));
                }
#endif

                PacketData packetData = _sparePacket;
                if (packetData is null)
                {
                    packetData = new PacketData();
                }
                else
                {
                    _sparePacket = null;
                }
                packetData.Buffer = buffer;
                packetData.Read = read;
                packetData.Prev = _snapshotInBuffList;
#if DEBUG
                packetData.SetStack(_stateObj._lastStack);
#endif
                _snapshotInBuffList = packetData;
                _snapshotInBuffCount++;
            }

            internal bool Replay()
            {
                if (_snapshotInBuffCurrent < _snapshotInBuffCount)
                {
                    PacketData next = _snapshotInBuffList;
                    for (
                        int position = (_snapshotInBuffCount - 1);
                        position != _snapshotInBuffCurrent;
                        position -= 1
                    )
                    {
                        next = next.Prev;
                    }
                    _stateObj._inBuff = next.Buffer;
                    _stateObj._inBytesUsed = 0;
                    _stateObj._inBytesRead = next.Read;
                    _snapshotInBuffCurrent++;
                    return true;
                }

                return false;
            }

            internal void Snap(TdsParserStateObject state)
            {
                _stateObj = state;
                _snapshotInBuffList = null;
                _snapshotInBuffCount = 0;
                _snapshotInBuffCurrent = 0;
                _snapshotInBytesUsed = _stateObj._inBytesUsed;
                _snapshotInBytesPacket = _stateObj._inBytesPacket;

                _snapshotMessageStatus = _stateObj._messageStatus;
                // _nullBitmapInfo must be cloned before it is updated
                _snapshotNullBitmapInfo = _stateObj._nullBitmapInfo;
                if (_stateObj._longlen != 0 || _stateObj._longlenleft != 0)
                {
                    _plpData = new PLPData(_stateObj._longlen, _stateObj._longlenleft);
                }
                _snapshotCleanupMetaData = _stateObj._cleanupMetaData;
                // _cleanupAltMetaDataSetArray must be cloned before it is updated
                _snapshotCleanupAltMetaDataSetArray = _stateObj._cleanupAltMetaDataSetArray;

                _state = _stateObj._snapshottedState;
#if DEBUG
                _rollingPend = 0;
                _rollingPendCount = 0;
                _stateObj._lastStack = null;
                Debug.Assert(_stateObj._bTmpRead == 0, "Has partially read data when snapshot taken");
                Debug.Assert(_stateObj._partialHeaderBytesRead == 0, "Has partially read header when snapshot taken");
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

                _stateObj._messageStatus = _snapshotMessageStatus;
                _stateObj._nullBitmapInfo = _snapshotNullBitmapInfo;
                _stateObj._cleanupMetaData = _snapshotCleanupMetaData;
                _stateObj._cleanupAltMetaDataSetArray = _snapshotCleanupAltMetaDataSetArray;

                // Make sure to go through the appropriate increment/decrement methods if changing the OpenResult flag
                if (!_stateObj.HasOpenResult && ((_state & SnapshottedStateFlags.OpenResult) == SnapshottedStateFlags.OpenResult))
                {
                    _stateObj.IncrementAndObtainOpenResultCount(_stateObj._executedUnderTransaction);
                }
                else if (_stateObj.HasOpenResult && ((_state & SnapshottedStateFlags.OpenResult) != SnapshottedStateFlags.OpenResult))
                {
                    _stateObj.DecrementOpenResultCount();
                }
                _stateObj._snapshottedState = _state;

                // Reset partially read state (these only need to be maintained if doing async without snapshot)
                _stateObj._bTmpRead = 0;
                _stateObj._partialHeaderBytesRead = 0;

                // reset plp state
                _stateObj._longlen = _plpData?.SnapshotLongLen ?? 0;
                _stateObj._longlenleft = _plpData?.SnapshotLongLenLeft ?? 0;

                _stateObj._snapshotReplay = true;

                _stateObj.AssertValidState();
            }

            internal void PrepareReplay()
            {
                ResetSnapshotState();
            }

            internal void Clear()
            {
                PacketData packet = _snapshotInBuffList;
                _snapshotInBuffList = null;
                _snapshotInBuffCount = 0;
                _snapshotInBuffCurrent = 0;
                _snapshotInBytesUsed = 0;
                _snapshotInBytesPacket = 0;
                _snapshotMessageStatus = 0;
                _snapshotNullBitmapInfo = default;
                _plpData = null;
                _snapshotCleanupMetaData = null;
                _snapshotCleanupAltMetaDataSetArray = null;
                _state = SnapshottedStateFlags.None;
#if DEBUG
                _rollingPend = 0;
                _rollingPendCount = 0;
                _stateObj._lastStack = null;
#endif
                _stateObj = null;

                packet.Clear();
                _sparePacket = packet;
            }
        }
    }
}
