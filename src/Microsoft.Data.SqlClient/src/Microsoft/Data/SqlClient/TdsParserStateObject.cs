// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
#if NETFRAMEWORK
using System.Runtime.ConstrainedExecution;
#endif

namespace Microsoft.Data.SqlClient
{
#if NETFRAMEWORK
    using RuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;
#endif
    
    sealed internal class LastIOTimer
    {
        internal long _value;
    }

    internal enum TdsOperationStatus : int
    {
        Done = 0,
        NeedMoreData = 1,
        InvalidData = 2
    }

    internal abstract partial class TdsParserStateObject
    {
        private static readonly ContextCallback s_readAsyncCallbackComplete = ReadAsyncCallbackComplete;

        private static int s_objectTypeCount; // EventSource counter
        internal readonly int _objectID = Interlocked.Increment(ref s_objectTypeCount);

        [Flags]
        internal enum SnapshottedStateFlags : byte
        {
            None = 0,
            PendingData = 1 << 1,
            OpenResult = 1 << 2,
            ErrorTokenReceived = 1 << 3,  // Keep track of whether an error was received for the result. This is reset upon each done token
            ColMetaDataReceived = 1 << 4, // Used to keep track of when to fire StatementCompleted event.
            AttentionReceived = 1 << 5    // NOTE: Received is not volatile as it is only ever accessed\modified by TryRun its callees (i.e. single threaded access)
        }

        private sealed class TimeoutState
        {
            public const int Stopped = 0;
            public const int Running = 1;
            public const int ExpiredAsync = 2;
            public const int ExpiredSync = 3;

            private readonly int _value;

            public TimeoutState(int value)
            {
                _value = value;
            }

            public int IdentityValue => _value;
        }

        private enum SnapshotStatus
        {
            NotActive,
            ReplayStarting,
            ReplayRunning,
            ContinueRunning
        }

        private const int AttentionTimeoutSeconds = 5;

        // Ticks to consider a connection "good" after a successful I/O (10,000 ticks = 1 ms)
        // The resolution of the timer is typically in the range 10 to 16 milliseconds according to msdn.
        // We choose a value that is smaller than the likely timer resolution, but
        // large enough to ensure that check connection execution will be 0.1% or less
        // of very small open, query, close loops.
        private const long CheckConnectionWindow = 50000;

        protected readonly TdsParser _parser;                            // TdsParser pointer
        private readonly WeakReference<object> _owner = new(null);   // the owner of this session, used to track when it's been orphaned
        internal SqlDataReader.SharedState _readerState;                    // susbset of SqlDataReader state (if it is the owner) necessary for parsing abandoned results in TDS
        private int _activateCount;                     // 0 when we're in the pool, 1 when we're not, all others are an error
        private SnapshottedStateFlags _snapshottedState;

        // Two buffers exist in tdsparser, an in buffer and an out buffer.  For the out buffer, only
        // one bookkeeping variable is needed, the number of bytes used in the buffer.  For the in buffer,
        // three variables are actually needed.  First, we need to record from the netlib how many bytes it
        // read from the netlib, this variable is _inBytesRead.  Then, we need to also keep track of how many
        // bytes we have used as we consume the bytes from the buffer, that variable is _inBytesUsed.  Third,
        // we need to keep track of how many bytes are left in the packet, so that we know when we have reached
        // the end of the packet and so we need to consume the next header.  That variable is _inBytesPacket.

        // Header length constants
        internal readonly int _inputHeaderLen = TdsEnums.HEADER_LEN;
        internal readonly int _outputHeaderLen = TdsEnums.HEADER_LEN;

        // Out buffer variables
        internal byte[] _outBuff;                         // internal write buffer - initialize on login
        internal int _outBytesUsed = TdsEnums.HEADER_LEN; // number of bytes used in internal write buffer - initialize past header

        // In buffer variables

        /// <summary>
        /// internal read buffer - initialize on login
        /// </summary>
        protected byte[] _inBuff;
        /// <summary>
        /// number of bytes used in internal read buffer
        /// </summary>
        internal int _inBytesUsed;
        /// <summary>
        /// number of bytes read into internal read buffer
        /// </summary>
        internal int _inBytesRead;

        /// <summary>
        /// number of bytes left in packet
        /// </summary>
        internal int _inBytesPacket;

        internal int _spid;                                 // SPID of the current connection

        // Packet state variables
        internal byte _outputMessageType;                   // tds header type
        internal byte _messageStatus;                       // tds header status
        internal byte _outputPacketNumber = 1;              // number of packets sent to server in message - start at 1 per ramas
        internal uint _outputPacketCount;

        internal volatile bool _fResetEventOwned;           // ResetEvent serializing call to sp_reset_connection
        internal volatile bool _fResetConnectionSent;       // For multiple packet execute
        internal bool _bulkCopyOpperationInProgress;        // Set to true during bulk copy and used to turn toggle write timeouts.
        internal bool _bulkCopyWriteTimeout;                // Set to trun when _bulkCopyOperationInProgress is trun and write timeout happens

        // SNI variables
        /// <summary>
        /// Used to synchronize access to _writePacketCache and _pendingWritePackets
        /// </summary>
        protected readonly object _writePacketLockObject = new object();

        // Async variables
        private int _pendingCallbacks;                      // we increment this before each async read/write call and decrement it in the callback.  We use this to determine when to release the GcHandle...

        // Timeout variables
        private long _timeoutMilliseconds;
        private long _timeoutTime;                          // variable used for timeout computations, holds the value of the hi-res performance counter at which this request should expire
        private int _timeoutState;                          // expected to be one of the constant values TimeoutStopped, TimeoutRunning, TimeoutExpiredAsync, TimeoutExpiredSync
        private int _timeoutIdentitySource;
        private volatile int _timeoutIdentityValue;
        internal volatile bool _attentionSent;              // true if we sent an Attention to the server
        internal volatile bool _attentionSending;
        private readonly TimerCallback _onTimeoutAsync;
        private readonly WeakReference _cancellationOwner = new WeakReference(null);

        // Below 2 properties are used to enforce timeout delays in code to 
        // reproduce issues related to theadpool starvation and timeout delay.
        // It should always be set to false by default, and only be enabled during testing.
        internal bool _enforceTimeoutDelay = false;
        internal int _enforcedTimeoutDelayInMilliSeconds = 5000;

        private readonly LastIOTimer _lastSuccessfulIOTimer;

        // secure password information to be stored
        //  At maximum number of secure string that need to be stored is two; one for login password and the other for new change password
        private readonly SecureString[] _securePasswords = new SecureString[2] { null, null };
        private readonly int[] _securePasswordOffsetsInBuffer = new int[2];

        // This variable is used to track whether another thread has requested a cancel.  The
        // synchronization points are
        //   On the user's execute thread:
        //     1) the first packet write
        //     2) session close - return this stateObj to the session pool
        //   On cancel thread we only have the cancel call.
        // Currently all access to this variable is inside a lock, The state diagram is:
        // 1) pre first packet write, if cancel is requested, set variable so exception is triggered
        //    on user thread when first packet write is attempted
        // 2) post first packet write, but before session return - a call to cancel will send an
        //    attention to the server
        // 3) post session close - no attention is allowed
        private bool _cancelled;
        private const int WaitForCancellationLockPollTimeout = 100;

        // Cache the transaction for which this command was executed so upon completion we can
        // decrement the appropriate result count.
        internal SqlInternalTransaction _executedUnderTransaction;

        // TDS stream processing variables
        internal ulong _longlen;                                     // plp data length indicator
        internal ulong _longlenleft;                                 // Length of data left to read (64 bit lengths)
        internal int[] _decimalBits;                // scratch buffer for decimal/numeric data
        internal byte[] _bTmp = new byte[TdsEnums.SQL2005_HEADER_LEN];  // Scratch buffer for misc use
        internal int _bTmpRead;                   // Counter for number of temporary bytes read
        internal Decoder _plpdecoder;             // Decoder object to process plp character data
        internal bool _accumulateInfoEvents;               // TRUE - accumulate info messages during TdsParser.Run, FALSE - fire them
        internal List<SqlError> _pendingInfoEvents;
        internal byte[] _bLongBytes;                 // scratch buffer to serialize Long values (8 bytes).
        internal byte[] _bIntBytes;                 // scratch buffer to serialize Int values (4 bytes).
        internal byte[] _bShortBytes;                 // scratch buffer to serialize Short values (2 bytes).
        internal byte[] _bDecimalBytes;                 // scratch buffer to serialize decimal values (17 bytes).

        // DO NOT USE THIS BUFFER FOR OTHER THINGS.
        // ProcessHeader can be called ANYTIME while doing network reads.
        private readonly byte[] _partialHeaderBuffer = new byte[TdsEnums.HEADER_LEN];   // Scratch buffer for ProcessHeader
        internal int _partialHeaderBytesRead;

        internal _SqlMetaDataSet _cleanupMetaData;
        internal _SqlMetaDataSetCollection _cleanupAltMetaDataSetArray;

        private SniContext _sniContext = SniContext.Undefined;
#if DEBUG
        private SniContext _debugOnlyCopyOfSniContext = SniContext.Undefined;
#endif

        private bool _bcpLock;

        // Null bitmap compression (NBC) information for the current row
        private NullBitmap _nullBitmapInfo;

        // Async
        internal TaskCompletionSource<object> _networkPacketTaskSource;
        private Timer _networkPacketTimeout;
        internal bool _syncOverAsync = true;
        private SnapshotStatus _snapshotStatus;
        private StateSnapshot _snapshot;
        private StateSnapshot _cachedSnapshot;
        internal ExecutionContext _executionContext;
        internal bool _asyncReadWithoutSnapshot;
#if DEBUG
        // Used to override the assert than ensures that the stacktraces on subsequent replays are the same
        // This is useful is you are purposefully running the replay from a different thread (e.g. during SqlDataReader.Close)
        internal bool _permitReplayStackTraceToDiffer;

        // Used to indicate that the higher level object believes that this stateObj has enough data to complete an operation
        // If this stateObj has to read, then it will raise an assert
        internal bool _shouldHaveEnoughData;
#endif

        // local exceptions to cache warnings and errors
        internal SqlErrorCollection _errors;
        internal SqlErrorCollection _warnings;
        internal object _errorAndWarningsLock = new object();
        private bool _hasErrorOrWarning;

        // local exceptions to cache warnings and errors that occurred prior to sending attention
        internal SqlErrorCollection _preAttentionErrors;
        internal SqlErrorCollection _preAttentionWarnings;

        private volatile TaskCompletionSource<object> _writeCompletionSource;
        protected volatile int _asyncWriteCount;
        private volatile Exception _delayedWriteAsyncCallbackException; // set by write async callback if completion source is not yet created

        // _readingcount is incremented when we are about to read.
        // We check the parser state afterwards.
        // When the read is completed, we decrement it before handling errors
        // as the error handling may end up calling Dispose.
        private int _readingCount;

        // Test hooks
#if DEBUG
        // This is a test hook to enable testing of the retry paths.
        // When set to true, almost every possible retry point will be attempted.
        // This will drastically impact performance.
        //
        // Sample code to enable:
        //
        //    Type type = typeof(SqlDataReader).Assembly.GetType("Microsoft.Data.SqlClient.TdsParserStateObject");
        //    System.Reflection.FieldInfo field = type.GetField("_forceAllPends", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        //    if (field != null) {
        //        field.SetValue(null, true);
        //    }
        //
        internal static bool s_forceAllPends = false;

        // set this while making a call that should not block.
        // instead of blocking it will fail.
        internal static bool s_failAsyncPends = false;

        // If this is set and an async read is made, then
        // we will switch to syncOverAsync mode for the
        // remainder of the async operation.
        internal static bool s_forceSyncOverAsyncAfterFirstPend = false;

        // Requests to send attention will be ignored when s_skipSendAttention is true.
        // This is useful to simulate circumstances where timeouts do not recover.
        internal static bool s_skipSendAttention = false;

        // Prevents any pending read from completing until the user signals it using
        // CompletePendingReadWithSuccess() or CompletePendingReadWithFailure(int errorCode) in SqlCommand\SqlDataReader
        internal static bool s_forcePendingReadsToWaitForUser = false;
        internal TaskCompletionSource<object> _realNetworkPacketTaskSource;

        // Field is never assigned to, and will always have its default value
#pragma warning disable 0649
        // Set to true to enable checking the call stacks match when packet retry occurs.
        internal static bool s_checkNetworkPacketRetryStacks = false;
#pragma warning restore 0649
#endif

        //////////////////
        // Constructors //
        //////////////////

        protected TdsParserStateObject(TdsParser parser)
        {
            // Construct a physical connection
            Debug.Assert(parser != null, "no parser?");
            _parser = parser;
            _onTimeoutAsync = OnTimeoutAsync;

            // For physical connection, initialize to default login packet size.
            SetPacketSize(TdsEnums.DEFAULT_LOGIN_PACKET_SIZE);

            // we post a callback that represents the call to dispose; once the
            // object is disposed, the next callback will cause the GC Handle to
            // be released.
            IncrementPendingCallbacks();
            _lastSuccessfulIOTimer = new LastIOTimer();
        }

