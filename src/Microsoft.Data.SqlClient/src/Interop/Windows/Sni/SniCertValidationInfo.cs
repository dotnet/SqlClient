// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Interop.Windows.Sni
{
    /// <summary>
    /// Managed mirror of the native <c>SNI_CERT_VALIDATION_INFO</c> structure filled by
    /// <c>SNIGetCertValidationInfoWrapper</c>. The layout is sequential and blittable: version 1 is
    /// 64 bytes of <c>DWORD</c> fields and a 32-byte hash buffer; version 2 appends
    /// <see cref="dwCipherSuite"/> for a total of 68 bytes.
    /// </summary>
    /// <remarks>
    /// A field is meaningful only when its corresponding <see cref="SniCertInfoAvailability"/> flag is
    /// set in <see cref="dwAvailable"/>. The caller requests version 2 by passing
    /// <see cref="cbSize"/> = <see cref="Size"/>; an SNI library that only knows version 1 writes
    /// <see cref="SizeV1"/> bytes, reports <see cref="dwVersion"/> = 1, and leaves the remaining
    /// bytes untouched.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct SniCertValidationInfo
    {
        /// <summary>Size, in bytes, of the version 1 structure.</summary>
        internal const int SizeV1 = 64;

        /// <summary>Size, in bytes, of the version 2 structure requested by this client.</summary>
        internal const int Size = 68;

        /// <summary>Length, in bytes, of a SHA-256 hash.</summary>
        internal const int HashLength = 32;

        /// <summary>Minimum structure version understood by this client.</summary>
        internal const uint MinimumVersion = 1;

        /// <summary>First structure version that carries <see cref="dwCipherSuite"/>.</summary>
        internal const uint CipherSuiteVersion = 2;

        /// <summary>In: size of the caller's buffer (at least <see cref="SizeV1"/>). Out: number of bytes written.</summary>
        public uint cbSize;

        /// <summary>Out: structure version.</summary>
        public uint dwVersion;

        /// <summary>Out: <see cref="SniCertInfoAvailability"/> flags.</summary>
        public uint dwAvailable;

        /// <summary>Out: <see cref="SniCertValidationMode"/> value.</summary>
        public uint dwValidationMode;

        /// <summary>Out: negotiated <c>SP_PROT_*</c> protocol bits.</summary>
        public uint dwProtocol;

        /// <summary>Out: effective validation error bits; identical to <c>SqlServerCertificateValidationErrors</c>.</summary>
        public uint dwValidationErrors;

        /// <summary>Out: raw <c>CERT_TRUST_STATUS.dwErrorStatus</c>; identical to <c>X509ChainStatusFlags</c>.</summary>
        public uint dwChainStatus;

        /// <summary>Out: number of valid bytes in <see cref="rgbHash"/> (32 or 0).</summary>
        public uint cbHash;

        /// <summary>Out: SHA-256 hash of the presented leaf certificate's DER encoding.</summary>
        public fixed byte rgbHash[HashLength];

        /// <summary>Out (version 2): IANA identifier of the negotiated TLS cipher suite.</summary>
        public uint dwCipherSuite;

        /// <summary>
        /// Creates an instance ready to be passed to native code, with <see cref="cbSize"/> set.
        /// </summary>
        internal static SniCertValidationInfo Create() => new SniCertValidationInfo { cbSize = Size };
    }

    /// <summary>
    /// Availability flags reported in <see cref="SniCertValidationInfo.dwAvailable"/>.
    /// </summary>
    [Flags]
    internal enum SniCertInfoAvailability : uint
    {
        None = 0x0,
        AttemptStarted = 0x1,
        Protocol = 0x2,
        Hash = 0x4,
        ValidationErrors = 0x8,
        ChainStatus = 0x10,
        HandshakeCompleted = 0x20,
        ChainStatusLeafTimeOnly = 0x40,
        CipherSuite = 0x80,
    }

    /// <summary>
    /// Validation mode reported in <see cref="SniCertValidationInfo.dwValidationMode"/>.
    /// </summary>
    internal enum SniCertValidationMode : uint
    {
        Unknown = 0,
        NotValidated = 1,
        SchannelAutomatic = 2,
        ChainPolicy = 3,
        PinnedCertificate = 4,
    }
}
