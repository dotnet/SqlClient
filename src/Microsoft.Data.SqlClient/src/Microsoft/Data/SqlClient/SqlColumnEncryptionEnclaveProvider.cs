// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/SqlColumnEncryptionEnclaveProvider/*'/>
    internal abstract class SqlColumnEncryptionEnclaveProvider
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetEnclaveSession/*'/>
        internal abstract void GetEnclaveSession(EnclaveSessionParameters enclaveSessionParameters, bool generateCustomData, bool isRetry, out SqlEnclaveSession sqlEnclaveSession, out long counter, out byte[] customData, out int customDataLength);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetAttestationParameters/*'/>
        internal abstract SqlEnclaveAttestationParameters GetAttestationParameters(string attestationUrl, byte[] customData, int customDataLength);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/CreateEnclaveSession/*'/>
        internal abstract void CreateEnclaveSession(byte[] enclaveAttestationInfo, ECDiffieHellman clientDiffieHellmanKey, EnclaveSessionParameters enclaveSessionParameters, byte[] customData, int customDataLength, out SqlEnclaveSession sqlEnclaveSession, out long counter);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/InvalidateEnclaveSession/*'/>
        internal abstract void InvalidateEnclaveSession(EnclaveSessionParameters enclaveSessionParameters, SqlEnclaveSession enclaveSession);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetEnclaveSessionAsync/*'/>
        internal virtual Task<(SqlEnclaveSession SqlEnclaveSession, long Counter, byte[] CustomData, int CustomDataLength)> GetEnclaveSessionAsync(
            EnclaveSessionParameters enclaveSessionParameters,
            bool generateCustomData,
            bool isRetry,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<(SqlEnclaveSession, long, byte[], int)>(cancellationToken);
            }

            try
            {
                GetEnclaveSession(
                    enclaveSessionParameters,
                    generateCustomData,
                    isRetry,
                    out SqlEnclaveSession sqlEnclaveSession,
                    out long counter,
                    out byte[] customData,
                    out int customDataLength);

                return Task.FromResult((sqlEnclaveSession, counter, customData, customDataLength));
            }
            catch (Exception e) when (ADP.IsCatchableExceptionType(e))
            {
                return Task.FromException<(SqlEnclaveSession, long, byte[], int)>(e);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/GetAttestationParametersAsync/*'/>
        internal virtual Task<SqlEnclaveAttestationParameters> GetAttestationParametersAsync(
            string attestationUrl,
            byte[] customData,
            int customDataLength,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<SqlEnclaveAttestationParameters>(cancellationToken);
            }

            try
            {
                return Task.FromResult(GetAttestationParameters(attestationUrl, customData, customDataLength));
            }
            catch (Exception e) when (ADP.IsCatchableExceptionType(e))
            {
                return Task.FromException<SqlEnclaveAttestationParameters>(e);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/CreateEnclaveSessionAsync/*'/>
        internal virtual Task<(SqlEnclaveSession SqlEnclaveSession, long Counter)> CreateEnclaveSessionAsync(
            byte[] enclaveAttestationInfo,
            ECDiffieHellman clientDiffieHellmanKey,
            EnclaveSessionParameters enclaveSessionParameters,
            byte[] customData,
            int customDataLength,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<(SqlEnclaveSession, long)>(cancellationToken);
            }

            try
            {
                CreateEnclaveSession(
                    enclaveAttestationInfo,
                    clientDiffieHellmanKey,
                    enclaveSessionParameters,
                    customData,
                    customDataLength,
                    out SqlEnclaveSession sqlEnclaveSession,
                    out long counter);

                return Task.FromResult((sqlEnclaveSession, counter));
            }
            catch (Exception e) when (ADP.IsCatchableExceptionType(e))
            {
                return Task.FromException<(SqlEnclaveSession, long)>(e);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionEnclaveProvider.xml' path='docs/members[@name="SqlColumnEncryptionEnclaveProvider"]/InvalidateEnclaveSessionAsync/*'/>
        internal virtual Task InvalidateEnclaveSessionAsync(
            EnclaveSessionParameters enclaveSessionParameters,
            SqlEnclaveSession enclaveSession,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            try
            {
                InvalidateEnclaveSession(enclaveSessionParameters, enclaveSession);
                return Task.CompletedTask;
            }
            catch (Exception e) when (ADP.IsCatchableExceptionType(e))
            {
                return Task.FromException(e);
            }
        }
    }
}