        private void SetSnapshottedState(SnapshottedStateFlags flag, bool value)
        {
            if (value)
            {
                _snapshottedState |= flag;
            }
            else
            {
                _snapshottedState &= ~flag;
            }
        }

        private bool GetSnapshottedState(SnapshottedStateFlags flag)
        {
            return (_snapshottedState & flag) == flag;
        }

        ////////////////
        // Properties //
        ////////////////

        internal bool HasOpenResult
        {
            get => GetSnapshottedState(SnapshottedStateFlags.OpenResult);
            set => SetSnapshottedState(SnapshottedStateFlags.OpenResult, value);
        }

        internal bool HasPendingData
        {
            get => GetSnapshottedState(SnapshottedStateFlags.PendingData);
            set => SetSnapshottedState(SnapshottedStateFlags.PendingData, value);
        }

        internal bool HasReceivedError
        {
            get => GetSnapshottedState(SnapshottedStateFlags.ErrorTokenReceived);
            set => SetSnapshottedState(SnapshottedStateFlags.ErrorTokenReceived, value);
        }

        internal bool HasReceivedAttention
        {
            get => GetSnapshottedState(SnapshottedStateFlags.AttentionReceived);
            set => SetSnapshottedState(SnapshottedStateFlags.AttentionReceived, value);
        }

        internal bool HasReceivedColumnMetadata
        {
            get => GetSnapshottedState(SnapshottedStateFlags.ColMetaDataReceived);
            set => SetSnapshottedState(SnapshottedStateFlags.ColMetaDataReceived, value);
        }

        internal int ObjectID => _objectID;

        // BcpLock - use to lock this object if there is a potential risk of using this object
        // between tds packets
        internal bool BcpLock
        {
            get => _bcpLock;
            set => _bcpLock = value;
        }

#if DEBUG
        internal SniContext DebugOnlyCopyOfSniContext => _debugOnlyCopyOfSniContext;

        internal void InvalidateDebugOnlyCopyOfSniContext()
        {
            _debugOnlyCopyOfSniContext = SniContext.Undefined;
        }
#endif

        internal bool IsOrphaned
        {
            get
            {
                bool isAlive = _owner.TryGetTarget(out object target);
                Debug.Assert((0 == _activateCount && !isAlive) // in pool
                             || (1 == _activateCount && isAlive && target != null)
                             || (1 == _activateCount && !isAlive), "Unknown state on TdsParserStateObject.IsOrphaned!");
                return (0 != _activateCount && !isAlive);
            }
        }

        internal object Owner
        {
            set
            {
                Debug.Assert(value == null || !_owner.TryGetTarget(out object target) || value is SqlDataReader reader1 && reader1.Command == target, "Should not be changing the owner of an owned stateObj");
                if (value is SqlDataReader reader)
                {
                    _readerState = reader._sharedState;
                }
                else
                {
                    _readerState = null;
                }
                _owner.SetTarget(value);
            }
        }

        internal bool HasOwner => _owner.TryGetTarget(out _);

        internal TdsParser Parser => _parser;

        internal SniContext SniContext
        {
            get
            {
                return _sniContext;
            }
            set
            {
                _sniContext = value;
#if DEBUG
                _debugOnlyCopyOfSniContext = value;
#endif
            }
        }

        internal bool TimeoutHasExpired
        {
            get
            {
                Debug.Assert(0 == _timeoutMilliseconds || 0 == _timeoutTime, "_timeoutTime hasn't been reset");
                return TdsParserStaticMethods.TimeoutHasExpired(_timeoutTime);
            }
        }

        internal long TimeoutTime
        {
            get
            {
                if (0 != _timeoutMilliseconds)
                {
                    _timeoutTime = TdsParserStaticMethods.GetTimeout(_timeoutMilliseconds);
                    _timeoutMilliseconds = 0;
                }
                return _timeoutTime;
            }
            set
            {
                _timeoutMilliseconds = 0;
                _timeoutTime = value;
            }
        }

        ////////////////
        // Properties //
        ////////////////

        internal abstract uint Status { get; }

        internal abstract Guid? SessionId { get; }

        internal abstract SessionHandle SessionHandle { get; }

        internal abstract uint SniGetConnectionId(ref Guid clientConnectionId);

        internal abstract uint DisableSsl();

        internal abstract SspiContextProvider CreateSspiContextProvider();

        internal abstract uint EnableMars(ref uint info);

        internal abstract uint SetConnectionBufferSize(ref uint unsignedPacketSize);

        internal int GetTimeoutRemaining()
        {
            int remaining;
            if (0 != _timeoutMilliseconds)
            {
                remaining = (int)Math.Min((long)int.MaxValue, _timeoutMilliseconds);
                _timeoutTime = TdsParserStaticMethods.GetTimeout(_timeoutMilliseconds);
                _timeoutMilliseconds = 0;
            }
            else
            {
                remaining = TdsParserStaticMethods.GetTimeoutMilliseconds(_timeoutTime);
            }
            return remaining;
        }

        internal TdsOperationStatus TryStartNewRow(bool isNullCompressed, int nullBitmapColumnsCount = 0)
        {
            Debug.Assert(!isNullCompressed || nullBitmapColumnsCount > 0, "Null-Compressed row requires columns count");

            _snapshot?.CloneNullBitmapInfo();

            // initialize or unset null bitmap information for the current row
            if (isNullCompressed)
            {
                // assert that NBCROW is not in use by 2005 or before
                Debug.Assert(_parser.Is2008OrNewer, "NBCROW is sent by pre-2008 server");

                TdsOperationStatus result = _nullBitmapInfo.TryInitialize(this, nullBitmapColumnsCount);
                if (result != TdsOperationStatus.Done)
                {
                    return result;
                }
            }
            else
            {
                _nullBitmapInfo.Clean();
            }

            return TdsOperationStatus.Done;
        }

        internal TdsOperationStatus TryReadChars(char[] chars, int charsOffset, int charsCount, out int charsCopied)
        {
            charsCopied = 0;
            while (charsCopied < charsCount)
            {
                // check if the current buffer contains some bytes we need to copy and copy them
                //  in a block
                int bytesToRead = Math.Min(
                    (charsCount - charsCopied) * 2,
                    unchecked((_inBytesRead - _inBytesUsed) & (int)0xFFFFFFFE) // it the result is odd take off the 0 to make it even
                );
                if (bytesToRead > 0)
                {
                    Buffer.BlockCopy(
                        _inBuff,
                        _inBytesUsed,
                        chars,
                        (charsOffset + charsCopied) * 2, // offset in bytes,
                        bytesToRead
                    );
                    charsCopied += (bytesToRead / 2);
                    _inBytesUsed += bytesToRead;
                    _inBytesPacket -= bytesToRead;
                }

                // if the number of chars requested is lower than the number copied then we need
                //  to request a new packet, use TryReadChar() to do this then loop back to see
                //  if we can copy another bulk of chars from the new buffer

                if (charsCopied < charsCount)
                {
                    TdsOperationStatus result = TryReadChar(out chars[charsOffset + charsCopied]);
                    if (result == TdsOperationStatus.Done)
                    {
                        charsCopied += 1;
                    }
                    else
                    {
                        return result;
                    }
                }
            }
            if (!BitConverter.IsLittleEndian)
            {
                for (int ii = charsOffset; ii < charsCopied + charsOffset; ii++)
                {
                    chars[ii] = (char)BinaryPrimitives.ReverseEndianness((ushort)chars[ii]);
                }
            }
            return TdsOperationStatus.Done;
        }

        internal bool IsRowTokenReady()
        {
            // Removing one byte since TryReadByteArray\TryReadByte will aggressively read the next packet if there is no data left - so we need to ensure there is a spare byte
            int bytesRemaining = Math.Min(_inBytesPacket, _inBytesRead - _inBytesUsed) - 1;

            if (bytesRemaining > 0)
            {
                if (_inBuff[_inBytesUsed] == TdsEnums.SQLROW)
                {
                    // At a row token, so we're ready
                    return true;
                }
                else if (_inBuff[_inBytesUsed] == TdsEnums.SQLNBCROW)
                {
                    // NBC row token, ensure that we have enough data for the bitmap
                    // SQLNBCROW + Null Bitmap (copied from NullBitmap.TryInitialize)
                    int bytesToRead = 1 + (_cleanupMetaData.Length + 7) / 8;
                    return (bytesToRead <= bytesRemaining);
                }
            }

            // No data left, or not at a row token
            return false;
        }

        internal bool IsNullCompressionBitSet(int columnOrdinal)
        {
            return _nullBitmapInfo.IsGuaranteedNull(columnOrdinal);
        }

        private struct NullBitmap
        {
            private byte[] _nullBitmap;
            private int _columnsCount; // set to 0 if not used or > 0 for NBC rows

            internal TdsOperationStatus TryInitialize(TdsParserStateObject stateObj, int columnsCount)
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
                TdsOperationStatus result = stateObj.TryReadByteArray(_nullBitmap, _nullBitmap.Length);
                if (result != TdsOperationStatus.Done)
                {
                    return result;
                }

                SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObject.NullBitmap.Initialize | INFO | ADV | State Object Id {0}, NBCROW bitmap received, column count = {1}", stateObj.ObjectID, columnsCount);
                SqlClientEventSource.Log.TryAdvancedTraceBinEvent("TdsParserStateObject.NullBitmap.Initialize | INFO | ADV | State Object Id {0}, NBCROW bitmap data. Null Bitmap: {1}, Null bitmap length: {2}", stateObj.ObjectID, _nullBitmap, _nullBitmap.Length);

                return TdsOperationStatus.Done;
            }

            internal bool ReferenceEquals(NullBitmap obj)
            {
                return object.ReferenceEquals(_nullBitmap, obj._nullBitmap);
            }

            internal NullBitmap Clone()
            {
                NullBitmap newBitmap = new NullBitmap();
                newBitmap._nullBitmap = _nullBitmap == null ? null : (byte[])_nullBitmap.Clone();
                newBitmap._columnsCount = _columnsCount;
                return newBitmap;
            }

            internal void Clean()
            {
                _columnsCount = 0;
                // no need to free _nullBitmap array - it is cached for the next row
            }

            /// <summary>
            /// If this method returns true, the value is guaranteed to be null. This is not true vice versa:
            /// if the bitmap value is false (if this method returns false), the value can be either null or non-null - no guarantee in this case.
            /// To determine whether it is null or not, read it from the TDS (per NBCROW design spec, for IMAGE/TEXT/NTEXT columns server might send
            /// bitmap = 0, when the actual value is null).
            /// </summary>
            internal bool IsGuaranteedNull(int columnOrdinal)
            {
                if (_columnsCount == 0)
                {
                    // not an NBC row
                    return false;
                }

                Debug.Assert(columnOrdinal >= 0 && columnOrdinal < _columnsCount, "Invalid column ordinal");

                byte testBit = (byte)(1 << (columnOrdinal & 0x7)); // columnOrdinal & 0x7 == columnOrdinal MOD 0x7
                byte testByte = _nullBitmap[columnOrdinal >> 3];
                return (testBit & testByte) != 0;
            }
        }


        /////////////////////
        // General methods //
        /////////////////////

        // If this object is part of a TdsParserSessionPool, then this *must* be called inside the pool's lock
        internal void Activate(object owner)
        {
            Debug.Assert(_parser.MARSOn, "Can not activate a non-MARS connection");
            Owner = owner; // must assign an owner for reclamation to work
            int result = Interlocked.Increment(ref _activateCount);   // must have non-zero activation count for reclamation to work too.
            Debug.Assert(result == 1, "invalid deactivate count");
        }

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

        // CancelRequest - use to cancel while writing a request to the server
        //
        // o none of the request might have been sent to the server, simply reset the buffer,
        //   sending attention does not hurt
        // o the request was partially written. Send an ignore header to the server. attention is
        //   required if the server was waiting for data (e.g. insert bulk rows)
        // o the request was completely written out and the server started to process the request.
        //   attention is required to have the server stop processing.
        //
        internal void CancelRequest()
        {
            ResetBuffer();    // clear out unsent buffer
            // VSDD#903514, if the first sqlbulkcopy timeout, _outputPacketNumber may not be 1,
            // the next sqlbulkcopy (same connection string) requires this to be 1, hence reset
            // it here when exception happens in the first sqlbulkcopy
            ResetPacketCounters();

            // VSDD#907507, if bulkcopy write timeout happens, it already sent the attention,
            // so no need to send it again
            if (!_bulkCopyWriteTimeout)
            {
                SendAttention();
                Parser.ProcessPendingAck(this);
            }
        }

