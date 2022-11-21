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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal partial class TdsParserStateObject
    {
        private SNIHandle _sessionHandle = null;              // the SNI handle we're to work on

        // SNI variables
        private SNIPacket _sniPacket = null;                // Will have to re-vamp this for MARS
        internal SNIPacket _sniAsyncAttnPacket = null;                // Packet to use to send Attn
        private readonly WritePacketCache _writePacketCache = new WritePacketCache(); // Store write packets that are ready to be re-used
        private readonly Dictionary<IntPtr, SNIPacket> _pendingWritePackets = new Dictionary<IntPtr, SNIPacket>(); // Stores write packets that have been sent to SNI, but have not yet finished writing (i.e. we are waiting for SNI's callback)

        // Async variables
        private GCHandle _gcHandle;                                    // keeps this object alive until we're closed.

        // This variable is used to prevent sending an attention by another thread that is not the
        // current owner of the stateObj.  I currently do not know how this can happen.  Mark added
        // the code but does not remember either.  At some point, we need to research killing this
        // logic.
        private volatile int _allowObjectID;
        
        // Used for blanking out password in trace.
        internal int _tracePasswordOffset = 0;
        internal int _tracePasswordLength = 0;
        internal int _traceChangePasswordOffset = 0;
        internal int _traceChangePasswordLength = 0;


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

            SQLFallbackDNSCache.Instance.GetDNSInfo(_parser.FQDNforDNSCache, out cachedDNSInfo);

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

        
        internal uint Status
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

                    Monitor.TryEnter(this, WaitForCancellationLockPollTimeout, ref hasLock);
                    if (hasLock)
                    { // Lock for the time being - since we need to synchronize the attention send.
                      // At some point in the future, I hope to remove this.
                      // This lock is also protecting against concurrent close and async continuations

                        // don't allow objectID -1 since it is reserved for 'not associated with a command'
                        // yes, the 2^32-1 comand won't cancel - but it also won't cancel when we don't want it
                        if ((!_cancelled) && (objectID == _allowObjectID) && (objectID != -1))
                        {
                            _cancelled = true;

                            if (HasPendingData && !_attentionSent)
                            {
                                bool hasParserLock = false;
                                // Keep looping until we have the parser lock (and so are allowed to write), or the conneciton closes\breaks
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
            string hostNameInCertificate = "")
        {
            SNINativeMethodWrapper.ConsumerInfo myInfo = CreateConsumerInfo(async);

            // Translate to SNI timeout values (Int32 milliseconds)
            long timeout;
            if (long.MaxValue == timerExpire)
            {
                timeout = int.MaxValue;
            }
            else
            {
                timeout = ADP.TimerRemainingMilliseconds(timerExpire);
                if (timeout > int.MaxValue)
                {
                    timeout = int.MaxValue;
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
                ipPreference, cachedDNSInfo, hostNameInCertificate);
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

        internal void ReadSniSyncOverAsync()
        {
            if (_parser.State == TdsParserState.Broken || _parser.State == TdsParserState.Closed)
            {
                throw ADP.ClosedConnectionError();
            }

            IntPtr readPacket = IntPtr.Zero;
            uint error;

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
                    if (s_forcePendingReadsToWaitForUser)
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
            if (s_forcePendingReadsToWaitForUser)
            {
                _realNetworkPacketTaskSource = new TaskCompletionSource<object>();
            }
#endif

            IntPtr readPacket = IntPtr.Zero;
            uint error = 0;

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
                    uint error;
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

            uint error = TdsEnums.SNI_SUCCESS;
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
        private void ReadSniError(TdsParserStateObject stateObj, uint error)
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
                    AddError(new SqlError(TdsEnums.TIMEOUT_EXPIRED, 0x00, TdsEnums.MIN_ERROR_CLASS, _parser.Server, _parser.Connection.TimeoutErrorInternal.GetErrorMessage(), "", 0, TdsEnums.SNI_WAIT_TIMEOUT));

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
        public void ProcessSniPacket(IntPtr packet, uint error)
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
                uint getDataError = SNINativeMethodWrapper.SNIPacketGetData(packet, _inBuff, ref dataSize);

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

        public void ReadAsyncCallback(IntPtr key, IntPtr packet, uint error)
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

        public void WriteAsyncCallback(IntPtr key, IntPtr packet, uint sniError)
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
                        TaskCompletionSource<object> writeCompletionSource = _writeCompletionSource;
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

#pragma warning restore 420

        /////////////////////////////////////////
        // Network/Packet Writing & Processing //
        /////////////////////////////////////////



        

       

        ////
        //// Takes a byte array and writes it to the buffer.
        ////
        //internal Task WriteByteArray(byte[] b, int len, int offsetBuffer, bool canAccumulate = true, TaskCompletionSource<object> completion = null)
        //{
        //    try
        //    {
        //        TdsParser.ReliabilitySection.Assert("unreliable call to WriteByteArray");  // you need to setup for a thread abort somewhere before you call this method

        //        bool async = _parser._asyncWrite;  // NOTE: We are capturing this now for the assert after the Task is returned, since WritePacket will turn off async if there is an exception
        //        Debug.Assert(async || _asyncWriteCount == 0);
        //        // Do we have to send out in packet size chunks, or can we rely on netlib layer to break it up?
        //        // would prefer to to do something like:
        //        //
        //        // if (len > what we have room for || len > out buf)
        //        //   flush buffer
        //        //   UnsafeNativeMethods.Write(b)
        //        //

        //        int offset = offsetBuffer;

        //        Debug.Assert(b.Length >= len, "Invalid length sent to WriteByteArray()!");

        //        // loop through and write the entire array
        //        do
        //        {
        //            if ((_outBytesUsed + len) > _outBuff.Length)
        //            {
        //                // If the remainder of the string won't fit into the buffer, then we have to put
        //                // whatever we can into the buffer, and flush that so we can then put more into
        //                // the buffer on the next loop of the while.

        //                int remainder = _outBuff.Length - _outBytesUsed;

        //                // write the remainder
        //                Buffer.BlockCopy(b, offset, _outBuff, _outBytesUsed, remainder);

        //                // handle counters
        //                offset += remainder;
        //                _outBytesUsed += remainder;
        //                len -= remainder;

        //                Task packetTask = WritePacket(TdsEnums.SOFTFLUSH, canAccumulate);

        //                if (packetTask != null)
        //                {
        //                    Task task = null;
        //                    Debug.Assert(async, "Returned task in sync mode");
        //                    if (completion == null)
        //                    {
        //                        completion = new TaskCompletionSource<object>();
        //                        task = completion.Task; // we only care about return from topmost call, so do not access Task property in other cases
        //                    }
        //                    WriteByteArraySetupContinuation(b, len, completion, offset, packetTask);
        //                    return task;
        //                }

        //            }
        //            else
        //            { //((stateObj._outBytesUsed + len) <= stateObj._outBuff.Length )
        //                // Else the remainder of the string will fit into the buffer, so copy it into the
        //                // buffer and then break out of the loop.

        //                Buffer.BlockCopy(b, offset, _outBuff, _outBytesUsed, len);

        //                // handle out buffer bytes used counter
        //                _outBytesUsed += len;
        //                break;
        //            }
        //        } while (len > 0);

        //        if (completion != null)
        //        {
        //            completion.SetResult(null);
        //        }
        //        return null;
        //    }
        //    catch (Exception e)
        //    {
        //        if (completion != null)
        //        {
        //            completion.SetException(e);
        //            return null;
        //        }
        //        else
        //        {
        //            throw;
        //        }
        //    }
        //}

        // This is in its own method to avoid always allocating the lambda in WriteByteArray
        private void WriteBytesSetupContinuation(byte[] b, int len, TaskCompletionSource<object> completion, int offset, Task packetTask)
        {
            AsyncHelper.ContinueTask(packetTask, completion,
                () => WriteByteArray(b, len: len, offsetBuffer: offset, canAccumulate: false, completion: completion),
                connectionToDoom: _parser.Connection
            );
        }

#pragma warning disable 420 // a reference to a volatile field will not be treated as volatile

        private Task SNIWritePacket(SNIHandle handle, SNIPacket packet, out uint sniError, bool canAccumulate, bool callerHasConnectionLock, bool asyncClose = false)
        {
            // Check for a stored exception
            Exception delayedException = Interlocked.Exchange(ref _delayedWriteAsyncCallbackException, null);
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
                        Debug.Assert(packetPointer != IntPtr.Zero, "Packet added to list has an invalid pointer, can not remove from pending list");
                        RemovePacketFromPendingList(packetPointer);
                    }
                }
                else
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.WritePacket|Info> write async returned error code {0}", (int)sniError);
                    AddError(_parser.ProcessSNIError(this));
                    ThrowExceptionAndWarning(callerHasConnectionLock, false);
                }
                AssertValidState();
            }
            return task;
        }

#pragma warning restore 420 

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
                            SNIWritePacket(Handle, attnPacket, out sniError, canAccumulate: false, callerHasConnectionLock: false, asyncClose);
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

            Debug.Assert(Parser.Connection._parserLock.ThreadMayHaveLock(), "Thread is writing without taking the connection lock");
            Task task = SNIWritePacket(Handle, packet, out _, canAccumulate, callerHasConnectionLock: true);

            // Check to see if the timeout has occurred.  This time out code is special case code to allow BCP writes to timeout to fix bug 350558, eventually we should make all writes timeout.
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

        //////////////////////////////////////////////
        // Errors and Warnings                      //
        //////////////////////////////////////////////


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
            private SnapshottedStateFlags _state = SnapshottedStateFlags.None;
            private byte _snapshotMessageStatus;

            private NullBitmap _snapshotNullBitmapInfo;
            private ulong _snapshotLongLen;
            private ulong _snapshotLongLenLeft;
            private _SqlMetaDataSet _snapshotCleanupMetaData;
            private _SqlMetaDataSetCollection _snapshotCleanupAltMetaDataSetArray;
            internal byte[] _plpBuffer;

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
                _snapshotMessageStatus = _stateObj._messageStatus;
                // _nullBitmapInfo must be cloned before it is updated
                _snapshotNullBitmapInfo = _stateObj._nullBitmapInfo;
                _snapshotLongLen = _stateObj._longlen;
                _snapshotLongLenLeft = _stateObj._longlenleft;
                _snapshotCleanupMetaData = _stateObj._cleanupMetaData;
                // _cleanupAltMetaDataSetArray must be cloned bofore it is updated
                _snapshotCleanupAltMetaDataSetArray = _stateObj._cleanupAltMetaDataSetArray;
                
                _state = _stateObj._snapshottedState;
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

                _stateObj._messageStatus = _snapshotMessageStatus;
                _stateObj._nullBitmapInfo = _snapshotNullBitmapInfo;
                _stateObj._cleanupMetaData = _snapshotCleanupMetaData;
                _stateObj._cleanupAltMetaDataSetArray = _snapshotCleanupAltMetaDataSetArray;

                // Make sure to go through the appropriate increment/decrement methods if changing HasOpenResult
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
    }
}
