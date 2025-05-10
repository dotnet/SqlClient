// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
#if NET
using Microsoft.Data.SqlClient.SNI;
#endif

namespace Microsoft.Data.SqlClient
{
    internal sealed class TdsParserStateObjectFactory
    {
        public static readonly TdsParserStateObjectFactory Singleton = new TdsParserStateObjectFactory();

        private const string UseManagedNetworkingOnWindows = "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows";

#if NET
        private static bool s_shouldUseManagedSNI;

        // If the appcontext switch is set then Use Managed SNI based on the value. Otherwise Native SNI.dll will be used by default.
        public static bool UseManagedSNI =>
            AppContext.TryGetSwitch(UseManagedNetworkingOnWindows, out s_shouldUseManagedSNI) ? s_shouldUseManagedSNI : false;
#else
        public const bool UseManagedSNI = false;
#endif

        public EncryptionOptions EncryptionOptions =>
#if NET
            UseManagedSNI ? SNI.SNILoadHandle.SingletonInstance.Options : SNILoadHandle.SingletonInstance.Options;
#else
            SNILoadHandle.SingletonInstance.Options;
#endif

        public uint SNIStatus =>
#if NET
            UseManagedSNI ? SNI.SNILoadHandle.SingletonInstance.Status : SNILoadHandle.SingletonInstance.Status;
#else
            SNILoadHandle.SingletonInstance.Status;
#endif

        /// <summary>
        /// Verify client encryption possibility.
        /// </summary>
        public bool ClientOSEncryptionSupport =>
#if NET
            UseManagedSNI ? SNI.SNILoadHandle.SingletonInstance.ClientOSEncryptionSupport : SNILoadHandle.SingletonInstance.ClientOSEncryptionSupport;
#else
            SNILoadHandle.SingletonInstance.ClientOSEncryptionSupport;
#endif

        public TdsParserStateObject CreateTdsParserStateObject(TdsParser parser)
        {
#if NET
            if (UseManagedSNI)
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectFactory.CreateTdsParserStateObject | Info | Found AppContext switch '{0}' enabled, managed networking implementation will be used."
                   , UseManagedNetworkingOnWindows);
                return new TdsParserStateObjectManaged(parser);
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectFactory.CreateTdsParserStateObject | Info | AppContext switch '{0}' not enabled, native networking implementation will be used."
                   , UseManagedNetworkingOnWindows);
                return new TdsParserStateObjectNative(parser);
            }
#else
            return new TdsParserStateObjectNative(parser);
#endif
        }

        internal TdsParserStateObject CreateSessionObject(TdsParser tdsParser, TdsParserStateObject _pMarsPhysicalConObj, bool v)
        {
#if NET
            if (TdsParserStateObjectFactory.UseManagedSNI)
            {
                return new TdsParserStateObjectManaged(tdsParser, _pMarsPhysicalConObj, true);
            }
            else
#endif
            {
                return new TdsParserStateObjectNative(tdsParser, _pMarsPhysicalConObj, true);
            }
        }
    }
}
