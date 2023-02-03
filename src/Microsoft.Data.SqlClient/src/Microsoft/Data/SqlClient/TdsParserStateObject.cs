// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    sealed internal class LastIOTimer
    {
        internal long _value;
    }

    partial class TdsParserStateObject
    {
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
        private bool _snapshotReplay;
        private StateSnapshot _snapshot;
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

        // Requests to send attention will be ignored when _skipSendAttention is true.
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

        internal TdsParserStateObject(TdsParser parser)
        {
            // Construct a physical connection
            Debug.Assert(null != parser, "no parser?");
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

        ////////////////
        // Properties //
        ////////////////

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

        internal bool HasOwner => _owner.TryGetTarget(out object _);

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

        internal bool TryStartNewRow(bool isNullCompressed, int nullBitmapColumnsCount = 0)
        {
            Debug.Assert(!isNullCompressed || nullBitmapColumnsCount > 0, "Null-Compressed row requires columns count");

            _snapshot?.CloneNullBitmapInfo();

            // initialize or unset null bitmap information for the current row
            if (isNullCompressed)
            {
                // assert that NBCROW is not in use by 2005 or before
                Debug.Assert(_parser.Is2008OrNewer, "NBCROW is sent by pre-2008 server");

                if (!_nullBitmapInfo.TryInitialize(this, nullBitmapColumnsCount))
                {
                    return false;
                }
            }
            else
            {
                _nullBitmapInfo.Clean();
            }

            return true;
        }

        internal bool TryReadChars(char[] chars, int charsOffset, int charsCount, out int charsCopied)
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
                    bool result = TryReadChar(out chars[charsOffset + charsCopied]);
                    if (result)
                    {
                        charsCopied += 1;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;
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

        private partial struct NullBitmap
        {
            private byte[] _nullBitmap;
            private int _columnsCount; // set to 0 if not used or > 0 for NBC rows

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

        internal void CloseSession()
        {
            ResetCancelAndProcessAttention();
#if DEBUG
            InvalidateDebugOnlyCopyOfSniContext();
#endif
            Parser.PutSession(this);
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

        internal void ThrowExceptionAndWarning(bool callerHasConnectionLock = false, bool asyncClose = false)
        {
            _parser.ThrowExceptionAndWarning(this, callerHasConnectionLock, asyncClose);
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
        internal bool TryProcessHeader()
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
                        _partialHeaderBytesRead = 0;
                        _inBytesPacket = ((int)_partialHeaderBuffer[TdsEnums.HEADER_LEN_FIELD_OFFSET] << 8 |
                                  (int)_partialHeaderBuffer[TdsEnums.HEADER_LEN_FIELD_OFFSET + 1]) - _inputHeaderLen;

                        _messageStatus = _partialHeaderBuffer[1];
                        _spid = _partialHeaderBuffer[TdsEnums.SPID_OFFSET] << 8 |
                                  _partialHeaderBuffer[TdsEnums.SPID_OFFSET + 1];

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
                            return true;
                        }

                        if (!TryReadNetworkPacket())
                        {
                            return false;
                        }

                        if (IsTimeoutStateExpired)
                        {
                            ThrowExceptionAndWarning();
                            return true;
                        }
                    }
                } while (_partialHeaderBytesRead != 0); // This is reset to 0 once we have read everything that we need

                AssertValidState();
            }
            else
            {
                // normal header processing...
                _messageStatus = _inBuff[_inBytesUsed + 1];
                _inBytesPacket = (_inBuff[_inBytesUsed + TdsEnums.HEADER_LEN_FIELD_OFFSET] << 8 |
                                              _inBuff[_inBytesUsed + TdsEnums.HEADER_LEN_FIELD_OFFSET + 1]) - _inputHeaderLen;
                _spid = _inBuff[_inBytesUsed + TdsEnums.SPID_OFFSET] << 8 |
                                              _inBuff[_inBytesUsed + TdsEnums.SPID_OFFSET + 1];
#if !NETFRAMEWORK
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

            return true;
        }

        // This ensure that there is data available to be read in the buffer and that the header has been processed
        // NOTE: This method (and all it calls) should be retryable without replaying a snapshot
        internal bool TryPrepareBuffer()
        {
#if NETFRAMEWORK
            TdsParser.ReliabilitySection.Assert("unreliable call to ReadBuffer");  // you need to setup for a thread abort somewhere before you call this method
#endif
            Debug.Assert(_inBuff != null, "packet buffer should not be null!");

            // Header spans packets, or we haven't read the header yet - process header
            if ((_inBytesPacket == 0) && (_inBytesUsed < _inBytesRead))
            {
                if (!TryProcessHeader())
                {
                    return false;
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
                    if (!TryReadNetworkPacket())
                    {
                        return false;
                    }
                }
                else if (_inBytesPacket == 0)
                {
                    // Else we have finished the packet and so we must read as much data as possible
                    if (!TryReadNetworkPacket())
                    {
                        return false;
                    }

                    if (!TryProcessHeader())
                    {
                        return false;
                    }

                    Debug.Assert(_inBytesPacket != 0, "_inBytesPacket cannot be 0 after processing header!");
                    if (_inBytesUsed == _inBytesRead)
                    {
                        // we read a header but didn't get anything else except it
                        // VSTS 219884: it can happen that the TDS packet header and its data are split across two network packets.
                        // Read at least one more byte to get/cache the first data portion of this TDS packet
                        if (!TryReadNetworkPacket())
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    Debug.Fail("entered negative _inBytesPacket loop");
                }
                AssertValidState();
            }

            return true;
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
                          _parser.Is2005OrNewer &&
                           _outBytesUsed == (_outputHeaderLen + BitConverter.ToInt32(_outBuff, _outputHeaderLen)) &&
                           _outputPacketNumber == 1)
                          ||
                          (_outBytesUsed == _outputHeaderLen && _outputPacketNumber == 1),
                          "SetPacketSize called with data in the buffer!");

            if (_inBuff == null || _inBuff.Length != size)
            { // We only check _inBuff, since two buffers should be consistent.
                // Allocate or re-allocate _inBuff.
                if (_inBuff == null)
                {
                    _inBuff = new byte[size];
                    _inBytesRead = 0;
                    _inBytesUsed = 0;
                }
                else if (size != _inBuff.Length)
                {
                    // If new size is other than existing...
                    if (_inBytesRead > _inBytesUsed)
                    {
                        // if we still have data left in the buffer we must keep that array reference and then copy into new one
                        byte[] temp = _inBuff;

                        _inBuff = new byte[size];

                        // copy remainder of unused data
                        int remainingData = _inBytesRead - _inBytesUsed;
                        if ((temp.Length < _inBytesUsed + remainingData) || (_inBuff.Length < remainingData))
                        {
                            string errormessage = StringsHelper.GetString(Strings.SQL_InvalidInternalPacketSize) + ' ' + temp.Length + ", " + _inBytesUsed + ", " + remainingData + ", " + _inBuff.Length;
                            throw SQL.InvalidInternalPacketSize(errormessage);
                        }
                        Buffer.BlockCopy(temp, _inBytesUsed, _inBuff, 0, remainingData);

                        _inBytesRead = _inBytesRead - _inBytesUsed;
                        _inBytesUsed = 0;

                        AssertValidState();
                    }
                    else
                    {
                        // buffer is empty - just create the new one that is double the size of the old one
                        _inBuff = new byte[size];
                        _inBytesRead = 0;
                        _inBytesUsed = 0;
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
