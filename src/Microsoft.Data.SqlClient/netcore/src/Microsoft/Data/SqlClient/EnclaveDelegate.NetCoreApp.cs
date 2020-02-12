// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// A delegate for communicating with secure enclave
    /// </summary>
    internal partial class EnclaveDelegate
    {
        private static readonly string GetSerializedAttestationParametersName = "GetSerializedAttestationParameters";

        private static Dictionary<SqlConnectionAttestationProtocol, SqlColumnEncryptionEnclaveProvider> EnclaveProviders = new Dictionary<SqlConnectionAttestationProtocol, SqlColumnEncryptionEnclaveProvider>();

        internal byte[] GetSerializedAttestationParameters(SqlEnclaveAttestationParameters sqlEnclaveAttestationParameters, string enclaveType)
        {
            byte[] attestationProtocolBytes = null;
            byte[] attestationProtocolInputLengthBytes = null;
            byte[] clientDHPublicKeyLengthBytes = null;
            int attestationProtocolInt = sqlEnclaveAttestationParameters.Protocol;

            // attestation protocol
            attestationProtocolBytes = GetUintBytes(enclaveType, attestationProtocolInt, "attestationProtocol");

            if (attestationProtocolBytes == null)
            {
                throw SQL.NullArgumentInternal("attestationProtocolBytes", ClassName, GetSerializedAttestationParametersName);
            }

            // attestationProtocolInput
            byte[] attestationProtocolInputBytes = sqlEnclaveAttestationParameters.GetInput();

            // attestationProtocolInput length
            attestationProtocolInputLengthBytes = GetUintBytes(enclaveType, attestationProtocolInputBytes.Length, "attestationProtocolInputLength");

            if (attestationProtocolInputLengthBytes == null)
            {
                throw SQL.NullArgumentInternal("attestationProtocolInputLengthBytes", ClassName, GetSerializedAttestationParametersName);
            }

            // clientDHPublicKey
            byte[] clientDHPublicKey = sqlEnclaveAttestationParameters.ClientDiffieHellmanKey.Key.Export(CngKeyBlobFormat.EccPublicBlob);

            // clientDHPublicKey length
            clientDHPublicKeyLengthBytes = GetUintBytes(enclaveType, clientDHPublicKey.Length, "clientDHPublicKeyLength");

            if (clientDHPublicKeyLengthBytes == null)
            {
                throw SQL.NullArgumentInternal("clientDHPublicKeyLengthBytes", ClassName, GetSerializedAttestationParametersName);
            }

            return CombineByteArrays(new[] { attestationProtocolBytes, attestationProtocolInputLengthBytes, attestationProtocolInputBytes, clientDHPublicKeyLengthBytes, clientDHPublicKey });
        }

        /// <summary>
        /// Create a new enclave session
        /// </summary>
        /// <param name="attestationProtocol">attestation protocol</param>
        /// <param name="enclaveType">enclave type</param>
        /// <param name="serverName">servername</param>
        /// <param name="attestationUrl">attestation url for attestation service endpoint</param>
        /// <param name="attestationInfo">attestation info from SQL Server</param>
        /// <param name="attestationParameters">attestation parameters</param>
        /// <param name="customData">A set of extra data needed for attestating the enclave.</param>
        /// <param name="customDataLength">The length of the extra data needed for attestating the enclave.</param>
        internal void CreateEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, string serverName, string attestationUrl,
            byte[] attestationInfo, SqlEnclaveAttestationParameters attestationParameters, byte[] customData, int customDataLength)
        {

            lock (_lock)
            {
                SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = GetEnclaveProvider(attestationProtocol, enclaveType);
                long counter;
                SqlEnclaveSession sqlEnclaveSession = null;
                byte[] dummyCustomData = null;
                int dummyCustomDataLength;

                sqlColumnEncryptionEnclaveProvider.GetEnclaveSession(serverName, attestationUrl, false, out sqlEnclaveSession, out counter, out dummyCustomData, out dummyCustomDataLength);

                if (sqlEnclaveSession != null)
                {
                    return;
                }

                sqlColumnEncryptionEnclaveProvider.CreateEnclaveSession(attestationInfo, attestationParameters.ClientDiffieHellmanKey, attestationUrl, serverName, customData, customDataLength, out sqlEnclaveSession, out counter);

                if (sqlEnclaveSession == null) 
                {
                    throw SQL.NullEnclaveSessionReturnedFromProvider(enclaveType, attestationUrl);
                }
            }
        }

        /// <summary>
        /// Generate the byte package that needs to be sent to the enclave
        /// </summary>
        /// <param name="attestationProtocol">attestation protocol</param>
        /// <param name="keysTobeSentToEnclave">Keys to be sent to enclave</param>
        /// <param name="queryText"></param>
        /// <param name="enclaveType">enclave type</param>
        /// <param name="serverName">server name</param>
        /// <param name="enclaveAttestationUrl">url for attestation endpoint</param>
        /// <returns></returns>
        internal EnclavePackage GenerateEnclavePackage(SqlConnectionAttestationProtocol attestationProtocol, Dictionary<int, SqlTceCipherInfoEntry> keysTobeSentToEnclave, string queryText, string enclaveType, string serverName, string enclaveAttestationUrl)
        {

            SqlEnclaveSession sqlEnclaveSession = null;
            long counter;
            byte[] dummyCustomData = null;
            int dummyCustomDataLength;

            try
            {
                GetEnclaveSession(attestationProtocol, enclaveType, serverName, enclaveAttestationUrl, false, out sqlEnclaveSession, out counter, out dummyCustomData, out dummyCustomDataLength, throwIfNull: true);
            }
            catch (Exception e)
            {
                throw new RetryableEnclaveQueryExecutionException(e.Message, e);
            }

            List<ColumnEncryptionKeyInfo> decryptedKeysToBeSentToEnclave = GetDecryptedKeysToBeSentToEnclave(keysTobeSentToEnclave, serverName);
            byte[] queryStringHashBytes = ComputeQueryStringHash(queryText);
            byte[] keyBytePackage = GenerateBytePackageForKeys(counter, queryStringHashBytes, decryptedKeysToBeSentToEnclave);
            byte[] sessionKey = sqlEnclaveSession.GetSessionKey();
            byte[] encryptedBytePackage = EncryptBytePackage(keyBytePackage, sessionKey, serverName);
            byte[] enclaveSessionHandle = BitConverter.GetBytes(sqlEnclaveSession.SessionId);
            byte[] byteArrayToBeSentToEnclave = CombineByteArrays(new[] { enclaveSessionHandle, encryptedBytePackage });
            return new EnclavePackage(byteArrayToBeSentToEnclave, sqlEnclaveSession);
        }

        internal void InvalidateEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, string serverName, string EnclaveAttestationUrl, SqlEnclaveSession enclaveSession)
        {
            SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = GetEnclaveProvider(attestationProtocol, enclaveType);
            sqlColumnEncryptionEnclaveProvider.InvalidateEnclaveSession(serverName, EnclaveAttestationUrl, enclaveSession);
        }

        
        internal SqlEnclaveAttestationParameters GetAttestationParameters(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, string attestationUrl, byte[] customData, int customDataLength)
        {
            SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = GetEnclaveProvider(attestationProtocol, enclaveType);
            return sqlColumnEncryptionEnclaveProvider.GetAttestationParameters(attestationUrl, customData, customDataLength);
        }

        private SqlColumnEncryptionEnclaveProvider GetEnclaveProvider(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType)
        {
            SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = null;

            if (!EnclaveProviders.TryGetValue(attestationProtocol, out sqlColumnEncryptionEnclaveProvider))
            {
                switch (attestationProtocol)
                {
                    case SqlConnectionAttestationProtocol.AAS:
                        AzureAttestationEnclaveProvider azureAttestationEnclaveProvider = new AzureAttestationEnclaveProvider();
                        EnclaveProviders[attestationProtocol] = (SqlColumnEncryptionEnclaveProvider)azureAttestationEnclaveProvider;
                        sqlColumnEncryptionEnclaveProvider = EnclaveProviders[attestationProtocol];
                        break;

                    case SqlConnectionAttestationProtocol.HGS:
                        HostGuardianServiceEnclaveProvider hostGuardianServiceEnclaveProvider = new HostGuardianServiceEnclaveProvider();
                        EnclaveProviders[attestationProtocol] = (SqlColumnEncryptionEnclaveProvider)hostGuardianServiceEnclaveProvider;
                        sqlColumnEncryptionEnclaveProvider = EnclaveProviders[attestationProtocol];
                        break;

#if ENCLAVE_SIMULATOR
                    case SqlConnectionAttestationProtocol.SIM:
                        SimulatorEnclaveProvider simulatorEnclaveProvider = new SimulatorEnclaveProvider();
                        EnclaveProviders[attestationProtocol] = (SqlColumnEncryptionEnclaveProvider)simulatorEnclaveProvider;
                        sqlColumnEncryptionEnclaveProvider = EnclaveProviders[attestationProtocol];
                        break;
#endif

                    default:
                        break;
                }
            }

            if (sqlColumnEncryptionEnclaveProvider == null)
            {
                throw SQL.EnclaveProviderNotFound(enclaveType, ConvertAttestationProtocolToString(attestationProtocol));
            }

            return sqlColumnEncryptionEnclaveProvider;
        }

        private string ConvertAttestationProtocolToString(SqlConnectionAttestationProtocol attestationProtocol)
        {
            switch (attestationProtocol)
            {
                case SqlConnectionAttestationProtocol.AAS:
                    return "AAS";

                case SqlConnectionAttestationProtocol.HGS:
                    return "HGS";

#if ENCLAVE_SIMULATOR
                case SqlConnectionAttestationProtocol.SIM:
                    return "SIM";
#endif

                default:
                    return "NotSpecified";
            }
        }

        internal void GetEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, string serverName, string enclaveAttestationUrl, bool generateCustomData, out SqlEnclaveSession sqlEnclaveSession, out byte[] customData, out int customDataLength)
        {
            long counter;
            GetEnclaveSession(attestationProtocol, enclaveType, serverName, enclaveAttestationUrl, generateCustomData, out sqlEnclaveSession, out counter, out customData, out customDataLength, throwIfNull: false);
        }

        private void GetEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, string serverName, string enclaveAttestationUrl, bool generateCustomData, out SqlEnclaveSession sqlEnclaveSession, out long counter, out byte[] customData, out int customDataLength, bool throwIfNull)
        {
            SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = GetEnclaveProvider(attestationProtocol, enclaveType);
            sqlColumnEncryptionEnclaveProvider.GetEnclaveSession(serverName, enclaveAttestationUrl, generateCustomData, out sqlEnclaveSession, out counter, out customData, out customDataLength);

            if (throwIfNull && sqlEnclaveSession == null)
            {
                throw SQL.NullEnclaveSessionDuringQueryExecution(enclaveType, enclaveAttestationUrl);
            }
        }
    }
}
