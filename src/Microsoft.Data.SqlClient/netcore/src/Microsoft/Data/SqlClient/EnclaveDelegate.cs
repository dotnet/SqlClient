// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// A delegate for communicating with secure enclave
    /// </summary>
    internal partial class EnclaveDelegate
    {
        private static readonly SqlAeadAes256CbcHmac256Factory SqlAeadAes256CbcHmac256Factory = new SqlAeadAes256CbcHmac256Factory();
        private static readonly string GetAttestationInfoQueryString = String.Format(@"Select GetTrustedModuleIdentityAndAttestationInfo({0}) as attestationInfo", 0);
        private static readonly string ClassName = "EnclaveDelegate";
        private static readonly string GetDecryptedKeysToBeSentToEnclaveName = "GetDecryptedKeysToBeSentToEnclave";
        private static readonly string ComputeQueryStringHashName = "ComputeQueryStringHash";

        private readonly Object _lock = new Object();

        //singleton instance
        internal static EnclaveDelegate Instance { get; } = new EnclaveDelegate();

        private EnclaveDelegate() { }

        private byte[] GetUintBytes(string enclaveType, int intValue, string variableName)
        {
            try
            {
                uint attestationProtocol = Convert.ToUInt32(intValue);
                return BitConverter.GetBytes(attestationProtocol);
            }
            catch (Exception e)
            {
                throw SQL.InvalidAttestationParameterUnableToConvertToUnsignedInt(
                    variableName, intValue, enclaveType, e);
            }
        }

        /// <summary>
        /// Decrypt the keys that need to be sent to the enclave
        /// </summary>
        /// <param name="keysTobeSentToEnclave">Keys that need to sent to the enclave</param>
        /// <param name="serverName"></param>
        /// <returns></returns>
        private List<ColumnEncryptionKeyInfo> GetDecryptedKeysToBeSentToEnclave(Dictionary<int, SqlTceCipherInfoEntry> keysTobeSentToEnclave, string serverName)
        {
            List<ColumnEncryptionKeyInfo> decryptedKeysToBeSentToEnclave = new List<ColumnEncryptionKeyInfo>();

            foreach (SqlTceCipherInfoEntry cipherInfo in keysTobeSentToEnclave.Values)
            {
                SqlClientSymmetricKey sqlClientSymmetricKey = null;
                SqlEncryptionKeyInfo? encryptionkeyInfoChosen = null;
                SqlSecurityUtility.DecryptSymmetricKey(cipherInfo, serverName, out sqlClientSymmetricKey,
                    out encryptionkeyInfoChosen);

                if (sqlClientSymmetricKey == null)
                    throw SQL.NullArgumentInternal("sqlClientSymmetricKey", ClassName, GetDecryptedKeysToBeSentToEnclaveName);
                if (cipherInfo.ColumnEncryptionKeyValues == null)
                    throw SQL.NullArgumentInternal("ColumnEncryptionKeyValues", ClassName, GetDecryptedKeysToBeSentToEnclaveName);
                if (!(cipherInfo.ColumnEncryptionKeyValues.Count > 0))
                    throw SQL.ColumnEncryptionKeysNotFound();

                //cipherInfo.CekId is always 0, hence used cipherInfo.ColumnEncryptionKeyValues[0].cekId. Even when cek has multiple ColumnEncryptionKeyValues
                //the cekid and the plaintext value will remain the same, what varies is the encrypted cek value, since the cek can be encrypted by 
                //multiple CMKs
                decryptedKeysToBeSentToEnclave.Add(new ColumnEncryptionKeyInfo(sqlClientSymmetricKey.RootKey,
                    cipherInfo.ColumnEncryptionKeyValues[0].databaseId,
                    cipherInfo.ColumnEncryptionKeyValues[0].cekMdVersion, cipherInfo.ColumnEncryptionKeyValues[0].cekId));
            }
            return decryptedKeysToBeSentToEnclave;
        }

        /// <summary>
        /// Generate a byte package consisting of decrypted keys and some headers expected by the enclave
        /// </summary>
        /// <param name="enclaveSessionCounter">counter to avoid replay attacks</param>
        /// <param name="queryStringHashBytes"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        private byte[] GenerateBytePackageForKeys(long enclaveSessionCounter, byte[] queryStringHashBytes, List<ColumnEncryptionKeyInfo> keys)
        {

            //Format GUID | counter | queryStringHash | key[1]id | key[1]Bytes | ...... key[n]id | key[n]bytes
            Guid guid = Guid.NewGuid();
            byte[] guidBytes = guid.ToByteArray();
            byte[] counterBytes = BitConverter.GetBytes(enclaveSessionCounter);

            int lengthOfByteArrayToAllocate = guidBytes.Length;
            lengthOfByteArrayToAllocate += counterBytes.Length;
            lengthOfByteArrayToAllocate += queryStringHashBytes.Length;

            foreach (ColumnEncryptionKeyInfo key in keys)
            {
                lengthOfByteArrayToAllocate += key.GetLengthForSerialization();
            }

            byte[] bytePackage = new byte[lengthOfByteArrayToAllocate];
            int startOffset = 0;

            Buffer.BlockCopy(guidBytes, 0, bytePackage, startOffset, guidBytes.Length);
            startOffset += guidBytes.Length;

            Buffer.BlockCopy(counterBytes, 0, bytePackage, startOffset, counterBytes.Length);
            startOffset += counterBytes.Length;

            Buffer.BlockCopy(queryStringHashBytes, 0, bytePackage, startOffset, queryStringHashBytes.Length);
            startOffset += queryStringHashBytes.Length;

            foreach (ColumnEncryptionKeyInfo key in keys)
            {
                startOffset = key.SerializeToBuffer(bytePackage, startOffset);
            }

            return bytePackage;
        }

        /// <summary>
        /// Encrypt the byte package containing keys with the session key
        /// </summary>
        /// <param name="bytePackage">byte package containing keys</param>
        /// <param name="sessionKey">session key used to encrypt the package</param>
        /// <param name="serverName">server hosting the enclave</param>
        /// <returns></returns>
        private byte[] EncryptBytePackage(byte[] bytePackage, byte[] sessionKey, string serverName)
        {
            if (sessionKey == null)
                throw SQL.NullArgumentInternal("sessionKey", ClassName, "EncryptBytePackage");
            if (sessionKey.Length == 0)
                throw SQL.EmptyArgumentInternal("sessionKey", ClassName, "EncryptBytePackage");
            //bytePackage is created internally in this class and is guaranteed to be non null and non empty

            try
            {
                SqlClientSymmetricKey symmetricKey = new SqlClientSymmetricKey(sessionKey);
                SqlClientEncryptionAlgorithm sqlClientEncryptionAlgorithm =
                    SqlAeadAes256CbcHmac256Factory.Create(symmetricKey, SqlClientEncryptionType.Randomized,
                        SqlAeadAes256CbcHmac256Algorithm.AlgorithmName);
                return sqlClientEncryptionAlgorithm.EncryptData(bytePackage);
            }
            catch (Exception e)
            {
                throw SQL.FailedToEncryptRegisterRulesBytePackage(e);
            }
        }

        /// <summary>
        /// Combine the array of given byte arrays into one
        /// </summary>
        /// <param name="byteArraysToCombine">byte arrays to be combined</param>
        /// <returns></returns>
        private byte[] CombineByteArrays(byte[][] byteArraysToCombine)
        {
            byte[] combinedArray = new byte[byteArraysToCombine.Sum(ba => ba.Length)];
            int offset = 0;
            foreach (byte[] byteArray in byteArraysToCombine)
            {
                Buffer.BlockCopy(byteArray, 0, combinedArray, offset, byteArray.Length);
                offset += byteArray.Length;
            }
            return combinedArray;
        }

        private byte[] ComputeQueryStringHash(string queryString)
        {
            // Validate the input parameters
            if (string.IsNullOrWhiteSpace(queryString))
            {
                string argumentName = "queryString";
                if (null == queryString)
                {
                    throw SQL.NullArgumentInternal(argumentName, ClassName, ComputeQueryStringHashName);
                }
                else
                {
                    throw SQL.EmptyArgumentInternal(argumentName, ClassName, ComputeQueryStringHashName);
                }
            }

            byte[] queryStringBytes = Encoding.Unicode.GetBytes(queryString);

            // Compute hash 
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                sha256.TransformFinalBlock(queryStringBytes, 0, queryStringBytes.Length);
                hash = sha256.Hash;
            }
            return hash;
        }

        /// <summary>
        /// Exception when executing a enclave based Always Encrypted query 
        /// </summary>
        internal class RetryableEnclaveQueryExecutionException : Exception
        {
            internal RetryableEnclaveQueryExecutionException(string message, Exception innerException) : base(message, innerException) { }
        }
    }
}
