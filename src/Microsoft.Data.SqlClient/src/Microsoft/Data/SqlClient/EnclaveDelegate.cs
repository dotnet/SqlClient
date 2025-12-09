// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// A delegate for communicating with secure enclave
    /// </summary>
    // @TODO: This isn't a delegate... it's a utility class
    internal sealed partial class EnclaveDelegate
    {
        private static readonly SqlAeadAes256CbcHmac256Factory s_sqlAeadAes256CbcHmac256Factory = new SqlAeadAes256CbcHmac256Factory();
        private static readonly EnclaveDelegate s_enclaveDelegate = new EnclaveDelegate();

        private readonly object _lock;

        public static EnclaveDelegate Instance => s_enclaveDelegate;

        private EnclaveDelegate()
        {
            _lock = new object();
        }

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
        /// <param name="connection"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        private List<ColumnEncryptionKeyInfo> GetDecryptedKeysToBeSentToEnclave(ConcurrentDictionary<int, SqlTceCipherInfoEntry> keysTobeSentToEnclave, string serverName, SqlConnection connection, SqlCommand command)
        {
            List<ColumnEncryptionKeyInfo> decryptedKeysToBeSentToEnclave = new List<ColumnEncryptionKeyInfo>();

            foreach (SqlTceCipherInfoEntry cipherInfo in keysTobeSentToEnclave.Values)
            {
                SqlSecurityUtility.DecryptSymmetricKey(cipherInfo, out SqlClientSymmetricKey sqlClientSymmetricKey, out SqlEncryptionKeyInfo encryptionkeyInfoChosen, connection, command);

                if (sqlClientSymmetricKey == null)
                {
                    throw SQL.NullArgumentInternal(nameof(sqlClientSymmetricKey), nameof(EnclaveDelegate), nameof(GetDecryptedKeysToBeSentToEnclave));
                }
                if (cipherInfo.ColumnEncryptionKeyValues == null)
                {
                    throw SQL.NullArgumentInternal(nameof(cipherInfo.ColumnEncryptionKeyValues), nameof(EnclaveDelegate), nameof(GetDecryptedKeysToBeSentToEnclave));
                }
                if (!(cipherInfo.ColumnEncryptionKeyValues.Count > 0))
                {
                    throw SQL.ColumnEncryptionKeysNotFound();
                }

                //cipherInfo.CekId is always 0, hence used cipherInfo.ColumnEncryptionKeyValues[0].cekId. Even when cek has multiple ColumnEncryptionKeyValues
                //the cekid and the plaintext value will remain the same, what varies is the encrypted cek value, since the cek can be encrypted by 
                //multiple CMKs
                decryptedKeysToBeSentToEnclave.Add(
                    new ColumnEncryptionKeyInfo(
                        sqlClientSymmetricKey.RootKey,
                        cipherInfo.ColumnEncryptionKeyValues[0].databaseId,
                        cipherInfo.ColumnEncryptionKeyValues[0].cekMdVersion,
                        cipherInfo.ColumnEncryptionKeyValues[0].cekId
                    )
                );
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
            {
                throw SQL.NullArgumentInternal(nameof(sessionKey), nameof(EnclaveDelegate), nameof(EncryptBytePackage));
            }
            if (sessionKey.Length == 0)
            {
                throw SQL.EmptyArgumentInternal(nameof(sessionKey), nameof(EnclaveDelegate), nameof(EncryptBytePackage));
            }
            //bytePackage is created internally in this class and is guaranteed to be non null and non empty

            try
            {
                SqlClientSymmetricKey symmetricKey = new SqlClientSymmetricKey(sessionKey);
                SqlClientEncryptionAlgorithm sqlClientEncryptionAlgorithm = s_sqlAeadAes256CbcHmac256Factory.Create(
                    symmetricKey,
                    SqlClientEncryptionType.Randomized,
                    SqlAeadAes256CbcHmac256Algorithm.AlgorithmName
                );
                return sqlClientEncryptionAlgorithm.EncryptData(bytePackage);
            }
            catch (Exception e)
            {
                throw SQL.FailedToEncryptRegisterRulesBytePackage(e);
            }
        }

        private byte[] CombineByteArrays(byte[] arr1, byte[] arr2)
        {
            // this complication avoids the usless allocation of a byte[][] to hold args
            // it would be easier with spans so revisit if System.Memory is now a standard include
            int length = arr1.Length + arr2.Length;
            byte[] combinedArray = new byte[length];

            Buffer.BlockCopy(arr1, 0, combinedArray, 0, arr1.Length);
            Buffer.BlockCopy(arr2, 0, combinedArray, arr1.Length, arr2.Length);

            return combinedArray;
        }

        private byte[] CombineByteArrays(byte[] arr1, byte[] arr2, byte[] arr3, byte[] arr4, byte[] arr5)
        {
            // this complication avoids the usless allocation of a byte[][] to hold args
            // it would be easier with spans so revisit if System.Memory is now a standard include
            int length = arr1.Length + arr2.Length + arr3.Length + arr4.Length + arr5.Length;
            byte[] combinedArray = new byte[length];

            Buffer.BlockCopy(arr1, 0, combinedArray, 0, arr1.Length);
            int copied = arr1.Length;
            Buffer.BlockCopy(arr2, 0, combinedArray, copied, arr2.Length);
            copied += arr2.Length;
            Buffer.BlockCopy(arr3, 0, combinedArray, copied, arr3.Length);
            copied += arr3.Length;
            Buffer.BlockCopy(arr4, 0, combinedArray, copied, arr4.Length);
            copied += arr4.Length;
            Buffer.BlockCopy(arr5, 0, combinedArray, copied, arr5.Length);

            return combinedArray;
        }

        private byte[] ComputeQueryStringHash(string queryString)
        {
            // Validate the input parameters
            if (string.IsNullOrWhiteSpace(queryString))
            {
                if (queryString == null)
                {
                    throw SQL.NullArgumentInternal(nameof(queryString), nameof(EnclaveDelegate), nameof(ComputeQueryStringHash));
                }
                else
                {
                    throw SQL.EmptyArgumentInternal(nameof(queryString), nameof(EnclaveDelegate), nameof(ComputeQueryStringHash));
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
