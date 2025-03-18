// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Interop.Windows.Sni;
using Microsoft.Data.SqlClient.LocalDb;

#if NETFRAMEWORK
using System.Runtime.CompilerServices;
using Microsoft.Data.Common;
#endif

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SNILoadHandle : SafeHandle
    {
        internal static readonly SNILoadHandle SingletonInstance = new SNILoadHandle();

        internal readonly SqlAsyncCallbackDelegate ReadAsyncCallbackDispatcher = new SqlAsyncCallbackDelegate(ReadDispatcher);
        internal readonly SqlAsyncCallbackDelegate WriteAsyncCallbackDispatcher = new SqlAsyncCallbackDelegate(WriteDispatcher);

        private readonly uint _sniStatus = TdsEnums.SNI_UNINITIALIZED;
        private readonly EncryptionOptions _encryptionOption = EncryptionOptions.OFF;
        private bool? _clientOSEncryptionSupport = null;

        private SNILoadHandle() : base(IntPtr.Zero, true)
        {
            // SQL BU DT 346588 - from security review - SafeHandle guarantees this is only called once.
            // The reason for the safehandle is guaranteed initialization and termination of SNI to
            // ensure SNI terminates and cleans up properly.
            try
            { }
            finally
            {
                _sniStatus = SniNativeWrapper.SniInitialize();
                base.handle = (IntPtr)1; // Initialize to non-zero dummy variable.
            }
        }

        /// <summary>
        /// Verify client encryption possibility.
        /// </summary>
        public bool ClientOSEncryptionSupport
        {
            get
            {
                if (_clientOSEncryptionSupport is null)
                {
                    // VSDevDiv 479597: If initialize fails, don't call QueryInfo.
                    if (TdsEnums.SNI_SUCCESS == _sniStatus)
                    {
                        try
                        {
                            uint value = 0;
                            // Query OS to find out whether encryption is supported.
                            SniNativeWrapper.SniQueryInfo(QueryType.SNI_QUERY_CLIENT_ENCRYPT_POSSIBLE, ref value);
                            _clientOSEncryptionSupport = value != 0;
                        }
                        catch (Exception e)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SNILoadHandle.EncryptClientPossible|SEC> Exception occurs during resolving encryption possibility: {0}", e.Message);
                        }
                    }
                }
                return _clientOSEncryptionSupport.Value;
            }
        }

        public override bool IsInvalid => (IntPtr.Zero == base.handle);

        override protected bool ReleaseHandle()
        {
            if (base.handle != IntPtr.Zero)
            {
                if (TdsEnums.SNI_SUCCESS == _sniStatus)
                {
                    LocalDbApi.ReleaseDllHandles();
                    SniNativeWrapper.SniTerminate();
                }
                base.handle = IntPtr.Zero;
            }

            return true;
        }

        public uint Status => _sniStatus;

        public EncryptionOptions Options => _encryptionOption;

        private static void ReadDispatcher(IntPtr key, IntPtr packet, uint error)
        {
            // This is the app-domain dispatcher for all async read callbacks, It 
            // simply gets the state object from the key that it is passed, and 
            // calls the state object's read callback.
            Debug.Assert(IntPtr.Zero != key, "no key passed to read callback dispatcher?");
            if (IntPtr.Zero != key)
            {
                // NOTE: we will get a null ref here if we don't get a key that
                //       contains a GCHandle to TDSParserStateObject; that is 
                //       very bad, and we want that to occur so we can catch it.
                GCHandle gcHandle = (GCHandle)key;
                TdsParserStateObject stateObj = (TdsParserStateObject)gcHandle.Target;

                if (stateObj != null)
                {
#if NETFRAMEWORK
                    stateObj.ReadAsyncCallback(IntPtr.Zero, packet, error);
#else
                    stateObj.ReadAsyncCallback(IntPtr.Zero, PacketHandle.FromNativePointer(packet), error);
#endif // NETFRAMEWORK
                }
            }
        }

        private static void WriteDispatcher(IntPtr key, IntPtr packet, uint error)
        {
            // This is the app-domain dispatcher for all async write callbacks, It 
            // simply gets the state object from the key that it is passed, and 
            // calls the state object's write callback.
            Debug.Assert(IntPtr.Zero != key, "no key passed to write callback dispatcher?");
            if (IntPtr.Zero != key)
            {
                // NOTE: we will get a null ref here if we don't get a key that
                //       contains a GCHandle to TDSParserStateObject; that is 
                //       very bad, and we want that to occur so we can catch it.
                GCHandle gcHandle = (GCHandle)key;
                TdsParserStateObject stateObj = (TdsParserStateObject)gcHandle.Target;

                if (stateObj != null)
                {
#if NETFRAMEWORK
                    stateObj.WriteAsyncCallback(IntPtr.Zero, packet, error);
#else
                    stateObj.WriteAsyncCallback(IntPtr.Zero, PacketHandle.FromNativePointer(packet), error);
#endif // NETFRAMEWORK
                }
            }
        }
    }

    internal sealed class SNIHandle : SafeHandle
    {
        private readonly uint _status = TdsEnums.SNI_UNINITIALIZED;
        private readonly bool _fSync = false;

        // creates a physical connection
        internal SNIHandle(
            ConsumerInfo myInfo,
            string serverName,
            ref string spn,
            int timeout,
            out byte[] instanceName,
            bool flushCache,
            bool fSync,
            bool fParallel,
#if NETFRAMEWORK
            TransparentNetworkResolutionState transparentNetworkResolutionState,
            int totalTimeout,
#endif
            SqlConnectionIPAddressPreference ipPreference,
            SQLDNSInfo cachedDNSInfo,
            string hostNameInCertificate)
            : base(IntPtr.Zero, true)
        {
#if NETFRAMEWORK
            RuntimeHelpers.PrepareConstrainedRegions();
#endif
            try
            { }
            finally
            {
                _fSync = fSync;
                instanceName = new byte[256]; // Size as specified by netlibs.
                // Option ignoreSniOpenTimeout is no longer available
                //if (ignoreSniOpenTimeout)
                //{
                //    // UNDONE: ITEM12001110 (DB Mirroring Reconnect) Old behavior of not truly honoring timeout presevered 
                //    //  for non-failover scenarios to avoid breaking changes as part of a QFE.  Consider fixing timeout
                //    //  handling in next full release and removing ignoreSniOpenTimeout parameter.
                //    timeout = Timeout.Infinite; // -1 == native SNIOPEN_TIMEOUT_VALUE / INFINITE
                //}

                #if NETFRAMEWORK
                int transparentNetworkResolutionStateNo = (int)transparentNetworkResolutionState;
                _status = SniNativeWrapper.SniOpenSyncEx(
                    myInfo,
                    serverName,
                    ref base.handle,
                    ref spn,
                    instanceName,
                    flushCache,
                    fSync,
                    timeout,
                    fParallel,
                    transparentNetworkResolutionStateNo,
                    totalTimeout,
                    ipPreference,
                    cachedDNSInfo,
                    hostNameInCertificate);
                #else
                _status = SniNativeWrapper.SniOpenSyncEx(
                    myInfo,
                    serverName,
                    ref base.handle,
                    ref spn,
                    instanceName,
                    flushCache,
                    fSync,
                    timeout,
                    fParallel,
                    ipPreference,
                    cachedDNSInfo,
                    hostNameInCertificate);
                #endif
            }
        }

        // constructs SNI Handle for MARS session
        internal SNIHandle(ConsumerInfo myInfo, SNIHandle parent, SqlConnectionIPAddressPreference ipPreference, SQLDNSInfo cachedDNSInfo) : base(IntPtr.Zero, true)
        {
            try
            { }
            finally
            {
                _status = SniNativeWrapper.SniOpenMarsSession(myInfo, parent, ref base.handle, parent._fSync, ipPreference, cachedDNSInfo);
            }
        }

        public override bool IsInvalid
        {
            get
            {
                return (IntPtr.Zero == base.handle);
            }
        }

        override protected bool ReleaseHandle()
        {
            // NOTE: The SafeHandle class guarantees this will be called exactly once.
            IntPtr ptr = base.handle;
            base.handle = IntPtr.Zero;
            if (IntPtr.Zero != ptr)
            {
                if (0 != SniNativeWrapper.SniClose(ptr))
                {
                    return false;   // SNIClose should never fail.
                }
            }
            return true;
        }

        internal uint Status
        {
            get
            {
                return _status;
            }
        }
    }

    internal sealed class SNIPacket : SafeHandle
    {
        internal SNIPacket(SafeHandle sniHandle) : base(IntPtr.Zero, true)
        {
            SniNativeWrapper.SniPacketAllocate(sniHandle, IoType.WRITE, ref base.handle);
            if (IntPtr.Zero == base.handle)
            {
                throw SQL.SNIPacketAllocationFailure();
            }
        }

        public override bool IsInvalid
        {
            get
            {
                return (IntPtr.Zero == base.handle);
            }
        }

        override protected bool ReleaseHandle()
        {
            // NOTE: The SafeHandle class guarantees this will be called exactly once.
            IntPtr ptr = base.handle;
            base.handle = IntPtr.Zero;
            if (IntPtr.Zero != ptr)
            {
                SniNativeWrapper.SniPacketRelease(ptr);
            }
            return true;
        }
    }

    internal sealed class WritePacketCache : IDisposable
    {
        private bool _disposed;
        private Stack<SNIPacket> _packets;

        public WritePacketCache()
        {
            _disposed = false;
            _packets = new Stack<SNIPacket>();
        }

        public SNIPacket Take(SNIHandle sniHandle)
        {
            SNIPacket packet;
            if (_packets.Count > 0)
            {
                // Success - reset the packet
                packet = _packets.Pop();
                SniNativeWrapper.SniPacketReset(sniHandle, IoType.WRITE, packet, ConsumerNumber.SNI_Consumer_SNI);
            }
            else
            {
                // Failed to take a packet - create a new one
                packet = new SNIPacket(sniHandle);
            }
            return packet;
        }

        public void Add(SNIPacket packet)
        {
            if (!_disposed)
            {
                _packets.Push(packet);
            }
            else
            {
                // If we're disposed, then get rid of any packets added to us
                packet.Dispose();
            }
        }

        public void Clear()
        {
            while (_packets.Count > 0)
            {
                _packets.Pop().Dispose();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Clear();
            }
        }
    }
}