        public void CheckSetResetConnectionState(uint error, CallbackType callbackType)
        {
            // Should only be called for MARS - that is the only time we need to take
            // the ResetConnection lock!

            // SQL BU DT 333026 - It was raised in a security review by Microsoft questioning whether
            // we need to actually process the resulting packet (sp_reset ack or error) to know if the
            // reset actually succeeded.  There was a concern that if the reset failed and we proceeded
            // there might be a security issue present.  We have been assured by the server that if
            // sp_reset fails, they guarantee they will kill the resulting connection.  So - it is
            // safe for us to simply receive the packet and then consume the pre-login later.

            Debug.Assert(_parser.MARSOn, "Should not be calling CheckSetResetConnectionState on non MARS connection");

            if (_fResetEventOwned)
            {
                if (callbackType == CallbackType.Read && TdsEnums.SNI_SUCCESS == error)
                {
                    // RESET SUCCEEDED!
                    // If we are on read callback and no error occurred (and we own reset event) -
                    // then we sent the sp_reset_connection and so we need to reset sp_reset_connection
                    // flag to false, and then release the ResetEvent.
                    _parser._fResetConnection = false;
                    _fResetConnectionSent = false;
                    _fResetEventOwned = !_parser._resetConnectionEvent.Set();
                    Debug.Assert(!_fResetEventOwned, "Invalid AutoResetEvent state!");
                }

                if (TdsEnums.SNI_SUCCESS != error)
                {
                    // RESET FAILED!

                    // If write or read failed with reset, we need to clear event but not mark connection
                    // as reset.
                    _fResetConnectionSent = false;
                    _fResetEventOwned = !_parser._resetConnectionEvent.Set();
                    Debug.Assert(!_fResetEventOwned, "Invalid AutoResetEvent state!");
                }
            }
        }

        internal void StartSession(object cancellationOwner)
        {
            _cancellationOwner.Target = cancellationOwner;
        }

        internal void CloseSession()
        {
            ResetCancelAndProcessAttention();
#if DEBUG
            InvalidateDebugOnlyCopyOfSniContext();
#endif
            Parser.PutSession(this);
        }

#if NETFRAMEWORK
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#endif
        internal int IncrementPendingCallbacks()
        {
            int remaining = Interlocked.Increment(ref _pendingCallbacks);
            SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObject.IncrementPendingCallbacks | ADV | State Object Id {0}, after incrementing _pendingCallbacks: {1}", _objectID, _pendingCallbacks);
            Debug.Assert(0 < remaining && remaining <= 3, $"_pendingCallbacks values is invalid after incrementing: {remaining}");
            return remaining;
        }

        internal bool Deactivate()
        {
            bool goodForReuse = false;

            try
            {
                TdsParserState state = Parser.State;
                if (state != TdsParserState.Broken && state != TdsParserState.Closed)
                {
                    if (HasPendingData)
                    {
                        Parser.DrainData(this); // This may throw - taking us to catch block.c
                    }

                    if (HasOpenResult)
                    { // SQL BU DT 383773 - need to decrement openResultCount for all pending operations.
                        DecrementOpenResultCount();
                    }

                    ResetCancelAndProcessAttention();
                    goodForReuse = true;
                }
            }
            catch (Exception e)
            {
                if (!ADP.IsCatchableExceptionType(e))
                {
                    throw;
                }

                ADP.TraceExceptionWithoutRethrow(e);
            }
            return goodForReuse;
        }

        // If this object is part of a TdsParserSessionPool, then this *must* be called inside the pool's lock
        internal void RemoveOwner()
        {
            if (_parser.MARSOn)
            {
                // We only care about the activation count for MARS connections
                int result = Interlocked.Decrement(ref _activateCount);   // must have non-zero activation count for reclamation to work too.
                Debug.Assert(result == 0, "invalid deactivate count");
            }
            Owner = null;
        }

        internal void DecrementOpenResultCount()
        {
            if (_executedUnderTransaction == null)
            {
                // If we were not executed under a transaction - decrement the global count
                // on the parser.
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObject.DecrementOpenResultCount | INFO | State Object Id {0}, Processing Attention.", _objectID);
                _parser.DecrementNonTransactedOpenResultCount();
            }
            else
            {
                // If we were executed under a transaction - decrement the count on the transaction.
                _executedUnderTransaction.DecrementAndObtainOpenResultCount();
                _executedUnderTransaction = null;
            }
            HasOpenResult = false;
        }

        internal void DisposeCounters()
        {
            Timer networkPacketTimeout = _networkPacketTimeout;
            if (networkPacketTimeout != null)
            {
                _networkPacketTimeout = null;
                networkPacketTimeout.Dispose();
            }

            Debug.Assert(Volatile.Read(ref _readingCount) >= 0, "_readingCount is negative");
            if (Volatile.Read(ref _readingCount) > 0)
            {
                // if _reading is true, we need to wait for it to complete
                // if _reading is false, then future read attempts will
                // already see the null _sessionHandle and abort.

                // We block after nulling _sessionHandle but before disposing it
                // to give a chance for a read that has already grabbed the
                // handle to complete.
                SpinWait.SpinUntil(() => Volatile.Read(ref _readingCount) == 0);
            }
        }

        internal int IncrementAndObtainOpenResultCount(SqlInternalTransaction transaction)
        {
            HasOpenResult = true;

            if (transaction == null)
            {
                // If we are not passed a transaction, we are not executing under a transaction
                // and thus we should increment the global connection result count.
                return _parser.IncrementNonTransactedOpenResultCount();
            }
            else
            {
                // If we are passed a transaction, we are executing under a transaction
                // and thus we should increment the transaction's result count.
                _executedUnderTransaction = transaction;
                return transaction.IncrementAndObtainOpenResultCount();
            }
        }

        internal void SetTimeoutSeconds(int timeout)
        {
            SetTimeoutMilliseconds((long)timeout * 1000L);
        }

