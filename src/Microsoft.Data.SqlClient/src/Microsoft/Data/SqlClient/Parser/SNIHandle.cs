// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Interop.Windows.Sni;

namespace Microsoft.Data.SqlClient.Parser;

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

        protected override bool ReleaseHandle()
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
