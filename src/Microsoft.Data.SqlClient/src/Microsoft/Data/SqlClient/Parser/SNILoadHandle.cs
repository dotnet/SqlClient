// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Interop.Windows.Sni;
using Microsoft.Data.SqlClient.Internal;
using Microsoft.Data.SqlClient.LocalDb;
using Microsoft.Data.SqlClient.Parser.Login;

namespace Microsoft.Data.SqlClient.Parser;

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

        protected override bool ReleaseHandle()
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
                    stateObj.ReadAsyncCallback(IntPtr.Zero, PacketHandle.FromNativePointer(packet), error);
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
                    stateObj.WriteAsyncCallback(IntPtr.Zero, PacketHandle.FromNativePointer(packet), error);
                }
            }
        }
    }
