// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlCommand
    {
        private SqlDataReader GetParameterEncryptionDataReader(
            out Task returnTask,
            Task fetchInputParameterEncryptionInfoTask,
            SqlDataReader describeParameterEncryptionDataReader,
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            bool describeParameterEncryptionNeeded,
            bool isRetry)
        {
            returnTask = AsyncHelper.CreateContinuationTaskWithState(fetchInputParameterEncryptionInfoTask, this,
                (object state) =>
                {
                    SqlCommand command = (SqlCommand)state;
                    bool processFinallyBlockAsync = true;
                    bool decrementAsyncCountInFinallyBlockAsync = true;

                    try
                    {
                        // Check for any exceptions on network write, before reading.
                        command.CheckThrowSNIException();

                        // If it is async, then TryFetchInputParameterEncryptionInfo-> RunExecuteReaderTds would have incremented the async count.
                        // Decrement it when we are about to complete async execute reader.
                        SqlInternalConnectionTds internalConnectionTds = command._activeConnection.GetOpenTdsConnection();
                        if (internalConnectionTds != null)
                        {
                            internalConnectionTds.DecrementAsyncCount();
                            decrementAsyncCountInFinallyBlockAsync = false;
                        }

                        // Complete executereader.
                        describeParameterEncryptionDataReader = command.CompleteAsyncExecuteReader(isInternal: false, forDescribeParameterEncryption: true);
                        Debug.Assert(command._stateObj == null, "non-null state object in PrepareForTransparentEncryption.");

                        // Read the results of describe parameter encryption.
                        command.ReadDescribeEncryptionParameterResults(
                            describeParameterEncryptionDataReader,
                            describeParameterEncryptionRpcOriginalRpcMap,
                            isRetry);

                        #if DEBUG
                        // Failpoint to force the thread to halt to simulate cancellation of SqlCommand.
                        if (_sleepAfterReadDescribeEncryptionParameterResults)
                        {
                            Thread.Sleep(10000);
                        }
                        #endif
                    }
                    catch (Exception e)
                    {
                        processFinallyBlockAsync = ADP.IsCatchableExceptionType(e);
                        throw;
                    }
                    finally
                    {
                        command.PrepareTransparentEncryptionFinallyBlock(closeDataReader: processFinallyBlockAsync,
                            decrementAsyncCount: decrementAsyncCountInFinallyBlockAsync,
                            clearDataStructures: processFinallyBlockAsync,
                            wasDescribeParameterEncryptionNeeded: describeParameterEncryptionNeeded,
                            describeParameterEncryptionRpcOriginalRpcMap: describeParameterEncryptionRpcOriginalRpcMap,
                            describeParameterEncryptionDataReader: describeParameterEncryptionDataReader);
                    }
                },
                onFailure: static (Exception exception, object state) =>
                {
                    SqlCommand command = (SqlCommand)state;
                    if (command.CachedAsyncState != null)
                    {
                        command.CachedAsyncState.ResetAsyncState();
                    }

                    if (exception != null)
                    {
                        throw exception;
                    }
                });

            return describeParameterEncryptionDataReader;
        }

        private SqlDataReader GetParameterEncryptionDataReaderAsync(
            out Task returnTask,
            SqlDataReader describeParameterEncryptionDataReader,
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            bool describeParameterEncryptionNeeded,
            bool isRetry)
        {
            returnTask = Task.Run(() =>
            {
                bool processFinallyBlockAsync = true;
                bool decrementAsyncCountInFinallyBlockAsync = true;

                try
                {
                    // Check for any exceptions on network write, before reading.
                    CheckThrowSNIException();

                    // If it is async, then TryFetchInputParameterEncryptionInfo-> RunExecuteReaderTds would have incremented the async count.
                    // Decrement it when we are about to complete async execute reader.
                    SqlInternalConnectionTds internalConnectionTds = _activeConnection.GetOpenTdsConnection();
                    if (internalConnectionTds != null)
                    {
                        internalConnectionTds.DecrementAsyncCount();
                        decrementAsyncCountInFinallyBlockAsync = false;
                    }

                    // Complete executereader.
                    describeParameterEncryptionDataReader = CompleteAsyncExecuteReader(isInternal: false, forDescribeParameterEncryption: true);
                    Debug.Assert(_stateObj == null, "non-null state object in PrepareForTransparentEncryption.");

                    // Read the results of describe parameter encryption.
                    ReadDescribeEncryptionParameterResults(
                        describeParameterEncryptionDataReader,
                        describeParameterEncryptionRpcOriginalRpcMap,
                        isRetry);

                    #if DEBUG
                    // Failpoint to force the thread to halt to simulate cancellation of SqlCommand.
                    if (_sleepAfterReadDescribeEncryptionParameterResults)
                    {
                        Thread.Sleep(10000);
                    }
                    #endif
                }
                catch (Exception e)
                {
                    processFinallyBlockAsync = ADP.IsCatchableExceptionType(e);
                    throw;
                }
                finally
                {
                    PrepareTransparentEncryptionFinallyBlock(closeDataReader: processFinallyBlockAsync,
                        decrementAsyncCount: decrementAsyncCountInFinallyBlockAsync,
                        clearDataStructures: processFinallyBlockAsync,
                        wasDescribeParameterEncryptionNeeded: describeParameterEncryptionNeeded,
                        describeParameterEncryptionRpcOriginalRpcMap: describeParameterEncryptionRpcOriginalRpcMap,
                        describeParameterEncryptionDataReader: describeParameterEncryptionDataReader);
                }
            });
            return describeParameterEncryptionDataReader;
        }

        private bool ReadDescribeEncryptionParameterResultsKeyList(
            SqlDataReader ds,
            Dictionary<int, SqlTceCipherInfoEntry> columnEncryptionKeyTable)
        {
            bool enclaveMetadataExists = true;
            while (ds.Read())
            {
                // Column Encryption Key Ordinal.
                int currentOrdinal = ds.GetInt32((int)DescribeParameterEncryptionResultSet1.KeyOrdinal);
                Debug.Assert(currentOrdinal >= 0, "currentOrdinal cannot be negative.");

                // Try to see if there was already an entry for the current ordinal.
                if (!columnEncryptionKeyTable.TryGetValue(currentOrdinal, out SqlTceCipherInfoEntry cipherInfoEntry))
                {
                    // If an entry for this ordinal was not found, create an entry in the columnEncryptionKeyTable for this ordinal.
                    cipherInfoEntry = new SqlTceCipherInfoEntry(currentOrdinal);
                    columnEncryptionKeyTable.Add(currentOrdinal, cipherInfoEntry);
                }

                Debug.Assert(!cipherInfoEntry.Equals(default(SqlTceCipherInfoEntry)), "cipherInfoEntry should not be un-initialized.");

                // Read the CEK.
                byte[] encryptedKey = null;
                int encryptedKeyLength = (int)ds.GetBytes((int)DescribeParameterEncryptionResultSet1.EncryptedKey, 0, encryptedKey, 0, 0);
                encryptedKey = new byte[encryptedKeyLength];
                ds.GetBytes((int)DescribeParameterEncryptionResultSet1.EncryptedKey, 0, encryptedKey, 0, encryptedKeyLength);

                // Read the metadata version of the key.
                // It should always be 8 bytes.
                byte[] keyMdVersion = new byte[8];
                ds.GetBytes((int)DescribeParameterEncryptionResultSet1.KeyMdVersion, 0, keyMdVersion, 0, keyMdVersion.Length);

                // Validate the provider name
                string providerName = ds.GetString((int)DescribeParameterEncryptionResultSet1.ProviderName);

                string keyPath = ds.GetString((int)DescribeParameterEncryptionResultSet1.KeyPath);
                cipherInfoEntry.Add(encryptedKey: encryptedKey,
                                    databaseId: ds.GetInt32((int)DescribeParameterEncryptionResultSet1.DbId),
                                    cekId: ds.GetInt32((int)DescribeParameterEncryptionResultSet1.KeyId),
                                    cekVersion: ds.GetInt32((int)DescribeParameterEncryptionResultSet1.KeyVersion),
                                    cekMdVersion: keyMdVersion,
                                    keyPath: keyPath,
                                    keyStoreName: providerName,
                                    algorithmName: ds.GetString((int)DescribeParameterEncryptionResultSet1.KeyEncryptionAlgorithm));

                bool isRequestedByEnclave = false;

                // Servers supporting enclave computations should always
                // return a boolean indicating whether the key is required by enclave or not.
                if (this._activeConnection.Parser.TceVersionSupported >= TdsEnums.MIN_TCE_VERSION_WITH_ENCLAVE_SUPPORT)
                {
                    isRequestedByEnclave =
                        ds.GetBoolean((int)DescribeParameterEncryptionResultSet1.IsRequestedByEnclave);
                }
                else
                {
                    enclaveMetadataExists = false;
                }

                if (isRequestedByEnclave)
                {
                    if (string.IsNullOrWhiteSpace(this.Connection.EnclaveAttestationUrl) && Connection.AttestationProtocol != SqlConnectionAttestationProtocol.None)
                    {
                        throw SQL.NoAttestationUrlSpecifiedForEnclaveBasedQuerySpDescribe(this._activeConnection.Parser.EnclaveType);
                    }

                    byte[] keySignature = null;

                    if (!ds.IsDBNull((int)DescribeParameterEncryptionResultSet1.KeySignature))
                    {
                        int keySignatureLength = (int)ds.GetBytes((int)DescribeParameterEncryptionResultSet1.KeySignature, 0, keySignature, 0, 0);
                        keySignature = new byte[keySignatureLength];
                        ds.GetBytes((int)DescribeParameterEncryptionResultSet1.KeySignature, 0, keySignature, 0, keySignatureLength);
                    }

                    SqlSecurityUtility.VerifyColumnMasterKeySignature(providerName, keyPath, isRequestedByEnclave, keySignature, _activeConnection, this);

                    int requestedKey = currentOrdinal;
                    SqlTceCipherInfoEntry cipherInfo;

                    // Lookup the key, failing which throw an exception
                    if (!columnEncryptionKeyTable.TryGetValue(requestedKey, out cipherInfo))
                    {
                        throw SQL.InvalidEncryptionKeyOrdinalEnclaveMetadata(requestedKey, columnEncryptionKeyTable.Count);
                    }

                    if (keysToBeSentToEnclave == null)
                    {
                        keysToBeSentToEnclave = new ConcurrentDictionary<int, SqlTceCipherInfoEntry>();
                        keysToBeSentToEnclave.TryAdd(currentOrdinal, cipherInfo);
                    }
                    else if (!keysToBeSentToEnclave.ContainsKey(currentOrdinal))
                    {
                        keysToBeSentToEnclave.TryAdd(currentOrdinal, cipherInfo);
                    }

                    requiresEnclaveComputations = true;
                }
            }

            return enclaveMetadataExists;
        }
    }
}
