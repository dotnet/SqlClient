// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClient
{
    internal sealed class TdsParserStateObjectFactory
    {
        public static readonly TdsParserStateObjectFactory Singleton = new TdsParserStateObjectFactory();

        private const string UseManagedNetworkingOnWindows = "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows";

        private static bool shouldUseManagedSNI;

        // If the appcontext switch is set then Use Managed SNI based on the value. Otherwise Native SNI.dll will be used by default.
        public static bool UseManagedSNI { get; } =
            AppContext.TryGetSwitch(UseManagedNetworkingOnWindows, out shouldUseManagedSNI) ? shouldUseManagedSNI : false;

        public EncryptionOptions EncryptionOptions
        {
            get
            {
                return UseManagedSNI ? SNI.SNILoadHandle.SingletonInstance.Options : SNILoadHandle.SingletonInstance.Options;
            }
        }

        public uint SNIStatus
        {
            get
            {
                return UseManagedSNI ? SNI.SNILoadHandle.SingletonInstance.Status : SNILoadHandle.SingletonInstance.Status;
            }
        }

        public TdsParserStateObject CreateTdsParserStateObject(TdsParser parser)
        {
            if (UseManagedSNI)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParserStateObjectFactory.CreateTdsParserStateObject|INFO> Found AppContext switch '{0}' enabled, managed networking implementation will be used."
                   , UseManagedNetworkingOnWindows);
                return new TdsParserStateObjectManaged(parser);
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParserStateObjectFactory.CreateTdsParserStateObject|INFO> AppContext switch '{0}' not enabled, native networking implementation will be used."
                   , UseManagedNetworkingOnWindows);
                return new TdsParserStateObjectNative(parser);
            }
        }

        internal TdsParserStateObject CreateSessionObject(TdsParser tdsParser, TdsParserStateObject _pMarsPhysicalConObj, bool v)
        {
            if (TdsParserStateObjectFactory.UseManagedSNI)
            {
                return new TdsParserStateObjectManaged(tdsParser, _pMarsPhysicalConObj, true);
            }
            else
            {
                return new TdsParserStateObjectNative(tdsParser, _pMarsPhysicalConObj, true);
            }
        }
    }
}
