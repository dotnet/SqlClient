// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.NativeSni;

namespace Interop.Windows.Sni
{
    internal interface ISniNativeMethods
    {
        uint SniAddProvider(SNIHandle pConn, Provider provider, ref AuthProviderInfo pInfo);

        uint SniAddProvider(SNIHandle pConn, Provider provider, ref uint pInfo);

        uint SniCheckConnection(SNIHandle pConn);

        uint SniClose(IntPtr pConn);

        /// <summary>
        /// Calls <c>SNIGetCertValidationInfoWrapper</c>: reads the certificate validation details
        /// native SNI recorded for the most recent client TLS handshake on a connection.
        /// </summary>
        /// <param name="pConn">The SNI connection handle.</param>
        /// <param name="pInfo">
        /// Receives the details. A field is meaningful only when its
        /// <see cref="SniCertInfoAvailability"/> flag is set in
        /// <see cref="SniCertValidationInfo.dwAvailable"/>.
        /// </param>
        /// <returns>
        /// <c>ERROR_SUCCESS</c> (0) if <paramref name="pInfo"/> was filled; otherwise a Win32 error
        /// code, such as <c>ERROR_INVALID_PARAMETER</c> (87) for an invalid argument.
        /// </returns>
        /// <exception cref="EntryPointNotFoundException">
        /// The loaded SNI library does not export the function.
        /// </exception>
        uint SniGetCertValidationInfo(SNIHandle pConn, ref SniCertValidationInfo pInfo);

        uint SniGetMaxComposedSpnLength();

        uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out bool pbQueryInfo);

        uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out Guid pbQueryInfo);

        uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out Provider provider);

        uint SniGetInfoWrapper(SNIHandle pConn, QueryType queryType, out ushort portNumber);

        void SniGetLastError(out SniError pLastError);

        /// <summary>
        /// Calls <c>SNIGetServerCertificateWrapper</c>: copies the DER encoding of the leaf
        /// certificate the server presented during the most recent client TLS handshake on a
        /// connection.
        /// </summary>
        /// <param name="pConn">The SNI connection handle.</param>
        /// <param name="pbCertificate">
        /// The destination buffer, or <see langword="null"/> to query only the size.
        /// </param>
        /// <param name="cbCertificate">The size, in bytes, of <paramref name="pbCertificate"/>.</param>
        /// <param name="pcbCertificate">Receives the size, in bytes, of the certificate.</param>
        /// <returns>
        /// <c>ERROR_SUCCESS</c> (0) if the certificate was copied; <c>ERROR_INSUFFICIENT_BUFFER</c>
        /// (122) if the buffer is null or too small, with the required size in
        /// <paramref name="pcbCertificate"/>; <c>ERROR_NOT_FOUND</c> (1168) if no certificate was
        /// captured; or another Win32 error code.
        /// </returns>
        /// <exception cref="EntryPointNotFoundException">
        /// The loaded SNI library does not export the function.
        /// </exception>
        unsafe uint SniGetServerCertificate(SNIHandle pConn, byte* pbCertificate, uint cbCertificate, out uint pcbCertificate);

        uint SniGetPeerAddrStrWrapper(SNIHandle pConn, int bufferSize, StringBuilder addrBuffer, out uint addrLength);

        uint SniInitialize(IntPtr pmo);

        uint SniOpenSyncExWrapper(ref SniClientConsumerInfo pClientConsumerInfo, out IntPtr ppConn);

        uint SniOpenWrapper(
            ref SniConsumerInfo pConsumerInfo,
            string connect,
            SNIHandle pConn,
            out IntPtr ppConn,
            bool fSync,
            SqlConnectionIPAddressPreference ipPreference,
            ref SniDnsCacheInfo pDnsCacheInfo);

        IntPtr SniPacketAllocateWrapper(SafeHandle pConn, IoType ioType);

        uint SniPacketGetDataWrapper(IntPtr packet, byte[] readBuffer, uint readBufferLength, out uint dataSize);

        void SniPacketReset(SNIHandle pConn, IoType ioType, SNIPacket pPacket, ConsumerNumber consumer);

        void SniPacketRelease(IntPtr pPacket);

        unsafe void SniPacketSetData(SNIPacket pPacket, byte* pbBuffer, uint cbBuffer);

        uint SniQueryInfo(QueryType queryType, ref IntPtr pbQueryInfo);

        uint SniQueryInfo(QueryType queryType, ref uint pbQueryInfo);

        uint SniReadAsync(SNIHandle pConn, ref IntPtr ppNewPacket);

        uint SniReadSyncOverAsync(SNIHandle pConn, ref IntPtr ppNewPacket, int timeout);

        uint SniRemoveProvider(SNIHandle pConn, Provider provider);

        unsafe uint SniSecGenClientContextWrapper(
            SNIHandle pConn,
            byte* pIn,
            uint cbIn,
            byte* pOut,
            ref uint pcbOut,
            out bool pfDone,
            byte* szServerInfo,
            uint cbServerInfo,
            string pwszUserName,
            string pwszPassword);

        uint SniSecInitPackage(ref uint pcbMaxToken);

        void SniServerEnumClose(IntPtr packet);

        IntPtr SniServerEnumOpen();

        int SniServerEnumRead(IntPtr packet, char[] readBuffer, int bufferLength, out bool more);

        uint SniSetInfo(SNIHandle pConn, QueryType queryType, ref uint pbQueryInfo);

        uint SniTerminate();

        uint SniWaitForSslHandshakeToComplete(SNIHandle pConn, int dwMilliseconds, out SniSslProtocols pProtocolVersion);

        uint SniWriteAsyncWrapper(SNIHandle pConn, SNIPacket pPacket);

        uint SniWriteSyncOverAsync(SNIHandle pConn, SNIPacket pPacket);
    }
}
