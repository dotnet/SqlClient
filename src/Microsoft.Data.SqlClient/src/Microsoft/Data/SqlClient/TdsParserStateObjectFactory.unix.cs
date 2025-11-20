// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET && _UNIX

using Microsoft.Data.SqlClient.ManagedSni;

namespace Microsoft.Data.SqlClient
{
    internal sealed class TdsParserStateObjectFactory
    {
        public static readonly TdsParserStateObjectFactory Singleton = new TdsParserStateObjectFactory();

        public EncryptionOptions EncryptionOptions => ManagedSni.SniLoadHandle.Options;

        public uint SNIStatus => ManagedSni.SniLoadHandle.Status;

        /// <summary>
        /// Verify client encryption possibility.
        /// </summary>
        public bool ClientOSEncryptionSupport => ManagedSni.SniLoadHandle.ClientOSEncryptionSupport;

        public TdsParserStateObject CreateTdsParserStateObject(TdsParser parser)
        {
            return new TdsParserStateObjectManaged(parser);
        }

        internal TdsParserStateObject CreateSessionObject(TdsParser tdsParser, TdsParserStateObject _pMarsPhysicalConObj, bool v)
        {
            return new TdsParserStateObjectManaged(tdsParser, _pMarsPhysicalConObj, true);
        }
    }
}

#endif
