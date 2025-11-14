// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System.Threading;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// Global SNI settings and status
    /// </summary>
    internal class SniLoadHandle
    {
        public static readonly SniLoadHandle SingletonInstance = new SniLoadHandle();

        public ThreadLocal<SniError> _lastError = new ThreadLocal<SniError>(static () => new SniError(SniProviders.INVALID_PROV, 0, TdsEnums.SNI_SUCCESS, string.Empty));

        /// <summary>
        /// Last SNI error
        /// </summary>
        public SniError LastError
        {
            get
            {
                return _lastError.Value;
            }

            set
            {
                _lastError.Value = value;
            }
        }

        /// <summary>
        /// SNI library status
        /// </summary>
        public const uint Status = TdsEnums.SNI_SUCCESS;

        /// <summary>
        /// Encryption options setting
        /// </summary>
        public const EncryptionOptions Options = EncryptionOptions.OFF;

        /// <summary>
        /// Verify client encryption possibility
        /// </summary>
        // TODO: by adding support ENCRYPT_NOT_SUP, it could be calculated.
        public const bool ClientOSEncryptionSupport = true;
    }
}

#endif
