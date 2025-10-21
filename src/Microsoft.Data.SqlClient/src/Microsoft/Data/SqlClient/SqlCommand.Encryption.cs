// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlCommand
    {
        #region Internal Methods

        /// <summary>
        /// This function returns a list of the names of the custom providers currently registered.
        /// </summary>
        /// <returns>Combined list of provider names</returns>
        internal List<string> GetColumnEncryptionCustomKeyStoreProvidersNames()
        {
            if (_customColumnEncryptionKeyStoreProviders.Count > 0)
            {
                return new List<string>(_customColumnEncryptionKeyStoreProviders.Keys);
            }
            return new List<string>(0);
        }

        /// <summary>
        /// This function walks through the registered custom column encryption key store providers and returns an object if found.
        /// </summary>
        /// <param name="providerName">Provider Name to be searched in custom provider dictionary.</param>
        /// <param name="columnKeyStoreProvider">If the provider is found, initializes the corresponding SqlColumnEncryptionKeyStoreProvider instance.</param>
        /// <returns>true if the provider is found, else returns false</returns>
        internal bool TryGetColumnEncryptionKeyStoreProvider(string providerName, out SqlColumnEncryptionKeyStoreProvider columnKeyStoreProvider)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(providerName), "Provider name is invalid");
            return _customColumnEncryptionKeyStoreProviders.TryGetValue(providerName, out columnKeyStoreProvider);
        }

        #endregion

        #region Private Methods

        private static void ValidateCustomProviders(IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders)
        {
            // Throw when the provided dictionary is null.
            if (customProviders is null)
            {
                throw SQL.NullCustomKeyStoreProviderDictionary();
            }

            // Validate that custom provider list doesn't contain any of system provider list
            foreach (string key in customProviders.Keys)
            {
                // Validate the provider name
                //
                // Check for null or empty
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw SQL.EmptyProviderName();
                }

                // Check if the name starts with MSSQL_, since this is reserved namespace for system providers.
                if (key.StartsWith(ADP.ColumnEncryptionSystemProviderNamePrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw SQL.InvalidCustomKeyStoreProviderName(key, ADP.ColumnEncryptionSystemProviderNamePrefix);
                }

                // Validate the provider value
                if (customProviders[key] is null)
                {
                    throw SQL.NullProviderValue(key);
                }
            }
        }

        private EnclaveSessionParameters GetEnclaveSessionParameters()
        {
            return new EnclaveSessionParameters(
                this._activeConnection.DataSource,
                this._activeConnection.EnclaveAttestationUrl,
                this._activeConnection.Database);
        }

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

        /// <summary>
        /// Executes the reader after checking to see if we need to encrypt input parameters and then encrypting it if required.
        /// TryFetchInputParameterEncryptionInfo() -> ReadDescribeEncryptionParameterResults()-> EncryptInputParameters() ->RunExecuteReaderTds()
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="timeout"></param>
        /// <param name="completion"></param>
        /// <param name="returnTask"></param>
        /// <param name="asyncWrite"></param>
        /// <param name="usedCache"></param>
        /// <param name="isRetry"></param>
        /// <returns></returns>
        private void PrepareForTransparentEncryption(
            bool isAsync,
            int timeout,
            TaskCompletionSource<object> completion,
            out Task returnTask,
            bool asyncWrite,
            out bool usedCache,
            bool isRetry)
        {
            // Fetch reader with input params
            Task fetchInputParameterEncryptionInfoTask = null;
            bool describeParameterEncryptionNeeded = false;
            SqlDataReader describeParameterEncryptionDataReader = null;
            returnTask = null;
            usedCache = false;

            Debug.Assert(_activeConnection != null, "_activeConnection should not be null in PrepareForTransparentEncryption.");
            Debug.Assert(_activeConnection.Parser != null, "_activeConnection.Parser should not be null in PrepareForTransparentEncryption.");
            Debug.Assert(_activeConnection.Parser.IsColumnEncryptionSupported,
                "_activeConnection.Parser.IsColumnEncryptionSupported should be true in PrepareForTransparentEncryption.");
            Debug.Assert(_columnEncryptionSetting == SqlCommandColumnEncryptionSetting.Enabled
                        || (_columnEncryptionSetting == SqlCommandColumnEncryptionSetting.UseConnectionSetting && _activeConnection.IsColumnEncryptionSettingEnabled),
                        "ColumnEncryption setting should be enabled for input parameter encryption.");
            Debug.Assert(isAsync == (completion != null), "completion should can be null if and only if mode is async.");

            // If we are not in Batch RPC and not already retrying, attempt to fetch the cipher MD for each parameter from the cache.
            // If this succeeds then return immediately, otherwise just fall back to the full crypto MD discovery.
            if (!_batchRPCMode && !isRetry && (this._parameters != null && this._parameters.Count > 0) && SqlQueryMetadataCache.GetInstance().GetQueryMetadataIfExists(this))
            {
                usedCache = true;
                return;
            }

            // A flag to indicate if finallyblock needs to execute.
            bool processFinallyBlock = true;

            // A flag to indicate if we need to decrement async count on the connection in finally block.
            bool decrementAsyncCountInFinallyBlock = false;

            // Flag to indicate if exception is caught during the execution, to govern clean up.
            bool exceptionCaught = false;

            // Used in _batchRPCMode to maintain a map of describe parameter encryption RPC requests (Keys) and their corresponding original RPC requests (Values).
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap = null;

            try
            {
                try
                {
                    // Fetch the encryption information that applies to any of the input parameters.
                    describeParameterEncryptionDataReader = TryFetchInputParameterEncryptionInfo(timeout,
                                                                                                 isAsync,
                                                                                                 asyncWrite,
                                                                                                 out describeParameterEncryptionNeeded,
                                                                                                 out fetchInputParameterEncryptionInfoTask,
                                                                                                 out describeParameterEncryptionRpcOriginalRpcMap,
                                                                                                 isRetry);

                    Debug.Assert(describeParameterEncryptionNeeded || describeParameterEncryptionDataReader == null,
                        "describeParameterEncryptionDataReader should be null if we don't need to request describe parameter encryption request.");

                    Debug.Assert(fetchInputParameterEncryptionInfoTask == null || isAsync,
                        "Task returned by TryFetchInputParameterEncryptionInfo, when in sync mode, in PrepareForTransparentEncryption.");

                    Debug.Assert((describeParameterEncryptionRpcOriginalRpcMap != null) == _batchRPCMode,
                        "describeParameterEncryptionRpcOriginalRpcMap can be non-null if and only if it is in _batchRPCMode.");

                    // If we didn't have parameters, we can fall back to regular code path, by simply returning.
                    if (!describeParameterEncryptionNeeded)
                    {
                        Debug.Assert(fetchInputParameterEncryptionInfoTask == null,
                            "fetchInputParameterEncryptionInfoTask should not be set if describe parameter encryption is not needed.");

                        Debug.Assert(describeParameterEncryptionDataReader == null,
                            "SqlDataReader created for describe parameter encryption params when it is not needed.");

                        return;
                    }

                    // If we are in async execution, we need to decrement our async count on exception.
                    decrementAsyncCountInFinallyBlock = isAsync;

                    Debug.Assert(describeParameterEncryptionDataReader != null,
                        "describeParameterEncryptionDataReader should not be null, as it is required to get results of describe parameter encryption.");

                    // Fire up another task to read the results of describe parameter encryption
                    if (fetchInputParameterEncryptionInfoTask != null)
                    {
                        // Mark that we should not process the finally block since we have async execution pending.
                        // Note that this should be done outside the task's continuation delegate.
                        processFinallyBlock = false;
                        describeParameterEncryptionDataReader = GetParameterEncryptionDataReader(
                            out returnTask,
                            fetchInputParameterEncryptionInfoTask,
                            describeParameterEncryptionDataReader,
                            describeParameterEncryptionRpcOriginalRpcMap,
                            describeParameterEncryptionNeeded,
                            isRetry);

                        decrementAsyncCountInFinallyBlock = false;
                    }
                    else
                    {
                        // If it was async, ending the reader is still pending.
                        if (isAsync)
                        {
                            // Mark that we should not process the finally block since we have async execution pending.
                            // Note that this should be done outside the task's continuation delegate.
                            processFinallyBlock = false;
                            describeParameterEncryptionDataReader = GetParameterEncryptionDataReaderAsync(
                                out returnTask,
                                describeParameterEncryptionDataReader,
                                describeParameterEncryptionRpcOriginalRpcMap,
                                describeParameterEncryptionNeeded,
                                isRetry);

                            decrementAsyncCountInFinallyBlock = false;
                        }
                        else
                        {
                            // For synchronous execution, read the results of describe parameter encryption here.
                            ReadDescribeEncryptionParameterResults(
                                describeParameterEncryptionDataReader,
                                describeParameterEncryptionRpcOriginalRpcMap,
                                isRetry);
                        }

                        #if DEBUG
                        // Failpoint to force the thread to halt to simulate cancellation of SqlCommand.
                        if (_sleepAfterReadDescribeEncryptionParameterResults)
                        {
                            Thread.Sleep(10000);
                        }
                        #endif
                    }
                }
                catch (Exception e)
                {
                    processFinallyBlock = ADP.IsCatchableExceptionType(e);
                    exceptionCaught = true;
                    throw;
                }
                finally
                {
                    // Free up the state only for synchronous execution. For asynchronous execution, free only if there was an exception.
                    PrepareTransparentEncryptionFinallyBlock(closeDataReader: (processFinallyBlock && !isAsync) || exceptionCaught,
                                           decrementAsyncCount: decrementAsyncCountInFinallyBlock && exceptionCaught,
                                           clearDataStructures: (processFinallyBlock && !isAsync) || exceptionCaught,
                                           wasDescribeParameterEncryptionNeeded: describeParameterEncryptionNeeded,
                                           describeParameterEncryptionRpcOriginalRpcMap: describeParameterEncryptionRpcOriginalRpcMap,
                                           describeParameterEncryptionDataReader: describeParameterEncryptionDataReader);
                }
            }
            // @TODO: CER Exception Handling was removed here (see GH#3581)
            catch (Exception e)
            {
                if (CachedAsyncState != null)
                {
                    CachedAsyncState.ResetAsyncState();
                }

                if (ADP.IsCatchableExceptionType(e))
                {
                    ReliablePutStateObject();
                }

                throw;
            }
        }

        /// <summary>
        /// Steps to be executed in the Prepare Transparent Encryption finally block.
        /// </summary>
        private void PrepareTransparentEncryptionFinallyBlock(bool closeDataReader,
            bool clearDataStructures,
            bool decrementAsyncCount,
            bool wasDescribeParameterEncryptionNeeded,
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            SqlDataReader describeParameterEncryptionDataReader)
        {
            if (clearDataStructures)
            {
                // Clear some state variables in SqlCommand that reflect in-progress describe parameter encryption requests.
                ClearDescribeParameterEncryptionRequests();

                if (describeParameterEncryptionRpcOriginalRpcMap != null)
                {
                    describeParameterEncryptionRpcOriginalRpcMap = null;
                }
            }

            // Decrement the async count.
            if (decrementAsyncCount)
            {
                SqlInternalConnectionTds internalConnectionTds = _activeConnection.GetOpenTdsConnection();
                if (internalConnectionTds != null)
                {
                    internalConnectionTds.DecrementAsyncCount();
                }
            }

            if (closeDataReader)
            {
                // Close the data reader to reset the _stateObj
                if (describeParameterEncryptionDataReader != null)
                {
                    describeParameterEncryptionDataReader.Close();
                }
            }
        }

        /// <summary>
        /// Read the output of sp_describe_parameter_encryption
        /// </summary>
        /// <param name="ds">Resultset from calling to sp_describe_parameter_encryption</param>
        /// <param name="describeParameterEncryptionRpcOriginalRpcMap"> Readonly dictionary with the map of parameter encryption rpc requests with the corresponding original rpc requests.</param>
        /// <param name="isRetry">Indicates if this is a retry from a failed call.</param>
        private void ReadDescribeEncryptionParameterResults(
            SqlDataReader ds,
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            bool isRetry)
        {
            _SqlRPC rpc = null;
            Dictionary<int, SqlTceCipherInfoEntry> columnEncryptionKeyTable = new Dictionary<int, SqlTceCipherInfoEntry>();

            Debug.Assert((describeParameterEncryptionRpcOriginalRpcMap != null) == _batchRPCMode,
                "describeParameterEncryptionRpcOriginalRpcMap should be non-null if and only if it is _batchRPCMode.");

            // Indicates the current result set we are reading, used in BatchRPCMode, where we can have more than 1 result set.
            int resultSetSequenceNumber = 0;

            // A flag that used in BatchRPCMode, to assert the result of lookup in to the dictionary maintaining the map of describe parameter encryption requests
            // and the corresponding original rpc requests.
            bool lookupDictionaryResult;

            do
            {
                if (_batchRPCMode)
                {
                    // If we got more RPC results from the server than what was requested.
                    if (resultSetSequenceNumber >= _sqlRPCParameterEncryptionReqArray.Length)
                    {
                        Debug.Assert(false, "Server sent back more results than what was expected for describe parameter encryption requests in _batchRPCMode.");
                        // Ignore the rest of the results from the server, if for whatever reason it sends back more than what we expect.
                        break;
                    }
                }

                bool enclaveMetadataExists = ReadDescribeEncryptionParameterResultsKeyList(ds, columnEncryptionKeyTable);
                if (!enclaveMetadataExists && !ds.NextResult())
                {
                    throw SQL.UnexpectedDescribeParamFormatParameterMetadata();
                }

                // Find the RPC command that generated this tce request
                if (_batchRPCMode)
                {
                    Debug.Assert(_sqlRPCParameterEncryptionReqArray[resultSetSequenceNumber] != null, "_sqlRPCParameterEncryptionReqArray[resultSetSequenceNumber] should not be null.");

                    // Lookup in the dictionary to get the original rpc request corresponding to the describe parameter encryption request
                    // pointed to by _sqlRPCParameterEncryptionReqArray[resultSetSequenceNumber]
                    rpc = null;
                    lookupDictionaryResult = describeParameterEncryptionRpcOriginalRpcMap.TryGetValue(_sqlRPCParameterEncryptionReqArray[resultSetSequenceNumber++], out rpc);

                    Debug.Assert(lookupDictionaryResult,
                        "Describe Parameter Encryption RPC request key must be present in the dictionary describeParameterEncryptionRpcOriginalRpcMap");
                    Debug.Assert(rpc != null,
                        "Describe Parameter Encryption RPC request's corresponding original rpc request must not be null in the dictionary describeParameterEncryptionRpcOriginalRpcMap");
                }
                else
                {
                    rpc = _rpcArrayOf1[0];
                }

                Debug.Assert(rpc != null, "rpc should not be null here.");

                int userParamCount = rpc.userParams?.Count ?? 0;
                int receivedMetadataCount = 0;
                if (!enclaveMetadataExists || ds.NextResult())
                {
                    receivedMetadataCount = ReadDescribeEncryptionParameterResultsMetadata(ds, rpc, columnEncryptionKeyTable);
                }

                // When the RPC object gets reused, the parameter array has more parameters that the valid params for the command.
                // Null is used to indicate the end of the valid part of the array. Refer to GetRPCObject().
                if (receivedMetadataCount != userParamCount)
                {
                    for (int index = 0; index < userParamCount; index++)
                    {
                        SqlParameter sqlParameter = rpc.userParams[index];
                        if (!sqlParameter.HasReceivedMetadata && sqlParameter.Direction != ParameterDirection.ReturnValue)
                        {
                            // Encryption MD wasn't sent by the server - we expect the metadata to be sent for all the parameters
                            // that were sent in the original sp_describe_parameter_encryption but not necessarily for return values,
                            // since there might be multiple return values but server will only send for one of them.
                            // For parameters that don't need encryption, the encryption type is set to plaintext.
                            throw SQL.ParamEncryptionMetadataMissing(sqlParameter.ParameterName, rpc.GetCommandTextOrRpcName());
                        }
                    }
                }

                if (ShouldUseEnclaveBasedWorkflow && (enclaveAttestationParameters != null) && requiresEnclaveComputations)
                {
                    if (!ds.NextResult())
                    {
                        throw SQL.UnexpectedDescribeParamFormatAttestationInfo(this._activeConnection.Parser.EnclaveType);
                    }

                    ReadDescribeEncryptionParameterResultsAttestation(ds, isRetry);
                }

                // The server has responded with encryption related information for this rpc request. So clear the needsFetchParameterEncryptionMetadata flag.
                rpc.needsFetchParameterEncryptionMetadata = false;
            } while (ds.NextResult());

            // Verify that we received response for each rpc call needs tce
            if (_batchRPCMode)
            {
                for (int i = 0; i < _RPCList.Count; i++)
                {
                    if (_RPCList[i].needsFetchParameterEncryptionMetadata)
                    {
                        throw SQL.ProcEncryptionMetadataMissing(_RPCList[i].rpcName);
                    }
                }
            }

            // If we are not in Batch RPC mode, update the query cache with the encryption MD.
            if (!_batchRPCMode && ShouldCacheEncryptionMetadata && (_parameters is not null && _parameters.Count > 0))
            {
                SqlQueryMetadataCache.GetInstance().AddQueryMetadata(this, ignoreQueriesWithReturnValueParams: true);
            }
        }

        private void InvalidateEnclaveSession()
        {
            if (ShouldUseEnclaveBasedWorkflow && this.enclavePackage != null)
            {
                EnclaveDelegate.Instance.InvalidateEnclaveSession(
                    this._activeConnection.AttestationProtocol,
                    this._activeConnection.Parser.EnclaveType,
                    GetEnclaveSessionParameters(),
                    this.enclavePackage.EnclaveSession);
            }
        }

        private void ReadDescribeEncryptionParameterResultsAttestation(SqlDataReader ds, bool isRetry)
        {
            bool attestationInfoRead = false;
            while (ds.Read())
            {
                if (attestationInfoRead)
                {
                    throw SQL.MultipleRowsReturnedForAttestationInfo();
                }

                int attestationInfoLength = (int)ds.GetBytes((int)DescribeParameterEncryptionResultSet3.AttestationInfo, 0, null, 0, 0);
                byte[] attestationInfo = new byte[attestationInfoLength];
                ds.GetBytes((int)DescribeParameterEncryptionResultSet3.AttestationInfo, 0, attestationInfo, 0, attestationInfoLength);

                SqlConnectionAttestationProtocol attestationProtocol = this._activeConnection.AttestationProtocol;
                string enclaveType = this._activeConnection.Parser.EnclaveType;

                EnclaveDelegate.Instance.CreateEnclaveSession(
                    attestationProtocol,
                    enclaveType,
                    GetEnclaveSessionParameters(),
                    attestationInfo,
                    enclaveAttestationParameters,
                    customData,
                    customDataLength,
                    isRetry);
                enclaveAttestationParameters = null;
                attestationInfoRead = true;
            }

            if (!attestationInfoRead)
            {
                throw SQL.AttestationInfoNotReturnedFromSqlServer(this._activeConnection.Parser.EnclaveType, this._activeConnection.EnclaveAttestationUrl);
            }
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

        private int ReadDescribeEncryptionParameterResultsMetadata(
            SqlDataReader ds,
            _SqlRPC rpc,
            Dictionary<int, SqlTceCipherInfoEntry> columnEncryptionKeyTable)
        {
            int receivedMetadataCount = 0;
            int userParamCount = rpc.userParams?.Count ?? 0;

            while (ds.Read())
            {
                Debug.Assert(rpc != null, "Describe Parameter Encryption requested for non-tce spec proc");
                string parameterName = ds.GetString((int)DescribeParameterEncryptionResultSet2.ParameterName);

                // When the RPC object gets reused, the parameter array has more parameters that the valid params for the command.
                // Null is used to indicate the end of the valid part of the array. Refer to GetRPCObject().
                for (int index = 0; index < userParamCount; index++)
                {
                    SqlParameter sqlParameter = rpc.userParams[index];
                    Debug.Assert(sqlParameter != null, "sqlParameter should not be null.");

                    if (SqlParameter.ParameterNamesEqual(sqlParameter.ParameterName, parameterName, StringComparison.Ordinal))
                    {
                        Debug.Assert(sqlParameter.CipherMetadata == null, "param.CipherMetadata should be null.");
                        sqlParameter.HasReceivedMetadata = true;
                        receivedMetadataCount += 1;
                        // Found the param, setup the encryption info.
                        byte columnEncryptionType = ds.GetByte((int)DescribeParameterEncryptionResultSet2.ColumnEncryptionType);
                        if ((byte)SqlClientEncryptionType.PlainText != columnEncryptionType)
                        {
                            byte cipherAlgorithmId = ds.GetByte((int)DescribeParameterEncryptionResultSet2.ColumnEncryptionAlgorithm);
                            int columnEncryptionKeyOrdinal = ds.GetInt32((int)DescribeParameterEncryptionResultSet2.ColumnEncryptionKeyOrdinal);
                            byte columnNormalizationRuleVersion = ds.GetByte((int)DescribeParameterEncryptionResultSet2.NormalizationRuleVersion);

                            // Lookup the key, failing which throw an exception
                            if (!columnEncryptionKeyTable.TryGetValue(columnEncryptionKeyOrdinal, out SqlTceCipherInfoEntry cipherInfoEntry))
                            {
                                throw SQL.InvalidEncryptionKeyOrdinalParameterMetadata(columnEncryptionKeyOrdinal, columnEncryptionKeyTable.Count);
                            }

                            sqlParameter.CipherMetadata = new SqlCipherMetadata(sqlTceCipherInfoEntry: cipherInfoEntry,
                                                                                ordinal: unchecked((ushort)-1),
                                                                                cipherAlgorithmId: cipherAlgorithmId,
                                                                                cipherAlgorithmName: null,
                                                                                encryptionType: columnEncryptionType,
                                                                                normalizationRuleVersion: columnNormalizationRuleVersion);

                            // Decrypt the symmetric key.(This will also validate and throw if needed).
                            Debug.Assert(_activeConnection != null, @"_activeConnection should not be null");
                            SqlSecurityUtility.DecryptSymmetricKey(sqlParameter.CipherMetadata, _activeConnection, this);

                            // This is effective only for BatchRPCMode even though we set it for non-BatchRPCMode also,
                            // since for non-BatchRPCMode mode, paramoptions gets thrown away and reconstructed in BuildExecuteSql.
                            int options = (int)(rpc.userParamMap[index] >> 32);
                            options |= TdsEnums.RPC_PARAM_ENCRYPTED;
                            rpc.userParamMap[index] = ((((long)options) << 32) | (long)index);
                        }

                        break;
                    }
                }
            }

            return receivedMetadataCount;
        }

        /// <summary>
        /// Resets the encryption related state of the command object and each of the parameters.
        /// BatchRPC doesn't need special handling to cleanup the state of each RPC object and its parameters since a new RPC object and
        /// parameters are generated on every execution.
        /// </summary>
        private void ResetEncryptionState()
        {
            // First reset the command level state.
            ClearDescribeParameterEncryptionRequests();

            // Reset the state for internal End execution.
            _internalEndExecuteInitiated = false;

            // Reset the state for the cache.
            CachingQueryMetadataPostponed = false;

            // Reset the state of each of the parameters.
            if (_parameters != null)
            {
                for (int i = 0; i < _parameters.Count; i++)
                {
                    _parameters[i].CipherMetadata = null;
                    _parameters[i].HasReceivedMetadata = false;
                }
            }

            keysToBeSentToEnclave?.Clear();
            enclavePackage = null;
            requiresEnclaveComputations = false;
            enclaveAttestationParameters = null;
            customData = null;
            customDataLength = 0;
        }

        /// <summary>
        /// Set the column encryption setting to the new one.
        /// Do not allow conflicting column encryption settings.
        /// </summary>
        // @TODO: This basically just allows it to be set once and it cannot be changed after.
        private void SetColumnEncryptionSetting(SqlCommandColumnEncryptionSetting newColumnEncryptionSetting)
        {
            if (!_wasBatchModeColumnEncryptionSettingSetOnce)
            {
                _columnEncryptionSetting = newColumnEncryptionSetting;
                _wasBatchModeColumnEncryptionSettingSetOnce = true;
            }
            else if (_columnEncryptionSetting != newColumnEncryptionSetting)
            {
                throw SQL.BatchedUpdateColumnEncryptionSettingMismatch();
            }
        }

        #endregion
    }
}
