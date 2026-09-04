// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class EnclaveDelegate
    {
        private static readonly ConcurrentDictionary<SqlConnectionAttestationProtocol, SqlColumnEncryptionEnclaveProvider> s_enclaveProviders = new();

        /// <summary>
        /// Create a new enclave session
        /// </summary>
        /// <param name="attestationProtocol">attestation protocol</param>
        /// <param name="enclaveType">enclave type</param>
        /// <param name="enclaveSessionParameters">The set of parameters required for enclave session.</param>
        /// <param name="attestationInfo">attestation info from SQL Server</param>
        /// <param name="attestationParameters">attestation parameters</param>
        /// <param name="customData">A set of extra data needed for attesting the enclave.</param>
        /// <param name="customDataLength">The length of the extra data needed for attesting the enclave.</param>
        /// <param name="isRetry">Indicates if this is a retry from a failed call.</param>
        internal void CreateEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, EnclaveSessionParameters enclaveSessionParameters,
            byte[] attestationInfo, SqlEnclaveAttestationParameters attestationParameters, byte[] customData, int customDataLength, bool isRetry)
        {
            lock (_lock)
            {
                SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = GetEnclaveProvider(attestationProtocol, enclaveType);

                sqlColumnEncryptionEnclaveProvider.GetEnclaveSession(
                    enclaveSessionParameters,
                    generateCustomData: false,
                    isRetry: isRetry,
                    sqlEnclaveSession: out SqlEnclaveSession sqlEnclaveSession,
                    counter: out _,
                    customData: out _,
                    customDataLength: out _
                );

                if (sqlEnclaveSession != null)
                {
                    return;
                }

                sqlColumnEncryptionEnclaveProvider.CreateEnclaveSession(
                    attestationInfo,
                    attestationParameters.ClientDiffieHellmanKey,
                    enclaveSessionParameters,
                    customData,
                    customDataLength,
                    out sqlEnclaveSession,
                    counter: out _
                );

                if (sqlEnclaveSession == null)
                {
                    throw SQL.NullEnclaveSessionReturnedFromProvider(enclaveType, enclaveSessionParameters.AttestationUrl);
                }
            }
        }

        internal void GetEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, EnclaveSessionParameters enclaveSessionParameters, bool generateCustomData, bool isRetry, out SqlEnclaveSession sqlEnclaveSession, out byte[] customData, out int customDataLength)
        {
            GetEnclaveSession(attestationProtocol, enclaveType, enclaveSessionParameters, generateCustomData, isRetry, out sqlEnclaveSession, out _, out customData, out customDataLength, throwIfNull: false);
        }

        private void GetEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, EnclaveSessionParameters enclaveSessionParameters, bool generateCustomData, bool isRetry, out SqlEnclaveSession sqlEnclaveSession, out long counter, out byte[] customData, out int customDataLength, bool throwIfNull)
        {
            SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = GetEnclaveProvider(attestationProtocol, enclaveType);
            sqlColumnEncryptionEnclaveProvider.GetEnclaveSession(enclaveSessionParameters, generateCustomData, isRetry, out sqlEnclaveSession, out counter, out customData, out customDataLength);

            if (throwIfNull && sqlEnclaveSession == null)
            {
                throw SQL.NullEnclaveSessionDuringQueryExecution(enclaveType, enclaveSessionParameters.AttestationUrl);
            }
        }

        internal void InvalidateEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, EnclaveSessionParameters enclaveSessionParameters, SqlEnclaveSession enclaveSession)
        {
            SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = GetEnclaveProvider(attestationProtocol, enclaveType);
            sqlColumnEncryptionEnclaveProvider.InvalidateEnclaveSession(enclaveSessionParameters, enclaveSession);
        }


        private SqlColumnEncryptionEnclaveProvider GetEnclaveProvider(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType)
        {
            if (!s_enclaveProviders.TryGetValue(attestationProtocol, out SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider))
            {
                switch (attestationProtocol)
                {
                    case SqlConnectionAttestationProtocol.AAS:
                        sqlColumnEncryptionEnclaveProvider = new AzureAttestationEnclaveProvider();
                        s_enclaveProviders[attestationProtocol] = sqlColumnEncryptionEnclaveProvider;
                        break;

                    case SqlConnectionAttestationProtocol.HGS:
                        sqlColumnEncryptionEnclaveProvider = new HostGuardianServiceEnclaveProvider();
                        s_enclaveProviders[attestationProtocol] = sqlColumnEncryptionEnclaveProvider;
                        break;

                    case SqlConnectionAttestationProtocol.None:
                        sqlColumnEncryptionEnclaveProvider = new NoneAttestationEnclaveProvider();
                        s_enclaveProviders[attestationProtocol] = sqlColumnEncryptionEnclaveProvider;
                        break;

                    default:
                        break;
                }
            }

            if (sqlColumnEncryptionEnclaveProvider == null)
            {
                throw SQL.EnclaveProviderNotFound(enclaveType, attestationProtocol.ToString());
            }

            return sqlColumnEncryptionEnclaveProvider;
        }

        /// <summary>
        /// Generate the byte package that needs to be sent to the enclave
        /// </summary>
        /// <param name="attestationProtocol">attestation protocol</param>
        /// <param name="keysToBeSentToEnclave">Keys to be sent to enclave</param>
        /// <param name="queryText"></param>
        /// <param name="enclaveType">enclave type</param>
        /// <param name="enclaveSessionParameters">The set of parameters required for enclave session.</param>
        /// <param name="connection">connection executing the query</param>
        /// <param name="command">command executing the query</param>
        /// <returns></returns>
        internal EnclavePackage GenerateEnclavePackage(SqlConnectionAttestationProtocol attestationProtocol, ConcurrentDictionary<int, SqlTceCipherInfoEntry> keysToBeSentToEnclave, string queryText, string enclaveType, EnclaveSessionParameters enclaveSessionParameters, SqlConnection connection, SqlCommand command)
        {
            SqlEnclaveSession sqlEnclaveSession;
            long counter;

            try
            {
                GetEnclaveSession(
                    attestationProtocol,
                    enclaveType,
                    enclaveSessionParameters,
                    generateCustomData: false,
                    isRetry: false,
                    sqlEnclaveSession: out sqlEnclaveSession,
                    counter: out counter,
                    customData: out _,
                    customDataLength: out _,
                    throwIfNull: true
                );
            }
            catch (Exception e)
            {
                throw new RetryableEnclaveQueryExecutionException(e.Message, e);
            }

            List<ColumnEncryptionKeyInfo> decryptedKeysToBeSentToEnclave = GetDecryptedKeysToBeSentToEnclave(keysToBeSentToEnclave, connection, command);
            return BuildEnclavePackage(decryptedKeysToBeSentToEnclave, queryText, counter, sqlEnclaveSession, enclaveSessionParameters.ServerName);
        }

        /// <summary>
        /// Asynchronously encrypts the byte package containing keys with the session key.
        /// </summary>
        /// <remarks>
        /// Async counterpart of <see cref="GenerateEnclavePackage"/>. Only the column encryption key decryption
        /// step performs I/O, so only that step is awaited; the remaining work is local cryptography and is
        /// performed inline.
        /// </remarks>
        /// <param name="attestationProtocol">attestation protocol</param>
        /// <param name="keysToBeSentToEnclave">Keys to be sent to enclave</param>
        /// <param name="queryText">Text of the query being executed</param>
        /// <param name="enclaveType">enclave type</param>
        /// <param name="enclaveSessionParameters">The set of parameters required for enclave session.</param>
        /// <param name="connection">connection executing the query</param>
        /// <param name="command">command executing the query</param>
        /// <param name="cancellationToken">Token used to request cancellation of the operation</param>
        internal async Task<EnclavePackage> GenerateEnclavePackageAsync(
            SqlConnectionAttestationProtocol attestationProtocol,
            ConcurrentDictionary<int, SqlTceCipherInfoEntry> keysToBeSentToEnclave,
            string queryText,
            string enclaveType,
            EnclaveSessionParameters enclaveSessionParameters,
            SqlConnection connection,
            SqlCommand command,
            CancellationToken cancellationToken)
        {
            SqlEnclaveSession sqlEnclaveSession;
            long counter;

            try
            {
                // @TODO: GetEnclaveSession is still synchronous. On a session cache hit it is pure in-memory
                // work, but on a miss it performs blocking attestation HTTP. Making it awaitable requires the
                // async enclave provider hierarchy (Phase 3 of the async Always Encrypted spec), which adds
                // public virtual API and therefore ships separately. Until then this is the one remaining
                // blocking call on the asynchronous Always Encrypted path.
                GetEnclaveSession(
                    attestationProtocol,
                    enclaveType,
                    enclaveSessionParameters,
                    generateCustomData: false,
                    isRetry: false,
                    sqlEnclaveSession: out sqlEnclaveSession,
                    counter: out counter,
                    customData: out _,
                    customDataLength: out _,
                    throwIfNull: true
                );
            }
            catch (Exception e)
            {
                throw new RetryableEnclaveQueryExecutionException(e.Message, e);
            }

            List<ColumnEncryptionKeyInfo> decryptedKeysToBeSentToEnclave = await GetDecryptedKeysToBeSentToEnclaveAsync(
                    keysToBeSentToEnclave,
                    connection,
                    command,
                    cancellationToken)
                .ConfigureAwait(false);

            return BuildEnclavePackage(decryptedKeysToBeSentToEnclave, queryText, counter, sqlEnclaveSession, enclaveSessionParameters.ServerName);
        }

        /// <summary>
        /// Assembles the enclave package from already-decrypted column encryption keys.
        /// </summary>
        /// <remarks>
        /// Shared by the synchronous and asynchronous package generation paths. All work here is local
        /// cryptography, so there is no asynchronous counterpart.
        /// </remarks>
        private EnclavePackage BuildEnclavePackage(
            List<ColumnEncryptionKeyInfo> decryptedKeysToBeSentToEnclave,
            string queryText,
            long counter,
            SqlEnclaveSession sqlEnclaveSession,
            string serverName)
        {
            byte[] queryStringHashBytes = ComputeQueryStringHash(queryText);
            byte[] keyBytePackage = GenerateBytePackageForKeys(counter, queryStringHashBytes, decryptedKeysToBeSentToEnclave);
            byte[] sessionKey = sqlEnclaveSession.GetSessionKey();
            byte[] encryptedBytePackage = EncryptBytePackage(keyBytePackage, sessionKey, serverName);
            byte[] enclaveSessionHandle = BitConverter.GetBytes(sqlEnclaveSession.SessionId);
            byte[] byteArrayToBeSentToEnclave = CombineByteArrays(enclaveSessionHandle, encryptedBytePackage);
            return new EnclavePackage(byteArrayToBeSentToEnclave, sqlEnclaveSession);
        }


        internal SqlEnclaveAttestationParameters GetAttestationParameters(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, string attestationUrl, byte[] customData, int customDataLength)
        {
            SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = GetEnclaveProvider(attestationProtocol, enclaveType);
            return sqlColumnEncryptionEnclaveProvider.GetAttestationParameters(attestationUrl, customData, customDataLength);
        }

        internal byte[] GetSerializedAttestationParameters(SqlEnclaveAttestationParameters sqlEnclaveAttestationParameters, string enclaveType)
        {
            byte[] attestationProtocolBytes = null;
            byte[] attestationProtocolInputLengthBytes = null;
            byte[] clientDHPublicKeyLengthBytes = null;
            int attestationProtocolInt = sqlEnclaveAttestationParameters.Protocol;

            attestationProtocolBytes = GetUintBytes(enclaveType, attestationProtocolInt, "attestationProtocol");

            if (attestationProtocolBytes == null)
            {
                throw SQL.NullArgumentInternal(nameof(attestationProtocolBytes), nameof(EnclaveDelegate), nameof(GetSerializedAttestationParameters));
            }

            byte[] attestationProtocolInputBytes = sqlEnclaveAttestationParameters.GetInput();

            attestationProtocolInputLengthBytes = GetUintBytes(enclaveType, attestationProtocolInputBytes.Length, "attestationProtocolInputLength");

            if (attestationProtocolInputLengthBytes == null)
            {
                throw SQL.NullArgumentInternal(nameof(attestationProtocolInputLengthBytes), nameof(EnclaveDelegate), nameof(GetSerializedAttestationParameters));
            }

            byte[] clientDHPublicKey = KeyConverter.GetECDiffieHellmanPublicKeyBlob(sqlEnclaveAttestationParameters.ClientDiffieHellmanKey);

            clientDHPublicKeyLengthBytes = GetUintBytes(enclaveType, clientDHPublicKey.Length, "clientDHPublicKeyLength");

            if (clientDHPublicKeyLengthBytes == null)
            {
                throw SQL.NullArgumentInternal(nameof(clientDHPublicKeyLengthBytes), nameof(EnclaveDelegate), nameof(GetSerializedAttestationParameters));
            }

            return CombineByteArrays(attestationProtocolBytes, attestationProtocolInputLengthBytes, attestationProtocolInputBytes, clientDHPublicKeyLengthBytes, clientDHPublicKey);
        }
    }
}
