// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.SNI;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient
{
    internal static partial class SNINativeMethodWrapper
    {
        private const string SNI = "Microsoft.Data.SqlClient.SNI.dll";

        internal enum SniSpecialErrors : uint
        {
            LocalDBErrorCode = SNICommon.LocalDBErrorCode,

            // multi-subnet-failover specific error codes
            MultiSubnetFailoverWithMoreThan64IPs = SNICommon.MultiSubnetFailoverWithMoreThan64IPs,
            MultiSubnetFailoverWithInstanceSpecified = SNICommon.MultiSubnetFailoverWithInstanceSpecified,
            MultiSubnetFailoverWithNonTcpProtocol = SNICommon.MultiSubnetFailoverWithNonTcpProtocol,

            // max error code value
            MaxErrorValue = SNICommon.MaxErrorValue
        }

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIServerEnumOpenWrapper")]
        internal static extern IntPtr SNIServerEnumOpen();

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIServerEnumCloseWrapper")]
        internal static extern void SNIServerEnumClose([In] IntPtr packet);

        [DllImport(SNI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SNIServerEnumReadWrapper")]
        internal static extern int SNIServerEnumRead([In] IntPtr packet, [In, Out] char[] readBuffer, int bufferLength, out bool more);

    }
}
