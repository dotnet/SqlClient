// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Interop.Windows.Sni;

namespace Microsoft.Data.SqlClient
{
    internal class TdsParserStateObjectNative : TdsParserStateObject
    {
        internal TdsParserStateObjectNative(TdsParser parser, TdsParserStateObject physicalConnection, bool async)
            : base(parser, physicalConnection.Handle, async)
        {
        }

        internal TdsParserStateObjectNative(TdsParser parser)
            : base(parser)
        {
        }

        ////////////////
        // Properties //
        ////////////////

        internal override uint Status => _sessionHandle != null ? _sessionHandle.Status : TdsEnums.SNI_UNINITIALIZED;

        internal override SessionHandle SessionHandle => SessionHandle.FromNativeHandle(_sessionHandle);

        internal override Guid? SessionId => default;

        internal override uint SniGetConnectionId(ref Guid clientConnectionId)
            => SniNativeWrapper.SniGetConnectionId(Handle, ref clientConnectionId);

        internal override uint DisableSsl()
            => SniNativeWrapper.SniRemoveProvider(Handle, Provider.SSL_PROV);

        internal override uint EnableMars(ref uint info)
            => SniNativeWrapper.SniAddProvider(Handle, Provider.SMUX_PROV, ref info);

        internal override uint SetConnectionBufferSize(ref uint unsignedPacketSize)
            => SniNativeWrapper.SniSetInfo(Handle, QueryType.SNI_QUERY_CONN_BUFSIZE, ref unsignedPacketSize);

        internal override SspiContextProvider CreateSspiContextProvider() => new NativeSspiContextProvider();
    }
}