        internal void SetTimeoutMilliseconds(long timeout)
        {
            if (timeout <= 0)
            {
                // 0 or less (i.e. Timespan.Infinite) == infinite (which is represented by Int64.MaxValue)
                _timeoutMilliseconds = 0;
                _timeoutTime = long.MaxValue;
            }
            else
            {
                _timeoutMilliseconds = timeout;
                _timeoutTime = 0;
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

        internal void ThrowExceptionAndWarning(bool callerHasConnectionLock = false, bool asyncClose = false)
        {
            _parser.ThrowExceptionAndWarning(this, null, callerHasConnectionLock, asyncClose);
        }

        ////////////////////////////////////////////
        // TDS Packet/buffer manipulation methods //
        ////////////////////////////////////////////

        internal Task ExecuteFlush()
        {
            lock (this)
            {
                if (_cancelled && 1 == _outputPacketNumber)
                {
                    ResetBuffer();
                    _cancelled = false;
                    throw SQL.OperationCancelled();
                }
                else
                {
                    Task writePacketTask = WritePacket(TdsEnums.HARDFLUSH);
                    if (writePacketTask == null)
                    {
                        HasPendingData = true;
                        _messageStatus = 0;
                        return null;
                    }
                    else
                    {
                        return AsyncHelper.CreateContinuationTaskWithState(
                            task: writePacketTask,
                            state: this,
                            onSuccess: static (object state) =>
                            {
                                TdsParserStateObject stateObject = (TdsParserStateObject)state;
                                stateObject.HasPendingData = true;
                                stateObject._messageStatus = 0;
                            }
                        );
                    }
                }
            }
        }

        // Processes the tds header that is present in the buffer
        internal TdsOperationStatus TryProcessHeader()
        {
            Debug.Assert(_inBytesPacket == 0, "there should not be any bytes left in packet when ReadHeader is called");

            // if the header splits buffer reads - special case!
            if ((_partialHeaderBytesRead > 0) || (_inBytesUsed + _inputHeaderLen > _inBytesRead))
            {
                // VSTS 219884: when some kind of MITM (man-in-the-middle) tool splits the network packets, the message header can be split over
                // several network packets.
                // Note: cannot use ReadByteArray here since it uses _inBytesPacket which is not set yet.
                do
                {
                    int copy = Math.Min(_inBytesRead - _inBytesUsed, _inputHeaderLen - _partialHeaderBytesRead);
                    Debug.Assert(copy > 0, "ReadNetworkPacket read empty buffer");

                    Buffer.BlockCopy(_inBuff, _inBytesUsed, _partialHeaderBuffer, _partialHeaderBytesRead, copy);
                    _partialHeaderBytesRead += copy;
                    _inBytesUsed += copy;

                    Debug.Assert(_partialHeaderBytesRead <= _inputHeaderLen, "Read more bytes for header than required");
                    if (_partialHeaderBytesRead == _inputHeaderLen)
                    {
                        // All read
                        ReadOnlySpan<byte> header = _partialHeaderBuffer.AsSpan(0, TdsEnums.HEADER_LEN);
                        _partialHeaderBytesRead = 0;
                        _messageStatus = Packet.GetStatusFromHeader(header);
                        _inBytesPacket = Packet.GetDataLengthFromHeader(header);
                        _spid = Packet.GetSpidFromHeader(header);

                        SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObject.TryProcessHeader | ADV | State Object Id {0}, Client Connection Id {1}, Server process Id (SPID) {2}", _objectID, _parser?.Connection?.ClientConnectionId, _spid);
                    }
                    else
                    {
                        Debug.Assert(_inBytesUsed == _inBytesRead, "Did not use all data while reading partial header");

                        // Require more data
                        if (_parser.State == TdsParserState.Broken || _parser.State == TdsParserState.Closed)
                        {
                            // NOTE: ReadNetworkPacket does nothing if the parser state is closed or broken
                            // to avoid infinite loop, we raise an exception
                            ThrowExceptionAndWarning();
                            return TdsOperationStatus.Done;
                        }

                        TdsOperationStatus result = TryReadNetworkPacket();
                        if (result != TdsOperationStatus.Done)
                        {
                            return result;
                        }

                        if (IsTimeoutStateExpired)
                        {
                            ThrowExceptionAndWarning();
                            return TdsOperationStatus.Done;
                        }
                    }
                } while (_partialHeaderBytesRead != 0); // This is reset to 0 once we have read everything that we need

                AssertValidState();
            }
            else
            {
                // normal header processing...
                ReadOnlySpan<byte> header = _inBuff.AsSpan(_inBytesUsed, TdsEnums.HEADER_LEN);
                _messageStatus = Packet.GetStatusFromHeader(header);
                _inBytesPacket = Packet.GetDataLengthFromHeader(header);
                _spid = Packet.GetSpidFromHeader(header);
#if NET
                SqlClientEventSource.Log.TryAdvancedTraceEvent("TdsParserStateObject.TryProcessHeader | ADV | State Object Id {0}, Client Connection Id {1}, Server process Id (SPID) {2}", _objectID, _parser?.Connection?.ClientConnectionId, _spid);
#endif
                _inBytesUsed += _inputHeaderLen;

                AssertValidState();
            }

            if (_inBytesPacket < 0)
            {
#if NETFRAMEWORK
                throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
#else
                // either TDS stream is corrupted or there is multithreaded misuse of connection
                throw SQL.ParsingError();
#endif
            }

            return TdsOperationStatus.Done;
        }

        internal void SetBuffer(byte[] buffer, int inBytesUsed, int inBytesRead/*, [CallerMemberName] string caller = null*/)
        {
            _inBuff = buffer;
            _inBytesUsed = inBytesUsed;
            _inBytesRead = inBytesRead;
        }

        // This ensure that there is data available to be read in the buffer and that the header has been processed
        // NOTE: This method (and all it calls) should be retryable without replaying a snapshot
        internal TdsOperationStatus TryPrepareBuffer()
        {
            Debug.Assert(_inBuff != null, "packet buffer should not be null!");

            // Header spans packets, or we haven't read the header yet - process header
            if ((_inBytesPacket == 0) && (_inBytesUsed < _inBytesRead))
            {
                TdsOperationStatus result = TryProcessHeader();
                if (result != TdsOperationStatus.Done)
                {
                    return result;
                }
                Debug.Assert(_inBytesPacket != 0, "_inBytesPacket cannot be 0 after processing header!");
                AssertValidState();
            }

            // If we're out of data, need to read more
            if (_inBytesUsed == _inBytesRead)
            {
                // If the _inBytesPacket is not zero, then we have data left in the packet, but the data in the packet
                // spans the buffer, so we can read any amount of data possible, and we do not need to call ProcessHeader
                // because there isn't a header at the beginning of the data that we are reading.
                if (_inBytesPacket > 0)
                {
                    TdsOperationStatus result = TryReadNetworkPacket();
                    if (result != TdsOperationStatus.Done)
                    {
                        return result;
                    }
                }
                else if (_inBytesPacket == 0)
                {
                    // Else we have finished the packet and so we must read as much data as possible
                    TdsOperationStatus result = TryReadNetworkPacket();
                    if (result != TdsOperationStatus.Done)
                    {
                        return result;
                    }

                    result = TryProcessHeader();
                    if (result != TdsOperationStatus.Done)
                    {
                        return result;
                    }

                    Debug.Assert(_inBytesPacket != 0, "_inBytesPacket cannot be 0 after processing header!");
                    if (_inBytesUsed == _inBytesRead)
                    {
                        // we read a header but didn't get anything else except it
                        // VSTS 219884: it can happen that the TDS packet header and its data are split across two network packets.
                        // Read at least one more byte to get/cache the first data portion of this TDS packet
                        result = TryReadNetworkPacket();
                        if (result != TdsOperationStatus.Done)
                        {
                            return result;
                        }
                    }
                }
                else
                {
                    Debug.Fail("entered negative _inBytesPacket loop");
                }
                AssertValidState();
            }

            return TdsOperationStatus.Done;
        }

        internal void ResetBuffer()
        {
            _outBytesUsed = _outputHeaderLen;
        }

        internal void ResetPacketCounters()
        {
            _outputPacketNumber = 1;
            _outputPacketCount = 0;
        }

        internal bool SetPacketSize(int size)
        {
            if (size > TdsEnums.MAX_PACKET_SIZE)
            {
                throw SQL.InvalidPacketSize();
            }
            Debug.Assert(size >= 1, "Cannot set packet size to less than 1.");
            Debug.Assert((_outBuff == null && _inBuff == null) ||
                          (_outBuff.Length == _inBuff.Length),
                          "Buffers are not in consistent state");
            Debug.Assert((_outBuff == null && _inBuff == null) ||
                          this == _parser._physicalStateObj,
                          "SetPacketSize should only be called on a stateObj with null buffers on the physicalStateObj!");
            Debug.Assert(_inBuff == null
                          ||
                          (
                           _outBytesUsed == (_outputHeaderLen + BitConverter.ToInt32(_outBuff, _outputHeaderLen)) &&
                           _outputPacketNumber == 1)
                          ||
                          (_outBytesUsed == _outputHeaderLen && _outputPacketNumber == 1),
                          "SetPacketSize called with data in the buffer!");

            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | State Object Id {2}, Setting packet size to {3}",
                nameof(TdsParserStateObject), nameof(SetPacketSize), _objectID, size);

            if (_inBuff == null || _inBuff.Length != size)
            { // We only check _inBuff, since two buffers should be consistent.
                // Allocate or re-allocate _inBuff.
                if (_inBuff == null)
                {
                    SetBuffer(new byte[size], 0, 0);
                }
                else if (size != _inBuff.Length)
                {
                    // If new size is other than existing...
                    if (_inBytesRead > _inBytesUsed)
                    {
                        // if we still have data left in the buffer we must keep that array reference and then copy into new one
                        byte[] temp = _inBuff;

                        // copy remainder of unused data
                        int remainingData = _inBytesRead - _inBytesUsed;
                        if ((temp.Length < _inBytesUsed + remainingData) || (size < remainingData))
                        {
                            string errormessage = StringsHelper.GetString(Strings.SQL_InvalidInternalPacketSize) + ' ' + temp.Length + ", " + _inBytesUsed + ", " + remainingData + ", " + size;
                            throw SQL.InvalidInternalPacketSize(errormessage);
                        }

                        byte[] inBuff = new byte[size];
                        Buffer.BlockCopy(temp, _inBytesUsed, inBuff, 0, remainingData);
                        SetBuffer(inBuff, 0, remainingData);

                        AssertValidState();
                    }
                    else
                    {
                        // buffer is empty - just create the new one that is double the size of the old one
                        SetBuffer(new byte[size], 0, 0);
                    }
                }

                // Always re-allocate _outBuff - assert is above to verify state.
                _outBuff = new byte[size];
                _outBytesUsed = _outputHeaderLen;

                AssertValidState();
                return true;
            }

            return false;
        }

        ///////////////////////////////////////
        // Buffer read methods - data values //
        ///////////////////////////////////////

        // look at the next byte without pulling it off the wire, don't just return _inBytesUsed since we may
        // have to go to the network to get the next byte.
        internal TdsOperationStatus TryPeekByte(out byte value)
        {
            TdsOperationStatus result = TryReadByte(out value);
            if (result != TdsOperationStatus.Done)
            {
                return result;
            }

            // now do fixup
            _inBytesPacket++;
            _inBytesUsed--;

            AssertValidState();
            return TdsOperationStatus.Done;
        }

        // Takes a byte array, an offset, and a len and fills the array from the offset to len number of
        // bytes from the in buffer.
        public TdsOperationStatus TryReadByteArray(Span<byte> buff, int len)
        {
            return TryReadByteArray(buff, len, out _, 0, false);
        }

        public TdsOperationStatus TryReadByteArray(Span<byte> buff, int len, out int totalRead)
        {
            return TryReadByteArray(buff, len, out totalRead, 0, false);
        }

        // NOTE: This method must be retriable WITHOUT replaying a snapshot
        // Every time you call this method increment the offset and decrease len by the value of totalRead
        public TdsOperationStatus TryReadByteArray(Span<byte> buff, int len, out int totalRead, int startOffset, bool writeDataSizeToSnapshot)
        {
            totalRead = 0;

#if DEBUG
            if (_snapshot != null && _snapshot.DoPend())
            {
                _networkPacketTaskSource = new TaskCompletionSource<object>();
                Interlocked.MemoryBarrier();

                if (s_forcePendingReadsToWaitForUser)
                {
                    _realNetworkPacketTaskSource = new TaskCompletionSource<object>();
                    _realNetworkPacketTaskSource.SetResult(null);
                }
                else
                {
                    _networkPacketTaskSource.TrySetResult(null);
                }
                return TdsOperationStatus.InvalidData;
            }
#endif

            Debug.Assert(buff.IsEmpty || buff.Length >= len, "Invalid length sent to ReadByteArray()!");


            totalRead += startOffset;
            len -= startOffset;

            // loop through and read up to array length
            while (len > 0)
            {
                if ((_inBytesPacket == 0) || (_inBytesUsed == _inBytesRead))
                {
                    TdsOperationStatus result = TryPrepareBuffer();
                    if (result != TdsOperationStatus.Done)
                    {
                        return result;
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

                if (writeDataSizeToSnapshot)
                {
                    SetSnapshotDataSize(bytesToRead);
                }

                AssertValidState();
            }

            return TdsOperationStatus.Done;
        }

        // Takes no arguments and returns a byte from the buffer.  If the buffer is empty, it is filled
        // before the byte is returned.
        internal TdsOperationStatus TryReadByte(out byte value)
        {
            Debug.Assert(_inBytesUsed >= 0 && _inBytesUsed <= _inBytesRead, "ERROR - TDSParser: _inBytesUsed < 0 or _inBytesUsed > _inBytesRead");
            value = 0;

#if DEBUG
            if (_snapshot != null && _snapshot.DoPend())
            {
                _networkPacketTaskSource = new TaskCompletionSource<object>();
                Interlocked.MemoryBarrier();

                if (s_forcePendingReadsToWaitForUser)
                {
                    _realNetworkPacketTaskSource = new TaskCompletionSource<object>();
                    _realNetworkPacketTaskSource.SetResult(null);
                }
                else
                {
                    _networkPacketTaskSource.TrySetResult(null);
                }
                return TdsOperationStatus.InvalidData;
            }
#endif

            if ((_inBytesPacket == 0) || (_inBytesUsed == _inBytesRead))
            {
                TdsOperationStatus result = TryPrepareBuffer();
                if (result != TdsOperationStatus.Done)
                {
                    return result;
                }
            }

            // decrement the number of bytes left in the packet
            _inBytesPacket--;

            Debug.Assert(_inBytesPacket >= 0, "ERROR - TDSParser: _inBytesPacket < 0");

            // return the byte from the buffer and increment the counter for number of bytes used in the in buffer
            value = (_inBuff[_inBytesUsed++]);

            AssertValidState();
            return TdsOperationStatus.Done;
        }

        internal TdsOperationStatus TryReadChar(out char value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");

            Span<byte> buffer = stackalloc byte[2];
            if (((_inBytesUsed + 2) > _inBytesRead) || (_inBytesPacket < 2))
            {
                // If the char isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.
                TdsOperationStatus result = TryReadByteArray(buffer, 2);
                if (result != TdsOperationStatus.Done)
                {
                    value = '\0';
                    return result;
                }
            }
            else
            {
                // The entire char is in the packet and in the buffer, so just return it
                // and take care of the counters.
                buffer = _inBuff.AsSpan(_inBytesUsed, 2);
                _inBytesUsed += 2;
                _inBytesPacket -= 2;
            }

            AssertValidState();
            value = (char)((buffer[1] << 8) + buffer[0]);

            return TdsOperationStatus.Done;
        }

        internal TdsOperationStatus TryReadInt16(out short value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");

            Span<byte> buffer = stackalloc byte[2];
            if (((_inBytesUsed + 2) > _inBytesRead) || (_inBytesPacket < 2))
            {
                // If the int16 isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.
                TdsOperationStatus result = TryReadByteArray(buffer, 2);
                if (result != TdsOperationStatus.Done)
                {
                    value = default;
                    return result;
                }
            }
            else
            {
                // The entire int16 is in the packet and in the buffer, so just return it
                // and take care of the counters.
                buffer = _inBuff.AsSpan(_inBytesUsed, 2);
                _inBytesUsed += 2;
                _inBytesPacket -= 2;
            }

            AssertValidState();
            value = (short)((buffer[1] << 8) + buffer[0]);
            return TdsOperationStatus.Done;
        }

        internal TdsOperationStatus TryReadInt32(out int value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            Span<byte> buffer = stackalloc byte[4];
            if (((_inBytesUsed + 4) > _inBytesRead) || (_inBytesPacket < 4))
            {
                // If the int isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.
                TdsOperationStatus result = TryReadByteArray(buffer, 4);
                if (result != TdsOperationStatus.Done)
                {
                    value = 0;
                    return result;
                }
            }
            else
            {
                // The entire int is in the packet and in the buffer, so just return it
                // and take care of the counters.
                buffer = _inBuff.AsSpan(_inBytesUsed, 4);
                _inBytesUsed += 4;
                _inBytesPacket -= 4;
            }

            AssertValidState();
            value = (buffer[3] << 24) + (buffer[2] << 16) + (buffer[1] << 8) + buffer[0];
            return TdsOperationStatus.Done;
        }

        // This method is safe to call when doing async without snapshot
        internal TdsOperationStatus TryReadInt64(out long value)
        {
            if ((_inBytesPacket == 0) || (_inBytesUsed == _inBytesRead))
            {
                TdsOperationStatus result = TryPrepareBuffer();
                if (result != TdsOperationStatus.Done)
                {
                    value = 0;
                    return result;
                }
            }

            if ((_bTmpRead > 0) || (((_inBytesUsed + 8) > _inBytesRead) || (_inBytesPacket < 8)))
            {
                // If the long isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                int bytesRead;
                TdsOperationStatus result = TryReadByteArray(_bTmp.AsSpan(start: _bTmpRead), 8 - _bTmpRead, out bytesRead);
                if (result != TdsOperationStatus.Done)
                {
                    Debug.Assert(_bTmpRead + bytesRead <= 8, "Read more data than required");
                    _bTmpRead += bytesRead;
                    value = 0;
                    return result;
                }
                else
                {
                    Debug.Assert(_bTmpRead + bytesRead == 8, "TryReadByteArray returned true without reading all data required");
                    _bTmpRead = 0;
                    AssertValidState();
                    value = BinaryPrimitives.ReadInt64LittleEndian(_bTmp);
                    return TdsOperationStatus.Done;
                }
            }
            else
            {
                // The entire long is in the packet and in the buffer, so just return it
                // and take care of the counters.

                value = BinaryPrimitives.ReadInt64LittleEndian(_inBuff.AsSpan(_inBytesUsed));

                _inBytesUsed += 8;
                _inBytesPacket -= 8;

                AssertValidState();
                return TdsOperationStatus.Done;
            }
        }

        internal TdsOperationStatus TryReadUInt16(out ushort value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");

            Span<byte> buffer = stackalloc byte[2];
            if (((_inBytesUsed + 2) > _inBytesRead) || (_inBytesPacket < 2))
            {
                // If the uint16 isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.
                TdsOperationStatus result = TryReadByteArray(buffer, 2);
                if (result != TdsOperationStatus.Done)
                {
                    value = default;
                    return result;
                }
            }
            else
            {
                // The entire uint16 is in the packet and in the buffer, so just return it
                // and take care of the counters.
                buffer = _inBuff.AsSpan(_inBytesUsed, 2);
                _inBytesUsed += 2;
                _inBytesPacket -= 2;
            }

            AssertValidState();
            value = (ushort)((buffer[1] << 8) + buffer[0]);
            return TdsOperationStatus.Done;
        }

        // This method is safe to call when doing async without replay
        internal TdsOperationStatus TryReadUInt32(out uint value)
        {
            if ((_inBytesPacket == 0) || (_inBytesUsed == _inBytesRead))
            {
                TdsOperationStatus result = TryPrepareBuffer();
                if (result != TdsOperationStatus.Done)
                {
                    value = 0;
                    return result;
                }
            }

            if ((_bTmpRead > 0) || (((_inBytesUsed + 4) > _inBytesRead) || (_inBytesPacket < 4)))
            {
                // If the int isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                int bytesRead;
                TdsOperationStatus result = TryReadByteArray(_bTmp.AsSpan(start: _bTmpRead), 4 - _bTmpRead, out bytesRead);
                if (result != TdsOperationStatus.Done)
                {
                    Debug.Assert(_bTmpRead + bytesRead <= 4, "Read more data than required");
                    _bTmpRead += bytesRead;
                    value = 0;
                    return result;
                }
                else
                {
                    Debug.Assert(_bTmpRead + bytesRead == 4, "TryReadByteArray returned true without reading all data required");
                    _bTmpRead = 0;
                    AssertValidState();
                    value = BinaryPrimitives.ReadUInt32LittleEndian(_bTmp);
                    return TdsOperationStatus.Done;
                }
            }
            else
            {
                // The entire int is in the packet and in the buffer, so just return it
                // and take care of the counters.

                value = BinaryPrimitives.ReadUInt32LittleEndian(_inBuff.AsSpan(_inBytesUsed));

                _inBytesUsed += 4;
                _inBytesPacket -= 4;

                AssertValidState();
                return TdsOperationStatus.Done;
            }
        }

        internal TdsOperationStatus TryReadSingle(out float value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            if (((_inBytesUsed + 4) > _inBytesRead) || (_inBytesPacket < 4))
            {
                // If the float isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                TdsOperationStatus result = TryReadByteArray(_bTmp, 4);
                if (result != TdsOperationStatus.Done)
                {
                    value = default;
                    return result;
                }

                AssertValidState();
                value = BitConverterCompatible.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(_bTmp));
                return TdsOperationStatus.Done;
            }
            else
            {
                // The entire float is in the packet and in the buffer, so just return it
                // and take care of the counters.

                value = BitConverterCompatible.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(_inBuff.AsSpan(_inBytesUsed)));

                _inBytesUsed += 4;
                _inBytesPacket -= 4;

                AssertValidState();
                return TdsOperationStatus.Done;
            }
        }

        internal TdsOperationStatus TryReadDouble(out double value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            if (((_inBytesUsed + 8) > _inBytesRead) || (_inBytesPacket < 8))
            {
                // If the double isn't fully in the buffer, or if it isn't fully in the packet,
                // then use ReadByteArray since the logic is there to take care of that.

                TdsOperationStatus result = TryReadByteArray(_bTmp, 8);
                if (result != TdsOperationStatus.Done)
                {
                    value = default;
                    return result;
                }

                AssertValidState();
                value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(_bTmp));
                return TdsOperationStatus.Done;
            }
            else
            {
                // The entire double is in the packet and in the buffer, so just return it
                // and take care of the counters.

                value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(_inBuff.AsSpan(_inBytesUsed)));

                _inBytesUsed += 8;
                _inBytesPacket -= 8;

                AssertValidState();
                return TdsOperationStatus.Done;
            }
        }

        internal TdsOperationStatus TryReadString(int length, out string value)
        {
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

                TdsOperationStatus result = TryReadByteArray(_bTmp, cBytes);
                if (result != TdsOperationStatus.Done)
                {
                    value = null;
                    return result;
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
            return TdsOperationStatus.Done;
        }

        internal TdsOperationStatus TryReadStringWithEncoding(int length, System.Text.Encoding encoding, bool isPlp, out string value)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");

            if (encoding == null)
            {
                // Need to skip the current column before throwing the error - this ensures that the state shared between this and the data reader is consistent when calling DrainData
                if (isPlp)
                {
                    TdsOperationStatus result = _parser.TrySkipPlpValue((ulong)length, this, out _);
                    if (result != TdsOperationStatus.Done)
                    {
                        value = null;
                        return result;
                    }
                }
                else
                {
                    TdsOperationStatus result = TrySkipBytes(length);
                    if (result != TdsOperationStatus.Done)
                    {
                        value = null;
                        return result;
                    }
                }

                _parser.ThrowUnsupportedCollationEncountered(this);
            }
            byte[] buf = null;
            int offset = 0;
            (bool isAvailable, bool isStarting, bool isContinuing) = GetSnapshotStatuses();

            if (isPlp)
            {
                TdsOperationStatus result = TryReadPlpBytes(ref buf, 0, int.MaxValue, out length);
                if (result != TdsOperationStatus.Done)
                {
                    value = null;
                    return result;
                }

                AssertValidState();
            }
            else
            {
                if (((_inBytesUsed + length) > _inBytesRead) || (_inBytesPacket < length))
                {
                    int startOffset = 0;
                    if (isAvailable)
                    {
                        if (isContinuing || isStarting)
                        {
                            buf = TryTakeSnapshotStorage() as byte[];
                            Debug.Assert(buf == null || buf.Length == length, "stored buffer length must be null or must have been created with the correct length");
                        }
                        if (buf != null)
                        {
                            startOffset = GetSnapshotTotalSize();
                        }
                    }

                    if (buf == null || buf.Length < length)
                    {
                        buf = new byte[length];
                    }

                    TdsOperationStatus result = TryReadByteArray(buf, length, out _, startOffset, isAvailable);
                    
                    if (result != TdsOperationStatus.Done)
                    {
                        if (result == TdsOperationStatus.NeedMoreData)
                        {
                            if (isStarting || isContinuing)
                            {
                                SetSnapshotStorage(buf);
                            }
                        }
                        value = null;
                        return result;
                    }

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
            return TdsOperationStatus.Done;
        }

        internal ulong ReadPlpLength(bool returnPlpNullIfNull)
        {
            ulong value;
            Debug.Assert(_syncOverAsync, "Should not attempt pends in a synchronous call");
            TdsOperationStatus result = TryReadPlpLength(returnPlpNullIfNull, out value);
            if (result != TdsOperationStatus.Done)
            {
                throw SQL.SynchronousCallMayNotPend();
            }
            return value;
        }

        // Reads the length of either the entire data or the length of the next chunk in a
        //   partially length prefixed data
        // After this call, call  ReadPlpBytes/ReadPlpUnicodeChars until the specified length of data
        // is consumed. Repeat this until ReadPlpLength returns 0 in order to read the
        // entire stream.
        // When this function returns 0, it means the data stream is read completely and the
        // plp state in the tdsparser is cleaned.
        internal TdsOperationStatus TryReadPlpLength(bool returnPlpNullIfNull, out ulong lengthLeft)
        {
            uint chunklen;
            // bool firstchunk = false;
            bool isNull = false;

            Debug.Assert(_longlenleft == 0, "Out of synch length read request");
            if (_longlen == 0)
            {
                // First chunk is being read. Find out what type of chunk it is
                long value;
                TdsOperationStatus result = TryReadInt64(out value);
                if (result != TdsOperationStatus.Done)
                {
                    lengthLeft = 0;
                    return result;
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
                TdsOperationStatus result = TryReadUInt32(out chunklen);
                if (result != TdsOperationStatus.Done)
                {
                    lengthLeft = 0;
                    return result;
                }
                if (chunklen == TdsEnums.SQL_PLP_CHUNK_TERMINATOR)
                {
                    _longlenleft = 0;
                    _longlen = 0;
                }
                else
                {
                    _longlenleft = chunklen;
                }
            }

            AssertValidState();

            if (isNull && returnPlpNullIfNull)
            {
                lengthLeft = TdsEnums.SQL_PLP_NULL;
                return TdsOperationStatus.Done;
            }

            lengthLeft = _longlenleft;
            return TdsOperationStatus.Done;
        }

        internal int ReadPlpBytesChunk(byte[] buff, int offset, int len)
        {
            Debug.Assert(_syncOverAsync, "Should not attempt pends in a synchronous call");
            Debug.Assert(_longlenleft > 0, "Read when no data available");

            int value;
            int bytesToRead = (int)Math.Min(_longlenleft, (ulong)len);
            TdsOperationStatus result = TryReadByteArray(buff.AsSpan(offset), bytesToRead, out value);
            _longlenleft -= (ulong)bytesToRead;
            if (result != TdsOperationStatus.Done)
            {
                throw SQL.SynchronousCallMayNotPend();
            }
            return value;
        }

        internal TdsOperationStatus TryReadPlpBytes(ref byte[] buff, int offset, int len, out int totalBytesRead)
        {
            bool isStarting = false;
            bool isContinuing = false;
            bool compatibilityMode = LocalAppContextSwitches.UseCompatibilityAsyncBehaviour;
            if (!compatibilityMode)
            {
                (_, isStarting, isContinuing) = GetSnapshotStatuses();
            }
            return TryReadPlpBytes(ref buff, offset, len, out totalBytesRead, isStarting || isContinuing, compatibilityMode);
        }
        // Reads the requested number of bytes from a plp data stream, or the entire data if
        // requested length is -1 or larger than the actual length of data. First call to this method
        //  should be preceeded by a call to ReadPlpLength or ReadDataLength.
        // Returns the actual bytes read.
        // NOTE: This method must be retriable WITHOUT replaying a snapshot
        // Every time you call this method increment the offset and decrease len by the value of totalBytesRead
        internal TdsOperationStatus TryReadPlpBytes(ref byte[] buff, int offset, int len, out int totalBytesRead, bool writeDataSizeToSnapshot, bool compatibilityMode)
        {
            totalBytesRead = 0;

            if (_longlen == 0)
            {
                Debug.Assert(_longlenleft == 0);
                if (buff == null)
                {
                    buff = Array.Empty<byte>();
                }

                AssertValidState();
                totalBytesRead = 0;
                return TdsOperationStatus.Done;       // No data
            }

            Debug.Assert(_longlen != TdsEnums.SQL_PLP_NULL, "Out of sync plp read request");
            Debug.Assert((buff == null && offset == 0) || (buff.Length >= offset + len), "Invalid length sent to ReadPlpBytes()!");

            int bytesLeft = len;

            // If total length is known up front, allocate the whole buffer in one shot instead of realloc'ing and copying over each time
            if (buff == null && _longlen != TdsEnums.SQL_PLP_UNKNOWNLEN)
            {
                if (writeDataSizeToSnapshot)
                {
                    // if there is a snapshot and it contains a stored plp buffer take it
                    // and try to use it if it is the right length
                    buff = TryTakeSnapshotStorage() as byte[];
                    if (buff != null)
                    {
                        offset = _snapshot.GetPacketDataOffset();
                        totalBytesRead = offset;
                    }
                }
                else if (compatibilityMode && _snapshot != null && _snapshotStatus != SnapshotStatus.NotActive)
                {
                    // legacy replay path perf optimization
                    // if there is a snapshot and it contains a stored plp buffer take it
                    // and try to use it if it is the right length
                    buff = TryTakeSnapshotStorage() as byte[];
                }

                if ((ulong)(buff?.Length ?? 0) != _longlen)
                {
                    // if the buffer is null or the wrong length create one to use
                    buff = new byte[(Math.Min((int)_longlen, len))];
                }
            }

            if (_longlenleft == 0)
            {
                TdsOperationStatus result = TryReadPlpLength(false, out _);
                if (result != TdsOperationStatus.Done)
                {
                    totalBytesRead = 0;
                    return result;
                }
                if (_longlenleft == 0)
                {
                    // Data read complete
                    totalBytesRead = 0;
                    return TdsOperationStatus.Done;
                }
            }

            if (buff == null)
            {
                buff = new byte[_longlenleft];
            }

            while (bytesLeft > 0)
            {
                int bytesToRead = (int)Math.Min(_longlenleft, (ulong)bytesLeft);
                if (buff.Length < (offset + bytesToRead))
                {
                    // Grow the array
                    byte[] newbuf = new byte[offset + bytesToRead];
                    Buffer.BlockCopy(buff, 0, newbuf, 0, offset);
                    buff = newbuf;
                    newbuf = null;
                }

                TdsOperationStatus result = TryReadByteArray(buff.AsSpan(offset), bytesToRead, out int bytesRead);
                Debug.Assert(bytesRead <= bytesLeft, "Read more bytes than we needed");
                Debug.Assert((ulong)bytesRead <= _longlenleft, "Read more bytes than is available");

                bytesLeft -= bytesRead;
                offset += bytesRead;
                totalBytesRead += bytesRead;
                _longlenleft -= (ulong)bytesRead;
                if (result != TdsOperationStatus.Done)
                {
                    if (writeDataSizeToSnapshot)
                    {
                        // a partial read has happened so store the target buffer in the snapshot
                        // so it can be re-used when another packet arrives and we read again
                        SetSnapshotStorage(buff);
                        SetSnapshotDataSize(bytesRead);

                    }
                    else if (compatibilityMode && _snapshot != null)
                    {
                        // legacy replay path perf optimization
                        // a partial read has happened so store the target buffer in the snapshot
                        // so it can be re-used when another packet arrives and we read again
                        SetSnapshotStorage(buff);
                    }
                    return result;
                }

                if (_longlenleft == 0)
                {
                    // Read the next chunk or cleanup state if hit the end
                    result = TryReadPlpLength(false, out _);
                    if (result != TdsOperationStatus.Done)
                    {
                        if (writeDataSizeToSnapshot)
                        {
                            if (result == TdsOperationStatus.NeedMoreData)
                            {
                                SetSnapshotStorage(buff);
                                SetSnapshotDataSize(bytesRead);
                            }
                        } 
                        else if (compatibilityMode && _snapshot != null)
                        {
                            // a partial read has happened so store the target buffer in the snapshot
                            // so it can be re-used when another packet arrives and we read again
                            SetSnapshotStorage(buff);
                        }
                        return result;
                    }
                }

                AssertValidState();

                // Catch the point where we read the entire plp data stream and clean up state
                if (_longlenleft == 0)   // Data read complete
                {
                    break;
                }
            }
            return TdsOperationStatus.Done;
        }

        /////////////////////////////////////////
        // Value Skip Logic                    //
        /////////////////////////////////////////

        // Reads bytes from the buffer but doesn't return them, in effect simply deleting them.
        // Does not handle plp fields, need to use SkipPlpBytesValue for those.
        // Does not handle null values or NBC bitmask, ensure the value is not null before calling this method
        internal TdsOperationStatus TrySkipLongBytes(long num)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");

            while (num > 0)
            {
                int cbSkip = (int)Math.Min(int.MaxValue, num);
                TdsOperationStatus result = TryReadByteArray(Span<byte>.Empty, cbSkip);
                if (result != TdsOperationStatus.Done)
                {
                    return result;
                }
                num -= cbSkip;
            }

            return TdsOperationStatus.Done;
        }

        // Reads bytes from the buffer but doesn't return them, in effect simply deleting them.
        internal TdsOperationStatus TrySkipBytes(int num)
        {
            Debug.Assert(_syncOverAsync || !_asyncReadWithoutSnapshot, "This method is not safe to call when doing sync over async");
            return TryReadByteArray(Span<byte>.Empty, num);
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

        private static void ReadAsyncCallbackComplete(object state)
        {
            TaskCompletionSource<object> source = (TaskCompletionSource<object>)state;
            source.TrySetResult(null);
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

        /////////////////////////////////////////
        // Network/Packet Reading & Processing //
        /////////////////////////////////////////

#if DEBUG
        private string _lastStack;
#endif

        internal TdsOperationStatus TryReadNetworkPacket()
        {
#if DEBUG
            Debug.Assert(!_shouldHaveEnoughData || _attentionSent, "Caller said there should be enough data, but we are currently reading a packet");
#endif
            TdsOperationStatus result = TdsOperationStatus.InvalidData;
            if (_snapshot != null)
            {
                if (_snapshotStatus != SnapshotStatus.NotActive)
                {
#if DEBUG
                    string stackTrace = null;
                    if (s_checkNetworkPacketRetryStacks)
                    {
                        // in debug builds stack traces contain line numbers so if we want to be
                        // able to compare the stack traces they must all be created in the same
                        // location in the code
                        stackTrace = Environment.StackTrace;
                    }
#endif
                    bool capturedAsContinue = false;
                    if (_snapshotStatus == SnapshotStatus.ReplayRunning || _snapshotStatus == SnapshotStatus.ReplayStarting)
                    {
                        if (_bTmpRead == 0 && _partialHeaderBytesRead == 0 && _longlenleft == 0 && _snapshot.ContinueEnabled)
                        {
                            // no temp between packets
                            // mark this point as continue-able
                            _snapshot.CaptureAsContinue(this);
                            capturedAsContinue = true;
                        }
                    }

                    if (_snapshot.MoveNext())
                    {
#if DEBUG
                        if (s_checkNetworkPacketRetryStacks)
                        {
                            _snapshot.CheckStack(stackTrace);
                        }
#endif
                        return TdsOperationStatus.Done;
                    }
                    else
                    {
#if DEBUG
                        if (s_checkNetworkPacketRetryStacks)
                        {
                            _lastStack = stackTrace;
                        }
#endif
                        if (_bTmpRead == 0 && _partialHeaderBytesRead == 0 && _longlenleft == 0 && _snapshot.ContinueEnabled && !capturedAsContinue)
                        {
                            // no temp between packets
                            // mark this point as continue-able
                            _snapshot.CaptureAsContinue(this);
                            capturedAsContinue = true;
                        }
                    }
                }

                // previous buffer is in snapshot
                _inBuff = new byte[_inBuff.Length];
                result = TdsOperationStatus.NeedMoreData;
            }

            if (result == TdsOperationStatus.InvalidData && PartialPacket != null && !PartialPacket.ContainsCompletePacket)
            {
                result = TdsOperationStatus.NeedMoreData;
            }

            if (_syncOverAsync)
            {
                ReadSniSyncOverAsync();
                while (_inBytesRead == 0)
                {
                    // a partial packet must have taken the packet data so we
                    // need to read more data to complete the packet, but we 
                    // can't return NeedMoreData in sync mode so we have to
                    // spin fetching more data here until we have something
                    // that the caller can read
                    ReadSniSyncOverAsync();
                }
                return TdsOperationStatus.Done;
            }

            ReadSni(new TaskCompletionSource<object>());

#if DEBUG
            if (s_failAsyncPends)
            {
                throw new InvalidOperationException("Attempted to pend a read when s_failAsyncPends test hook was enabled");
            }
            if (s_forceSyncOverAsyncAfterFirstPend)
            {
                _syncOverAsync = true;
            }
#endif
            Debug.Assert((_snapshot != null) ^ _asyncReadWithoutSnapshot, "Must have either _snapshot set up or _asyncReadWithoutSnapshot enabled (but not both) to pend a read");

            return result;
        }

        internal void PrepareReplaySnapshot()
        {
            _networkPacketTaskSource = null;
            if (!_snapshot.MoveToContinue())
            {
                _snapshot.MoveToStart();
            }
        }

        internal void ReadSniSyncOverAsync()
        {
            if (_parser.State == TdsParserState.Broken || _parser.State == TdsParserState.Closed)
            {
                throw ADP.ClosedConnectionError();
            }

            PacketHandle readPacket = default;
            bool readFromNetwork = !PartialPacketContainsCompletePacket();
            uint error;

            RuntimeHelpers.PrepareConstrainedRegions();
            bool shouldDecrement = false;
            try
            {
                Interlocked.Increment(ref _readingCount);
                shouldDecrement = true;

                if (readFromNetwork)
                {
                    readPacket = ReadSyncOverAsync(GetTimeoutRemaining(), out error);
                }
                else
                {
                    error = TdsEnums.SNI_SUCCESS;
                }

                Interlocked.Decrement(ref _readingCount);
                shouldDecrement = false;

                if (_parser.MARSOn)
                { // Only take reset lock on MARS and Async.
                    CheckSetResetConnectionState(error, CallbackType.Read);
                }

                if (TdsEnums.SNI_SUCCESS == error)
                {
                    // Success - process results!

                    if (readFromNetwork)
                    {
                        Debug.Assert(!IsPacketEmpty(readPacket), "ReadNetworkPacket cannot be null in synchronous operation!");
                    }

                    ProcessSniPacket(readPacket, TdsEnums.SNI_SUCCESS);
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
                {
                    // Failure!
                    if (readFromNetwork)
                    {
                        Debug.Assert(!IsValidPacket(readPacket), "unexpected readPacket without corresponding SNIPacketRelease");
                    }

                    ReadSniError(this, error);
                }
            }
            finally
            {
                if (shouldDecrement)
                {
                    Interlocked.Decrement(ref _readingCount);
                }

                if (readFromNetwork)
                {
                    if (!IsPacketEmpty(readPacket))
                    {
                        ReleasePacket(readPacket);
                    }
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
            Interlocked.MemoryBarrier();

            // then check for networkPacketTaskSource
            TaskCompletionSource<object> taskSource = _networkPacketTaskSource;
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

        private bool OnTimeoutSync(bool asyncClose = false)
        {
            return OnTimeoutCore(TimeoutState.Running, TimeoutState.ExpiredSync, asyncClose);
        }

        /// <summary>
        /// attempts to change the timeout state from the expected state to the target state and if it succeeds
        /// will setup the the stateobject into the timeout expired state
        /// </summary>
        /// <param name="expectedState">the state that is the expected current state, state will change only if this is correct</param>
        /// <param name="targetState">the state that will be changed to if the expected state is correct</param>
        /// <param name="asyncClose">any close action to be taken by an async task to avoid deadlock.</param>
        /// <returns>boolean value indicating whether the call changed the timeout state</returns>
        private bool OnTimeoutCore(int expectedState, int targetState, bool asyncClose = false)
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
                        AddError(new SqlError(TdsEnums.TIMEOUT_EXPIRED, 0x00, TdsEnums.MIN_ERROR_CLASS, _parser.Server, _parser.Connection.TimeoutErrorInternal.GetErrorMessage(), "", 0, TdsEnums.SNI_WAIT_TIMEOUT));

                        // Grab a reference to the _networkPacketTaskSource in case it becomes null while we are trying to use it
                        TaskCompletionSource<object> source = _networkPacketTaskSource;

                        if (_parser.Connection.IsInPool)
                        {
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
                                SendAttention(mustTakeWriteLock: true, asyncClose);
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
            bool readFromNetwork = true;

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

                SessionHandle handle = default;

                RuntimeHelpers.PrepareConstrainedRegions();
                try
                { }
                finally
                {
                    Interlocked.Increment(ref _readingCount);
                    try
                    {
                        handle = SessionHandle;

                        readFromNetwork = !PartialPacketContainsCompletePacket();
                        if (readFromNetwork)
                        {
                            if (!handle.IsNull)
                            {
                                IncrementPendingCallbacks();

                                readPacket = ReadAsync(handle, out error);

                                if (!(TdsEnums.SNI_SUCCESS == error || TdsEnums.SNI_SUCCESS_IO_PENDING == error))
                                {
                                    DecrementPendingCallbacks(false); // Failure - we won't receive callback!
                                }
                            }
                        }
                        else
                        {
                            readPacket = default;
                            error = TdsEnums.SNI_SUCCESS;
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _readingCount);
                    }
                }

                if (handle.IsNull)
                {
                    throw ADP.ClosedConnectionError();
                }

                if (TdsEnums.SNI_SUCCESS == error)
                { // Success - process results!
                    Debug.Assert(!readFromNetwork || IsValidPacket(readPacket) , "ReadNetworkPacket should not have been null on this async operation!");
                    // Evaluate this condition for MANAGED_SNI. This may not be needed because the network call is happening Async and only the callback can receive a success.
                    ReadAsyncCallback(IntPtr.Zero, readPacket, 0);

                    // Only release packet for Managed SNI as for Native SNI packet is released in finally block.
                    if (TdsParserStateObjectFactory.UseManagedSNI && readFromNetwork && !IsPacketEmpty(readPacket))
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
                    if (readFromNetwork && !IsPacketEmpty(readPacket))
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
                    SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObject.IsConnectionAlive | Info | State Object Id {0}, This connection is currently in use, assume that the connection is 'alive'", _objectID);
                    // NOTE: SNICheckConnection is not currently supported for connections that are in use
                    Debug.Assert(true, "Call to IsConnectionAlive while connection is in use");
                }
                else
                {
                    SniContext = SniContext.Snix_Connect;

                    uint error = CheckConnection();
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

        internal Task WriteByteSpan(ReadOnlySpan<byte> span, bool canAccumulate = true, TaskCompletionSource<object> completion = null)
        {
            return WriteBytes(span, span.Length, 0, canAccumulate, completion);
        }

        internal Task WriteByteArray(byte[] b, int len, int offsetBuffer, bool canAccumulate = true, TaskCompletionSource<object> completion = null)
        {
            return WriteBytes(ReadOnlySpan<byte>.Empty, len, offsetBuffer, canAccumulate, completion, b);
        }

        //
        // Takes a span or a byte array and writes it to the buffer
        // If you pass in a span and a null array then the span wil be used.
        // If you pass in a non-null array then the array will be used and the span is ignored.
        // if the span cannot be written into the current packet then the remaining contents of the span are copied to a
        //  new heap allocated array that will used to callback into the method to continue the write operation.
        private Task WriteBytes(ReadOnlySpan<byte> b, int len, int offsetBuffer, bool canAccumulate = true, TaskCompletionSource<object> completion = null, byte[] array = null)
        {
            if (array != null)
            {
                b = new ReadOnlySpan<byte>(array, offsetBuffer, len);
            }
            try
            {
                bool async = _parser._asyncWrite;  // NOTE: We are capturing this now for the assert after the Task is returned, since WritePacket will turn off async if there is an exception
                Debug.Assert(async || _asyncWriteCount == 0);
                // Do we have to send out in packet size chunks, or can we rely on netlib layer to break it up?
                // would prefer to do something like:
                //
                // if (len > what we have room for || len > out buf)
                //   flush buffer
                //   UnsafeNativeMethods.Write(b)
                //

                int offset = offsetBuffer;

                Debug.Assert(b.Length >= len, "Invalid length sent to WriteBytes()!");

                // loop through and write the entire array
                do
                {
                    if ((_outBytesUsed + len) > _outBuff.Length)
                    {
                        // If the remainder of the data won't fit into the buffer, then we have to put
                        // whatever we can into the buffer, and flush that so we can then put more into
                        // the buffer on the next loop of the while.

                        int remainder = _outBuff.Length - _outBytesUsed;

                        // write the remainder
                        Span<byte> copyTo = _outBuff.AsSpan(_outBytesUsed, remainder);
                        ReadOnlySpan<byte> copyFrom = b.Slice(0, remainder);

                        Debug.Assert(copyTo.Length == copyFrom.Length, $"copyTo.Length:{copyTo.Length} and copyFrom.Length{copyFrom.Length:D} should be the same");

                        copyFrom.CopyTo(copyTo);

                        offset += remainder;
                        _outBytesUsed += remainder;
                        len -= remainder;
                        b = b.Slice(remainder, len);

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

                            if (array == null)
                            {
                                byte[] tempArray = new byte[len];
                                Span<byte> copyTempTo = tempArray.AsSpan();

                                Debug.Assert(copyTempTo.Length == b.Length, $"copyTempTo.Length:{copyTempTo.Length} and copyTempFrom.Length:{b.Length:D} should be the same");

                                b.CopyTo(copyTempTo);
                                array = tempArray;
                                offset = 0;
                            }

                            WriteBytesSetupContinuation(array, len, completion, offset, packetTask);
                            return task;
                        }
                    }
                    else
                    {
                        //((stateObj._outBytesUsed + len) <= stateObj._outBuff.Length )
                        // Else the remainder of the string will fit into the buffer, so copy it into the
                        // buffer and then break out of the loop.

                        Span<byte> copyTo = _outBuff.AsSpan(_outBytesUsed, len);
                        ReadOnlySpan<byte> copyFrom = b.Slice(0, len);

                        Debug.Assert(copyTo.Length == copyFrom.Length, $"copyTo.Length:{copyTo.Length} and copyFrom.Length:{copyFrom.Length:D} should be the same");

                        copyFrom.CopyTo(copyTo);

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

        // This is in its own method to avoid always allocating the lambda in WriteBytes
        private void WriteBytesSetupContinuation(byte[] array, int len, TaskCompletionSource<object> completion, int offset, Task packetTask)
        {
            AsyncHelper.ContinueTask(packetTask, completion,
               onSuccess: () => WriteBytes(ReadOnlySpan<byte>.Empty, len: len, offsetBuffer: offset, canAccumulate: false, completion: completion, array)
           );
        }

        /// <summary>
        /// Creates a human-readable message containing the <c>_inBytesRead</c>, <c>_inBytesUsed</c> counters
        /// and the used and unused portions of the <c>_inBuff</c> array to help diagnosing problems with
        /// packet parsing.
        /// </summary>
        /// <returns></returns>
        internal string DumpBuffer() 
        {
            StringBuilder buffer = new StringBuilder(128);
            buffer.AppendLine("dumping buffer");
            buffer.AppendFormat("_inBytesRead = {0}", _inBytesRead).AppendLine();
            buffer.AppendFormat("_inBytesUsed = {0}", _inBytesUsed).AppendLine();
            int cc = 0; // character counter
            int i;
            buffer.AppendLine("used buffer:");
            for (i=0; i< _inBytesUsed; i++) 
            {
                if (cc==16) {
                    buffer.AppendLine();
                    cc = 0;
                }
                buffer.AppendFormat("{0,-2:X2} ", _inBuff[i]);
                cc++;
            }
            if (cc>0) 
            {
                buffer.AppendLine();
            }

            cc = 0;
            buffer.AppendLine("unused buffer:");
            for (i=_inBytesUsed; i<_inBytesRead; i++) 
            {
                if (cc==16) 
                {
                    buffer.AppendLine();
                    cc = 0;
                }
                buffer.AppendFormat("{0,-2:X2} ", _inBuff[i]);
                cc++;
            }
            if (cc>0) 
            {
                buffer.AppendLine();
            }
            return buffer.ToString();
        }
        
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
            Debug.Assert(_snapshot._storage == null);
            _snapshot.CaptureAsStart(this);
            _snapshotStatus = SnapshotStatus.NotActive;
        }

        internal void ResetSnapshot()
        {
            if (_snapshot != null)
            {
                StateSnapshot snapshot = _snapshot;
                _snapshot = null;
                Debug.Assert(snapshot._storage == null);
                snapshot.Clear();
                Interlocked.CompareExchange(ref _cachedSnapshot, snapshot, null);
            }
            _snapshotStatus = SnapshotStatus.NotActive;
        }

        internal bool IsSnapshotAvailable()
        {
            return _snapshot != null && _snapshot.ContinueEnabled;
        }
        /// <summary>
        /// Returns true if the state object is in the state of continuing from a previously stored snapshot packet 
        /// meaning that consumers should resume from the point where they last needed more data instead of beginning
        /// to process packets in the snapshot from the beginning again
        /// </summary>
        /// <returns></returns>
        internal bool IsSnapshotContinuing()
        {
            return _snapshot != null &&
                _snapshot.ContinueEnabled &&
                _snapshotStatus == TdsParserStateObject.SnapshotStatus.ContinueRunning;
        }

        internal (bool IsAvailable, bool IsStarting, bool IsContinuing) GetSnapshotStatuses()
        {
            bool isAvailable = _snapshot != null && _snapshot.ContinueEnabled;
            bool isStarting = false;
            bool isContinuing = false;
            if (isAvailable)
            {
                isStarting = _snapshotStatus == SnapshotStatus.ReplayStarting;
                isContinuing = _snapshotStatus == SnapshotStatus.ContinueRunning;
            }
            return (isAvailable, isStarting, isContinuing);
        }

        internal int GetSnapshotStorageLength<T>()
        {
            Debug.Assert(_snapshot != null && _snapshot.ContinueEnabled, "should not access snapshot accessor functions without first checking that the snapshot is available");
            return (_snapshot?._storage as IList<T>)?.Count ?? 0;
        }

        internal object TryTakeSnapshotStorage()
        {
            Debug.Assert(_snapshot != null, "should not access snapshot accessor functions without first checking that the snapshot is present");
            object buffer = null;
            if (_snapshot != null)
            {
                buffer = _snapshot._storage;
                _snapshot._storage = null;
            }
            return buffer;
        }

        internal void SetSnapshotStorage(object buffer)
        {
            Debug.Assert(_snapshot != null, "should not access snapshot accessor functions without first checking that the snapshot is available");
            Debug.Assert(_snapshot._storage == null, "should not overwrite snapshot stored buffer");
            if (_snapshot != null)
            {
                _snapshot._storage = buffer;
            }
        }

        /// <summary>
        /// stores the countOfBytesCopiedFromCurrentPacket of bytes copied from the current packet in the snapshot allowing the total
        /// countOfBytesCopiedFromCurrentPacket to be calculated
        /// </summary>
        /// <param name="countOfBytesCopiedFromCurrentPacket"></param>
        internal void SetSnapshotDataSize(int countOfBytesCopiedFromCurrentPacket)
        {
            Debug.Assert(_snapshot != null && _snapshot.ContinueEnabled, "_snapshot must exist to store packet data size");
            _snapshot.SetPacketDataSize(countOfBytesCopiedFromCurrentPacket);
        }

        internal int GetSnapshotTotalSize()
        {
            Debug.Assert(_snapshot != null && _snapshot.ContinueEnabled, "_snapshot must exist to read total size");
            Debug.Assert(_snapshotStatus != SnapshotStatus.NotActive, "_snapshot must be active read total size");
            return _snapshot.GetPacketDataOffset();
        }

        internal int GetSnapshotDataSize()
        {
            Debug.Assert(_snapshot != null && _snapshot.ContinueEnabled, "_snapshot must exist to read packet data size");
            Debug.Assert(_snapshotStatus != SnapshotStatus.NotActive, "_snapshot must be active read packet data size");
            return _snapshot.GetPacketDataSize();
        }

        internal int GetSnapshotPacketID()
        {
            Debug.Assert(_snapshot != null && _snapshot.ContinueEnabled, "_snapshot must exist to read packet data size");
            return _snapshot.GetPacketID();
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

        internal sealed partial class StateSnapshot
        {
            private sealed partial class PacketData
            {
                public byte[] Buffer;
                public int Read;
                public PacketData NextPacket;
                public PacketData PrevPacket;

                /// <summary>
                /// Stores the data size of the total snapshot so far so that enumeration is not needed
                /// to get the offset of the previous packet data in the stored buffer
                /// </summary>
                public int RunningDataSize;

                public int PacketID => Packet.GetIDFromHeader(Buffer.AsSpan(0, TdsEnums.HEADER_LEN));

                internal int GetPacketDataOffset()
                {
                    int previous = 0;
                    if (PrevPacket != null)
                    {
                        previous = PrevPacket.RunningDataSize;
                    }
                    return previous;
                }
                internal int GetPacketDataSize()
                {
                    int previous = 0;
                    if (PrevPacket != null)
                    {
                        previous = PrevPacket.RunningDataSize;
                    }
                    return Math.Max(RunningDataSize - previous, 0);
                }

                internal void Clear()
                {
                    Buffer = null;
                    Read = 0;
                    NextPacket = null;
                    if (PrevPacket != null)
                    {
                        PrevPacket.NextPacket = null;
                        PrevPacket = null;
                    }
                    SetDebugStackImpl(null);
                    SetDebugPacketId(0);
                    SetDebugDataHash();
                }

                internal void SetDebugStack(string value) => SetDebugStackImpl(value);
                internal void SetDebugPacketId(int value) => SetDebugPacketIdImpl(value);
                internal void SetDebugDataHash() => SetDebugDataHashImpl();

                internal void CheckDebugDataHash() => CheckDebugDataHashImpl();

                partial void SetDebugStackImpl(string value);
                partial void SetDebugPacketIdImpl(int value);
                partial void SetDebugDataHashImpl();
                partial void CheckDebugDataHashImpl();
            }

#if DEBUG
            [DebuggerDisplay("{ToString(),nq}")]
            [DebuggerTypeProxy(typeof(PacketDataDebugView))]
            private sealed partial class PacketData
            {
                internal sealed class PacketDataDebugView
                {
                    private readonly PacketData _data;

                    public PacketDataDebugView(PacketData data)
                    {
                        if (data == null)
                        {
                            throw new ArgumentNullException(nameof(data));
                        }

                        _data = data;
                    }

                    public string Type {

                        get
                        {
                            if (_data != null && _data.Buffer!=null)
                            {
                                switch (_data.Buffer[0])
                                {
                                    case 1: return nameof(TdsEnums.MT_SQL);
                                    case 2: return nameof(TdsEnums.MT_LOGIN);
                                    case 3: return nameof(TdsEnums.MT_RPC);
                                    case 4: return nameof(TdsEnums.MT_TOKENS);
                                    case 5: return nameof(TdsEnums.MT_BINARY);
                                    case 6: return nameof(TdsEnums.MT_ATTN);
                                    case 7: return nameof(TdsEnums.MT_BULK);
                                    case 8: return nameof(TdsEnums.MT_FEDAUTH);
                                    case 9: return nameof(TdsEnums.MT_CLOSE);
                                    case 10: return nameof(TdsEnums.MT_ERROR);
                                    case 11: return nameof(TdsEnums.MT_ACK);
                                    case 12: return nameof(TdsEnums.MT_ECHO);
                                    case 13: return nameof(TdsEnums.MT_LOGOUT);
                                    case 14: return nameof(TdsEnums.MT_TRANS);
                                    case 15: return nameof(TdsEnums.MT_OLEDB);
                                    case 16: return nameof(TdsEnums.MT_LOGIN7);
                                    case 17: return nameof(TdsEnums.MT_SSPI);
                                    case 18: return nameof(TdsEnums.MT_PRELOGIN);
                                    default: return _data.Buffer[0].ToString("X2");
                                }
                            }
                            return "";
                        }
                    }

                    public string Status
                    {
                        get
                        {
                            if (_data != null && _data.Buffer != null && _data.Buffer.Length > 1)
                            {
                                int status = Packet.GetStatusFromHeader(_data.Buffer);
                                StringBuilder buffer = new StringBuilder(10);

                                if ((status & TdsEnums.ST_EOM) == TdsEnums.ST_EOM)
                                {
                                    if (buffer.Length > 0)
                                    {
                                        buffer.Append(',');
                                    }
                                    buffer.Append(nameof(TdsEnums.ST_EOM));
                                }
                                if ((status & TdsEnums.ST_AACK) == TdsEnums.ST_AACK)
                                {
                                    if (buffer.Length > 0)
                                    {
                                        buffer.Append(',');
                                    }
                                    buffer.Append(nameof(TdsEnums.ST_AACK));
                                }
                                if ((status & TdsEnums.ST_BATCH) == TdsEnums.ST_BATCH)
                                {
                                    if (buffer.Length > 0)
                                    {
                                        buffer.Append(',');
                                    }
                                    buffer.Append(nameof(TdsEnums.ST_BATCH));
                                }
                                if ((status & TdsEnums.ST_RESET_CONNECTION) == TdsEnums.ST_RESET_CONNECTION)
                                {
                                    if (buffer.Length > 0)
                                    {
                                        buffer.Append(',');
                                    }
                                    buffer.Append(nameof(TdsEnums.ST_RESET_CONNECTION));
                                }
                                if ((status & TdsEnums.ST_RESET_CONNECTION_PRESERVE_TRANSACTION) == TdsEnums.ST_RESET_CONNECTION_PRESERVE_TRANSACTION)
                                {
                                    if (buffer.Length > 0)
                                    {
                                        buffer.Append(',');
                                    }
                                    buffer.Append(nameof(TdsEnums.ST_RESET_CONNECTION_PRESERVE_TRANSACTION));
                                }

                                return buffer.ToString();
                            }

                            return "";
                        }
                    }

                    public int Length => _data.DataLength;

                    public int Spid => _data.SPID;

                    public int PacketID => _data.PacketID;

                    public ReadOnlySpan<byte> HeaderBytes => _data.GetHeaderSpan();

                    public ReadOnlySpan<byte> Data => _data.Buffer.AsSpan(TdsEnums.HEADER_LEN);

                    public int RunningDataSize => _data.RunningDataSize;

                    public PacketData NextPacket => _data.NextPacket;
                    public PacketData PrevPacket => _data.PrevPacket;
                }

                public int DebugPacketId;
                public string Stack;
                public byte[] Hash;

                public int SPID => Packet.GetSpidFromHeader(Buffer.AsSpan(0, TdsEnums.HEADER_LEN));

                public bool IsEOM => Packet.GetIsEOMFromHeader(Buffer.AsSpan(0, TdsEnums.HEADER_LEN));

                public int DataLength => Packet.GetDataLengthFromHeader(Buffer.AsSpan(0, TdsEnums.HEADER_LEN));

                public ReadOnlySpan<byte> GetHeaderSpan() => Buffer.AsSpan(0, TdsEnums.HEADER_LEN);

                partial void SetDebugStackImpl(string value) => Stack = value;

                partial void SetDebugPacketIdImpl(int value) => DebugPacketId = value;

                partial void SetDebugDataHashImpl()
                {
                    if (Buffer != null)
                    {
                        using (MD5 hasher = MD5.Create())
                        {
                            Hash = hasher.ComputeHash(Buffer, 0, Read);
                        }
                    }
                    else
                    {
                        Hash = null;
                    }
                    
                }

                partial void CheckDebugDataHashImpl()
                {
                    if (Hash == null)
                    {
                        if (Buffer != null && Read > 0)
                        {
                            throw new InvalidOperationException("Packet modification detected. Hash is null but packet contains non-null buffer");
                        }
                    }
                    else
                    {
                        byte[] checkHash = null;
                        using (MD5 hasher = MD5.Create())
                        {
                            checkHash = hasher.ComputeHash(Buffer, 0, Read);
                        }

                        for (int index = 0; index < Hash.Length; index++)
                        {
                            if (Hash[index] != checkHash[index])
                            {
                                throw new InvalidOperationException("Packet modification detected. Hash from packet creation does not match hash from packet check");
                            }
                        }
                    }
                }

                public override string ToString()
                {
                    return $"{PacketID}({GetPacketDataOffset()},{GetPacketDataSize()})";
                }
            }
#endif

            private sealed class StateObjectData
            {
                private int _inBytesUsed;
                private int _inBytesPacket;
                private byte _messageStatus;
                internal NullBitmap _nullBitmapInfo;
                private _SqlMetaDataSet _cleanupMetaData;
                internal _SqlMetaDataSetCollection _cleanupAltMetaDataSetArray;
                private SnapshottedStateFlags _state;
                private ulong _longLen;
                private ulong _longLenLeft;

                internal void Capture(TdsParserStateObject stateObj, bool trackStack = true)
                {
                    _inBytesUsed = stateObj._inBytesUsed;
                    _inBytesPacket = stateObj._inBytesPacket;
                    _messageStatus = stateObj._messageStatus;
                    _nullBitmapInfo = stateObj._nullBitmapInfo; // _nullBitmapInfo must be cloned before it is updated
                    _longLen = stateObj._longlen;
                    _longLenLeft = stateObj._longlenleft;
                    _cleanupMetaData = stateObj._cleanupMetaData;
                    _cleanupAltMetaDataSetArray = stateObj._cleanupAltMetaDataSetArray; // _cleanupAltMetaDataSetArray must be cloned before it is updated
                    _state = stateObj._snapshottedState;
#if DEBUG
                    if (trackStack)
                    {
                        stateObj._lastStack = null;
                    }
                    Debug.Assert(stateObj._bTmpRead == 0, "Has partially read data when snapshot taken");
                    Debug.Assert(stateObj._partialHeaderBytesRead == 0, "Has partially read header when snapshot taken");
#endif
                }

                internal void Clear(TdsParserStateObject stateObj, bool trackStack = true)
                {
                    _inBytesUsed = 0;
                    _inBytesPacket = 0;
                    _messageStatus = 0;
                    _nullBitmapInfo = default;
                    _longLen = 0;
                    _longLenLeft = 0;
                    _cleanupMetaData = null;
                    _cleanupAltMetaDataSetArray = null;
                    _state = SnapshottedStateFlags.None;
#if DEBUG
                    if (trackStack)
                    {
                        stateObj._lastStack = null;
                    }
#endif
                }

                internal void Restore(TdsParserStateObject stateObj)
                {
                    stateObj._inBytesUsed = _inBytesUsed;
                    stateObj._inBytesPacket = _inBytesPacket;
                    stateObj._messageStatus = _messageStatus;
                    stateObj._nullBitmapInfo = _nullBitmapInfo;
                    stateObj._cleanupMetaData = _cleanupMetaData;
                    stateObj._cleanupAltMetaDataSetArray = _cleanupAltMetaDataSetArray;

                    // Make sure to go through the appropriate increment/decrement methods if changing HasOpenResult
                    if (!stateObj.HasOpenResult && ((_state & SnapshottedStateFlags.OpenResult) == SnapshottedStateFlags.OpenResult))
                    {
                        stateObj.IncrementAndObtainOpenResultCount(stateObj._executedUnderTransaction);
                    }
                    else if (stateObj.HasOpenResult && ((_state & SnapshottedStateFlags.OpenResult) != SnapshottedStateFlags.OpenResult))
                    {
                        stateObj.DecrementOpenResultCount();
                    }
                    //else _stateObj._hasOpenResult is already == _snapshotHasOpenResult
                    stateObj._snapshottedState = _state;

                    // reset plp state
                    stateObj._longlen = _longLen;
                    stateObj._longlenleft = _longLenLeft;

                    // Reset partially read state (these only need to be maintained if doing async without snapshot)
                    stateObj._bTmpRead = 0;
                    stateObj._partialHeaderBytesRead = 0;
                }
            }

            private TdsParserStateObject _stateObj;
            private StateObjectData _replayStateData;
            private StateObjectData _continueStateData;

            internal object _storage;

            private PacketData _lastPacket;
            private PacketData _firstPacket;
            private PacketData _current;
            private PacketData _continuePacket;
            private PacketData _sparePacket;

#if DEBUG
            private int _packetCounter;
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

            internal void AssertCurrent()
            {
                Debug.Assert(_current == _lastPacket);
            }

            internal void CheckStack(string trace)
            {
                PacketData prev = _current?.PrevPacket;
                if (prev.Stack == null)
                {
                    prev.Stack = trace;
                }
                else
                {
                    Debug.Assert(_stateObj._permitReplayStackTraceToDiffer || prev.Stack == trace, "The stack trace on subsequent replays should be the same");
                }
            }
#endif
            public bool ContinueEnabled => !LocalAppContextSwitches.UseCompatibilityAsyncBehaviour;

            internal void CloneNullBitmapInfo()
            {
                if (_stateObj._nullBitmapInfo.ReferenceEquals(_replayStateData?._nullBitmapInfo ?? default))
                {
                    _stateObj._nullBitmapInfo = _stateObj._nullBitmapInfo.Clone();
                }
            }

            internal void CloneCleanupAltMetaDataSetArray()
            {
                if (_stateObj._cleanupAltMetaDataSetArray != null && object.ReferenceEquals(_replayStateData?._cleanupAltMetaDataSetArray ?? default, _stateObj._cleanupAltMetaDataSetArray))
                {
                    _stateObj._cleanupAltMetaDataSetArray = (_SqlMetaDataSetCollection)_stateObj._cleanupAltMetaDataSetArray.Clone();
                }
            }

            internal void AppendPacketData(byte[] buffer, int read)
            {
                Debug.Assert(buffer != null, "packet data cannot be null");
                Debug.Assert(read >= TdsEnums.HEADER_LEN, "minimum packet length is TdsEnums.HEADER_LEN");
                Debug.Assert(TdsEnums.HEADER_LEN + Packet.GetDataLengthFromHeader(buffer) == read, "partially read packets cannot be appended to the snapshot");
#if DEBUG
                for (PacketData current = _firstPacket; current != null; current = current.NextPacket)
                {
                    if (ReferenceEquals(current.Buffer, buffer))
                    {
                        // multiple packets are permitted to be in the same buffer because of partial packets
                        // but their contents cannot overlap
                        if ((current.Read + current.DataLength) > read)
                        {
                            Debug.Fail("duplicate or overlapping packet appended to snapshot");
                        }
                    }
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
#if DEBUG
                packetData.SetDebugStack(_stateObj._lastStack);
                packetData.SetDebugPacketId(Interlocked.Increment(ref _packetCounter));
                packetData.SetDebugDataHash();
#endif
                if (_firstPacket is null)
                {
                    _firstPacket = packetData;
                }
                else
                {
                    _lastPacket.NextPacket = packetData;
                    packetData.PrevPacket = _lastPacket;
                }
                _lastPacket = packetData;
            }

            internal bool MoveNext()
            {
                bool retval = false;
                SnapshotStatus moveToMode = SnapshotStatus.ReplayRunning;
                bool moved = false;
                if (_current == null)
                {
                    _current = _firstPacket;
                    moveToMode = SnapshotStatus.ReplayStarting;
                    moved = true;
                }
                else if (_current.NextPacket != null)
                {
                    if (_stateObj._snapshotStatus == SnapshotStatus.ContinueRunning)
                    {
                        moveToMode = SnapshotStatus.ContinueRunning;
                    }
                    _current = _current.NextPacket;
                    moved = true;
                }

                if (moved)
                {
                    _stateObj.SetBuffer(_current.Buffer, 0, _current.Read);
                    _stateObj._snapshotStatus = moveToMode;
                    retval = true;
                }

                return retval;
            }

            internal void MoveToStart()
            {
                // go back to the beginning
                _current = null;
                MoveNext();
                _replayStateData.Restore(_stateObj);
                _stateObj.AssertValidState();
            }

            internal bool MoveToContinue()
            {
                if (ContinueEnabled)
                {
                    if (_continuePacket != null && _continuePacket != _current)
                    {
                        _continueStateData.Restore(_stateObj);
                        _stateObj.SetBuffer(_current.Buffer, 0, _current.Read);
                        _stateObj._snapshotStatus = SnapshotStatus.ContinueRunning;
                        _stateObj.AssertValidState();
                        return true;
                    }
                }
                return false;
            }

            internal void CaptureAsStart(TdsParserStateObject stateObj)
            {
                _firstPacket = null;
                _lastPacket = null;
                _current = null;

                _stateObj = stateObj;
                _replayStateData ??= new StateObjectData();
                _replayStateData.Capture(stateObj);
#if DEBUG
                _rollingPend = 0;
                _rollingPendCount = 0;
                stateObj._lastStack = null;
                Debug.Assert(stateObj._bTmpRead == 0, "Has partially read data when snapshot taken");
                Debug.Assert(stateObj._partialHeaderBytesRead == 0, "Has partially read header when snapshot taken");
#endif

                AppendPacketData(stateObj._inBuff, stateObj._inBytesRead);
            }

            internal void CaptureAsContinue(TdsParserStateObject stateObj)
            {
                if (ContinueEnabled)
                {
                    Debug.Assert(_stateObj == stateObj);
                    if (_current is not null)
                    {
                        _continueStateData ??= new StateObjectData();
                        _continueStateData.Capture(stateObj, trackStack: false);
                        _continuePacket = _current;
                    }
                }
            }

            internal void SetPacketDataSize(int size)
            {
                PacketData target = _current;
                // special case for the start of a snapshot when we expect to have only a single packet
                // but have no current packet because we haven't started to replay yet.
                if (
                    target == null &&
                    _firstPacket != null &&
                    _firstPacket == _lastPacket
                )
                {
                    target = _firstPacket;
                }

                if (target == null)
                {
                    throw new InvalidOperationException();
                }
                int total = 0;
                if (target.PrevPacket != null)
                {
                    total = target.PrevPacket.RunningDataSize;
                }
                target.RunningDataSize = total + size;
            }

            internal int GetPacketDataOffset()
            {
                int offset = 0;
                if (_current != null)
                {
                    offset = _current.GetPacketDataOffset();
                }
                return offset;
            }

            internal int GetPacketDataSize()
            {
                int offset = 0;
                if (_current != null)
                {
                    offset = _current.GetPacketDataSize();
                }
                return offset;
            }

            internal int GetPacketID()
            {
                int id = 0;
                if (_current != null)
                {
                    id = _current.PacketID;
                }
                return id;
            }

            internal void Clear()
            {
                ClearState();
                ClearPackets();
            }

            private void ClearPackets()
            {
                PacketData packet = _firstPacket;
                _firstPacket = null;
                _lastPacket = null;
                _continuePacket = null;
                _current = null;
                packet.Clear();
                _sparePacket = packet;
            }

            private void ClearState()
            {
                Debug.Assert(_storage == null);
                _storage = null;
                _replayStateData.Clear(_stateObj);
                _continueStateData?.Clear(_stateObj, trackStack: false);
#if DEBUG
                _rollingPend = 0;
                _rollingPendCount = 0;
                _stateObj._lastStack = null;
                _packetCounter = 0;
#endif
                _stateObj = null;
            }
        }
    }
}
