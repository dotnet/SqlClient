// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

#nullable enable

namespace Interop.Windows.Sni
{
    /// <summary>
    /// Managed mirror of the native <c>SNI_CERT_VALIDATION_INFO</c> structure filled by
    /// <c>SNIGetCertValidationInfoWrapper</c>: 24 bytes of sequential, blittable <c>DWORD</c>
    /// fields. The layout is fixed; a different structure would be exposed through a new export.
    /// </summary>
    /// <remarks>
    /// A field is meaningful only when its corresponding <see cref="SniCertInfoAvailability"/> flag is
    /// set in <see cref="dwAvailable"/>. The presented certificate itself is not part of this
    /// structure; when <see cref="SniCertInfoAvailability.Certificate"/> is set it is read with
    /// <c>SNIGetServerCertificateWrapper</c>.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SniCertValidationInfo
    {
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

        /// <summary>Out: IANA identifier of the negotiated TLS cipher suite.</summary>
        public uint dwCipherSuite;
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
        Certificate = 0x4,
        ValidationErrors = 0x8,
        ChainStatus = 0x10,
        HandshakeCompleted = 0x20,

        /// <summary>
        /// <see cref="SniCertValidationInfo.dwChainStatus"/> reflects only a leaf validity-period check
        /// for a pinned certificate; no chain was built, so the chain status is not reported.
        /// </summary>
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
