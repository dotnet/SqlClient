// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.ComponentModel;
using System.Net.Sockets;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// SNI error
    /// </summary>
    internal class SniError
    {
        // Error numbers from native SNI implementation
        // This is signed int representation of the error code 0x80090325 
        internal const int CertificateValidationErrorCode = -2146893019;

        public readonly SniProviders provider;
        public readonly string errorMessage;
        public readonly int nativeError;
        public readonly uint sniError;
        public readonly string function;
        public readonly uint lineNumber;
        public readonly Exception exception;

        public static SniError Success { get; } = new(SniProviders.INVALID_PROV, 0, TdsEnums.SNI_SUCCESS, string.Empty);

        public SniError(SniProviders provider, int nativeError, uint sniErrorCode, string errorMessage)
        {
            lineNumber = 0;
            function = string.Empty;
            this.provider = provider;
            this.nativeError = nativeError;
            sniError = sniErrorCode;
            this.errorMessage = errorMessage;
            exception = null;
        }

        public SniError(SniProviders provider, uint sniErrorCode, Exception sniException, int nativeErrorCode = 0)
        {
            lineNumber = 0;
            function = string.Empty;
            this.provider = provider;
            nativeError = nativeErrorCode;
            if (nativeErrorCode == 0)
            {
                if (sniException is SocketException socketException)
                {
                    // SocketErrorCode values are cross-plat consistent in .NET (matching native Windows error codes)
                    // underlying type of SocketErrorCode is int
                    nativeError = (int)socketException.SocketErrorCode;
                }
                else if (sniException is Win32Exception win32Exception)
                {
                    nativeError = win32Exception.NativeErrorCode; // Replicates native SNI behavior
                }
            }
            sniError = sniErrorCode;
            errorMessage = string.Empty;
            exception = sniException;
        }
    }
}

#endif
