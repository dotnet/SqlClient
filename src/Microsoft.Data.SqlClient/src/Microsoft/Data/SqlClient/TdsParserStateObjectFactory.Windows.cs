// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System;
using Microsoft.Data.SqlClient.ManagedSni;
#endif

namespace Microsoft.Data.SqlClient
{
    internal sealed class TdsParserStateObjectFactory
    {
        public static readonly TdsParserStateObjectFactory Singleton = new TdsParserStateObjectFactory();

        public EncryptionOptions EncryptionOptions =>
#if NET
            LocalAppContextSwitches.UseManagedNetworking ? ManagedSni.SniLoadHandle.Options : SNILoadHandle.SingletonInstance.Options;
#else
            SNILoadHandle.SingletonInstance.Options;
#endif

        public uint SNIStatus =>
#if NET
            LocalAppContextSwitches.UseManagedNetworking ? ManagedSni.SniLoadHandle.Status : SNILoadHandle.SingletonInstance.Status;
#else
            SNILoadHandle.SingletonInstance.Status;
#endif

        /// <summary>
        /// Verify client encryption possibility.
        /// </summary>
        public bool ClientOSEncryptionSupport =>
#if NET
            LocalAppContextSwitches.UseManagedNetworking ? ManagedSni.SniLoadHandle.ClientOSEncryptionSupport : SNILoadHandle.SingletonInstance.ClientOSEncryptionSupport;
#else
            SNILoadHandle.SingletonInstance.ClientOSEncryptionSupport;
#endif

        public TdsParserStateObject CreateTdsParserStateObject(TdsParser parser)
        {
#if NET
            if (LocalAppContextSwitches.UseManagedNetworking)
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectFactory.CreateTdsParserStateObject | Info | Using managed networking implementation.");
                return new TdsParserStateObjectManaged(parser);
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectFactory.CreateTdsParserStateObject | Info | Using native networking implementation.");
                return new TdsParserStateObjectNative(parser);
            }
#else
            return new TdsParserStateObjectNative(parser);
#endif
        }

        internal TdsParserStateObject CreateSessionObject(TdsParser tdsParser, TdsParserStateObject _pMarsPhysicalConObj, bool v)
        {
#if NET
            if (LocalAppContextSwitches.UseManagedNetworking)
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
