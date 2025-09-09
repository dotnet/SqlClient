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
        // @TODO: 1) This should be a property
        // @TODO: 2) There is no reason for this to be a List, or even for it to be copied
        // @TODO: 3) This doesn't check for null _customColumnEncryptionKeyStoreProviders
        internal List<string> GetColumnEncryptionCustomKeyStoreProvidersNames()
        {
            if (_customColumnEncryptionKeyStoreProviders.Count > 0)
            {
                return new List<string>(_customColumnEncryptionKeyStoreProviders.Keys);
            }

            return new List<string>(0);
        }

        /// <summary>
        /// This function walks through the registered custom column encryption key store providers
        /// and returns an object if found.
        /// </summary>
        /// <param name="providerName">Provider Name to be searched in custom provider dictionary.</param>
        /// <param name="columnKeyStoreProvider">
        /// If the provider is found, the matching provider is returned.
        /// </param>
        /// <returns><c>true</c> if the provider is found, else returns <c>false</c></returns>
        internal bool TryGetColumnEncryptionKeyStoreProvider(
            string providerName,
            out SqlColumnEncryptionKeyStoreProvider columnKeyStoreProvider)
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

        private EnclaveSessionParameters GetEnclaveSessionParameters() =>
            new EnclaveSessionParameters(
                _activeConnection.DataSource,
                _activeConnection.EnclaveAttestationUrl,
                _activeConnection.Database);

        // @TODO: Isn't this doing things asynchronously? We should just have a purely asynchronous and a purely synchronous pathway instead of this mix of check this check that and flags.
        private SqlDataReader GetParameterEncryptionDataReader(
            out Task returnTask,
            Task fetchInputParameterEncryptionInfoTask,
            SqlDataReader describeParameterEncryptionDataReader,
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            bool describeParameterEncryptionNeeded,
            bool isRetry)
        {
            returnTask = AsyncHelper.CreateContinuationTaskWithState(
                task: fetchInputParameterEncryptionInfoTask,
                state: this,
                onSuccess: state =>
                {
                    SqlCommand command = (SqlCommand)state;
                    bool processFinallyBlockAsync = true;
                    bool decrementAsyncCountInFinallyBlockAsync = true;

                    try
                    {
                        // Check for any exceptions on network write, before reading.
                        command.CheckThrowSNIException();

                        // If it is async, then TryFetchInputParameterEncryptionInfo ->
                        // RunExecuteReaderTds would have incremented the async count. Decrement it
                        // when we are about to complete async execute reader.
                        SqlInternalConnectionTds internalConnectionTds =
                            command._activeConnection.GetOpenTdsConnection();
                        if (internalConnectionTds is not null)
                        {
                            internalConnectionTds.DecrementAsyncCount();
                            decrementAsyncCountInFinallyBlockAsync = false;
                        }

                        // Complete executereader.
                        // @TODO: If we can remove this reference, this could be a static lambda
                        describeParameterEncryptionDataReader = command.CompleteAsyncExecuteReader(
                            isInternal: false,
                            forDescribeParameterEncryption: true);
                        Debug.Assert(command._stateObj is null, "non-null state object in PrepareForTransparentEncryption.");

                        // Read the results of describe parameter encryption.
                        command.ReadDescribeEncryptionParameterResults(
                            describeParameterEncryptionDataReader,
                            describeParameterEncryptionRpcOriginalRpcMap,
                            isRetry);

                        #if DEBUG
                        // Failpoint to force the thread to halt to simulate cancellation of SqlCommand.
                        if (_sleepAfterReadDescribeEncryptionParameterResults)
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(10));
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
                        command.PrepareTransparentEncryptionFinallyBlock(
                            closeDataReader: processFinallyBlockAsync,
                            decrementAsyncCount: decrementAsyncCountInFinallyBlockAsync,
                            clearDataStructures: processFinallyBlockAsync,
                            wasDescribeParameterEncryptionNeeded: describeParameterEncryptionNeeded,
                            describeParameterEncryptionRpcOriginalRpcMap: describeParameterEncryptionRpcOriginalRpcMap,
                            describeParameterEncryptionDataReader: describeParameterEncryptionDataReader);
                    }
                },
                onFailure: static (exception, state) =>
                {
                    SqlCommand command = (SqlCommand)state;
                    command.CachedAsyncState?.ResetAsyncState();

                    if (exception is not null)
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
                    // Check for any exception on network write before reading.
                    CheckThrowSNIException();

                    // If it is async, then TryFetchInputParameterEncryptionInfo ->
                    // RunExecuteReaderTds would have incremented the async count. Decrement it
                    // when we are about to complete async execute reader.
                    SqlInternalConnectionTds internalConnectionTds = _activeConnection.GetOpenTdsConnection();
                    if (internalConnectionTds is not null)
                    {
                        internalConnectionTds.DecrementAsyncCount();
                        decrementAsyncCountInFinallyBlockAsync = false;
                    }

                    // Complete executereader.
                    describeParameterEncryptionDataReader = CompleteAsyncExecuteReader(
                        isInternal: false,
                        forDescribeParameterEncryption: true);
                    Debug.Assert(_stateObj is null, "non-null state object in PrepareForTransparentEncryption.");

                    // Read the results of describe parameter encryption.
                    ReadDescribeEncryptionParameterResults(
                        describeParameterEncryptionDataReader,
                        describeParameterEncryptionRpcOriginalRpcMap,
                        isRetry);

                    #if DEBUG
                    // Failpoint to force the thread to halt to simulate cancellation of SqlCommand.
                    if (_sleepAfterReadDescribeEncryptionParameterResults)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
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
                    PrepareTransparentEncryptionFinallyBlock(
                        closeDataReader: processFinallyBlockAsync,
                        decrementAsyncCount: decrementAsyncCountInFinallyBlockAsync,
                        clearDataStructures: processFinallyBlockAsync,
                        wasDescribeParameterEncryptionNeeded: describeParameterEncryptionNeeded,
                        describeParameterEncryptionRpcOriginalRpcMap: describeParameterEncryptionRpcOriginalRpcMap,
                        describeParameterEncryptionDataReader: describeParameterEncryptionDataReader);
                }
            });

            return describeParameterEncryptionDataReader;
        }

        private void InvalidateEnclaveSession()
        {
            if (ShouldUseEnclaveBasedWorkflow && enclavePackage != null)
            {
                EnclaveDelegate.Instance.InvalidateEnclaveSession(
                    _activeConnection.AttestationProtocol,
                    _activeConnection.Parser.EnclaveType,
                    GetEnclaveSessionParameters(),
                    enclavePackage.EnclaveSession);
            }
        }

        /// <summary>
        /// Executes the reader after checking to see if we need to encrypt input parameters and
        /// then encrypting it if required.
        /// * TryFetchInputParameterEncryptionInfo() ->
        /// * ReadDescribeEncryptionParameterResults() ->
        /// * EncryptInputParameters() ->
        /// * RunExecuteReaderTds()
        /// </summary>
        private void PrepareForTransparentEncryption(
            bool isAsync,
            int timeout,
            TaskCompletionSource<object> completion, // @TODO: Only used for debug checks
            out Task returnTask,
            bool asyncWrite,
            out bool usedCache,
            bool isRetry)
        {
            Debug.Assert(_activeConnection != null,
                "_activeConnection should not be null in PrepareForTransparentEncryption.");
            Debug.Assert(_activeConnection.Parser != null,
                "_activeConnection.Parser should not be null in PrepareForTransparentEncryption.");
            Debug.Assert(_activeConnection.Parser.IsColumnEncryptionSupported,
                "_activeConnection.Parser.IsColumnEncryptionSupported should be true in PrepareForTransparentEncryption.");
            Debug.Assert(_columnEncryptionSetting == SqlCommandColumnEncryptionSetting.Enabled
                         || (_columnEncryptionSetting == SqlCommandColumnEncryptionSetting.UseConnectionSetting && _activeConnection.IsColumnEncryptionSettingEnabled),
                "ColumnEncryption setting should be enabled for input parameter encryption.");
            Debug.Assert(isAsync == (completion != null),
                "completion should can be null if and only if mode is async.");

            // Fetch reader witn input params
            Task fetchInputParameterEncryptionInfoTask = null;
            bool describeParameterEncryptionNeeded = false;
            SqlDataReader describeParameterEncryptionDataReader = null;
            returnTask = null;
            usedCache = false;

            // If we are not in _batchRPC and not already retrying, attempt to fetch the cipher MD for each parameter from the cache.
            // If this succeeds then return immediately, otherwise just fall back to the full crypto MD discovery.
            if (!_batchRPCMode &&
                !isRetry &&
                _parameters?.Count > 0 &&
                SqlQueryMetadataCache.GetInstance().GetQueryMetadataIfExists(this))
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
                    describeParameterEncryptionDataReader = TryFetchInputParameterEncryptionInfo(
                        timeout,
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

                    Debug.Assert(describeParameterEncryptionDataReader != null,
                        "describeParameterEncryptionDataReader should not be null, as it is required to get results of describe parameter encryption.");

                    // If we are in async execution, we need to decrement our async count on exception.
                    decrementAsyncCountInFinallyBlock = isAsync;

                    // Fire up another task to read the results of describe parameter encryption
                    if (fetchInputParameterEncryptionInfoTask is not null)
                    {
                        // Mark that we should not process the finally block since we have async
                        // execution pending. Note that this should be done outside the task's
                        // continuation delegate.
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
                        // @TODO Make these else-if/else, or idk flip it around with the main if case
                        if (isAsync)
                        {
                            // If it was async, ending the reader is still pending
                            // Mark that we should not process the finally block since we have async
                            // execution pending. Note that this should be done outside the task's
                            // continuation delegate.
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
                            // For synchronous execution, read the results of describe parameter
                            // encryption here.
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
                    // @TODO: should this also check if processFinallyBlock has been cleared in the try?
                    processFinallyBlock = ADP.IsCatchableExceptionType(e);
                    exceptionCaught = true;
                    throw;
                }
                finally
                {
                    // Free up the state only for synchronous execution. For asynchronous
                    // execution, free only if there was an exception.
                    // @TODO: processFinallyBlock should probably switch this entire method?
                    PrepareTransparentEncryptionFinallyBlock(
                        closeDataReader: (processFinallyBlock && !isAsync) || exceptionCaught,
                        decrementAsyncCount: decrementAsyncCountInFinallyBlock && exceptionCaught,
                        clearDataStructures: (processFinallyBlock && !isAsync) || exceptionCaught,
                        wasDescribeParameterEncryptionNeeded: describeParameterEncryptionNeeded,
                        describeParameterEncryptionRpcOriginalRpcMap: describeParameterEncryptionRpcOriginalRpcMap,
                        describeParameterEncryptionDataReader: describeParameterEncryptionDataReader);
                }
            }
            catch (Exception e)
            {
                CachedAsyncState?.ResetAsyncState();
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
        private void PrepareTransparentEncryptionFinallyBlock(
            bool closeDataReader,
            bool clearDataStructures,
            bool decrementAsyncCount,
            bool wasDescribeParameterEncryptionNeeded, // @TODO: This isn't used anywhere
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            SqlDataReader describeParameterEncryptionDataReader)
        {
            if (clearDataStructures)
            {
                // Clear some state variables in SqlCommand that reflect in-progress describe
                // parameter encryption requests.
                ClearDescribeParameterEncryptionRequests();
                if (describeParameterEncryptionRpcOriginalRpcMap != null) // @TODO: This doesn't do anything
                {
                    describeParameterEncryptionRpcOriginalRpcMap = null;
                }
            }

            if (decrementAsyncCount)
            {
                // Decrement the async count
                SqlInternalConnectionTds internalConnection = _activeConnection.GetOpenTdsConnection();
                internalConnection?.DecrementAsyncCount();
            }

            if (closeDataReader)
            {
                // Close the data reader to reset the _stateObj
                describeParameterEncryptionDataReader?.Close();
            }
        }

        /// <summary>
        /// Read the output of sp_describe_parameter_encryption
        /// </summary>
        /// <param name="ds">Resultset from calling to sp_describe_parameter_encryption</param>
        /// <param name="describeParameterEncryptionRpcOriginalRpcMap"> Readonly dictionary with the map of parameter encryption rpc requests with the corresponding original rpc requests.</param>
        /// <param name="isRetry">Indicates if this is a retry from a failed call.</param>
        private void ReadDescribeEncryptionParameterResults(
            SqlDataReader ds, // @TODO: Rename something more obvious
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            bool isRetry)
        {
            // @TODO: This should be SqlTceCipherInfoTable
            Dictionary<int, SqlTceCipherInfoEntry> columnEncryptionKeyTable = new Dictionary<int, SqlTceCipherInfoEntry>();

            Debug.Assert((describeParameterEncryptionRpcOriginalRpcMap != null) == _batchRPCMode,
                "describeParameterEncryptionRpcOriginalRpcMap should be non-null if and only if it is _batchRPCMode.");

            // Indicates the current result set we are reading, used in BatchRPCMode, where we can have more than 1 result set.
            int resultSetSequenceNumber = 0;

            // A flag that used in BatchRPCMode, to assert the result of lookup in to the dictionary maintaining the map of describe parameter encryption requests
            // and the corresponding original rpc requests.
            bool lookupDictionaryResult;

            // @TODO: If this is supposed to read the results of sp_describe_parameter_encryption there should only ever be 2/3 result sets. So no need to loop this.
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

                // 1) Read the first result set that contains the column encryption key list
                bool enclaveMetadataExists = ReadDescribeEncryptionParameterResults1(ds, columnEncryptionKeyTable);
                if (!enclaveMetadataExists && !ds.NextResult())
                {
                    throw SQL.UnexpectedDescribeParamFormatParameterMetadata();
                }

                // 2) Find the RPC command that generated this TCE request
                _SqlRPC rpc;
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

                // 3) Read the second result set containing the cipher metadata
                int receivedMetadataCount = 0;
                if (!enclaveMetadataExists || ds.NextResult())
                {
                    receivedMetadataCount = ReadDescribeEncryptionParameterResults2(ds, rpc, columnEncryptionKeyTable);
                }

                // When the RPC object gets reused, the parameter array has more parameters that the valid params for the command.
                // Null is used to indicate the end of the valid part of the array. Refer to GetRPCObject().
                int userParamCount = rpc.userParams?.Count ?? 0;
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

                // 4) Read the third result set containing enclave attestation information
                if (ShouldUseEnclaveBasedWorkflow && (enclaveAttestationParameters != null) && requiresEnclaveComputations)
                {
                    if (!ds.NextResult())
                    {
                        throw SQL.UnexpectedDescribeParamFormatAttestationInfo(this._activeConnection.Parser.EnclaveType);
                    }

                    ReadDescribeEncryptionParameterResults3(ds, isRetry);
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

        private bool ReadDescribeEncryptionParameterResults1(
            SqlDataReader ds,
            Dictionary<int, SqlTceCipherInfoEntry> columnEncryptionKeyTable)
        {
            bool enclaveMetadataExists = true;
            while (ds.Read())
            {
                // Column encryption key ordinal
                int currentOrdinal = ds.GetInt32((int)DescribeParameterEncryptionResultSet1.KeyOrdinal);
                Debug.Assert(currentOrdinal >= 0, "currentOrdinal cannot be negative");

                // See if there was already an entry for the current ordinal, and if not create one.
                if (!columnEncryptionKeyTable.TryGetValue(currentOrdinal, out SqlTceCipherInfoEntry cipherInfoEntry))
                {
                    cipherInfoEntry = new SqlTceCipherInfoEntry(currentOrdinal);
                    columnEncryptionKeyTable.Add(currentOrdinal, cipherInfoEntry);
                }

                Debug.Assert(cipherInfoEntry is not null, "cipherInfoEntry should not be un-initialized.");

                // Read the column encryption key
                // @TODO: This pattern is used quite a bit - can we turn it into a helper or extension of SqlDataReader?
                int encryptedKeyLength = (int)ds.GetBytes(
                    (int)DescribeParameterEncryptionResultSet1.EncryptedKey,
                    dataIndex: 0,
                    buffer: null,
                    bufferIndex: 0,
                    length: 0);
                byte[] encryptedKey = new byte[encryptedKeyLength];
                ds.GetBytes(
                    (int)DescribeParameterEncryptionResultSet1.EncryptedKey,
                    dataIndex: 0,
                    buffer: encryptedKey,
                    bufferIndex: 0,
                    length: encryptedKeyLength);

                // Read the metadata version of the key. It should always be 8 bytes.
                // @TODO: We have so many asserts on the structure of this data, should we have one here too??
                byte[] keyMdVersion = new byte[8];
                ds.GetBytes(
                    (int)DescribeParameterEncryptionResultSet1.KeyMdVersion,
                    dataIndex: 0,
                    buffer: keyMdVersion,
                    bufferIndex: 0,
                    length: keyMdVersion.Length);

                // Read the provider name (key store name)
                string providerName = ds.GetString((int)DescribeParameterEncryptionResultSet1.ProviderName);

                // Read the key path
                string keyPath = ds.GetString((int)DescribeParameterEncryptionResultSet1.KeyPath);

                cipherInfoEntry.Add(
                    encryptedKey: encryptedKey,
                    databaseId: ds.GetInt32((int)DescribeParameterEncryptionResultSet1.DbId),
                    cekId: ds.GetInt32((int)DescribeParameterEncryptionResultSet1.KeyId),
                    cekVersion: ds.GetInt32((int)DescribeParameterEncryptionResultSet1.KeyVersion),
                    cekMdVersion: keyMdVersion,
                    keyPath: keyPath,
                    keyStoreName: providerName,
                    algorithmName: ds.GetString((int)DescribeParameterEncryptionResultSet1.KeyEncryptionAlgorithm));

                // Servers supporting enclave computations should always return a boolean
                // indicating whether the key is required by enclave or not.
                // @TODO: Do we need to make this check for each row? I doubt it.
                bool isRequestedByEnclave = false;
                if (_activeConnection.Parser.TceVersionSupported >= TdsEnums.MIN_TCE_VERSION_WITH_ENCLAVE_SUPPORT)
                {
                    isRequestedByEnclave = ds.GetBoolean((int)DescribeParameterEncryptionResultSet1.IsRequestedByEnclave);
                }
                else
                {
                    enclaveMetadataExists = false;
                }

                if (isRequestedByEnclave)
                {
                    if (string.IsNullOrWhiteSpace(_activeConnection.EnclaveAttestationUrl) &&
                        _activeConnection.AttestationProtocol != SqlConnectionAttestationProtocol.None)
                    {
                        throw SQL.NoAttestationUrlSpecifiedForEnclaveBasedQuerySpDescribe(
                            _activeConnection.Parser.EnclaveType);
                    }

                    byte[] keySignature = null;
                    if (!ds.IsDBNull((int)DescribeParameterEncryptionResultSet1.KeySignature))
                    {
                        int keySignatureLength = (int)ds.GetBytes(
                            (int)DescribeParameterEncryptionResultSet1.KeySignature,
                            dataIndex: 0,
                            buffer: null,
                            bufferIndex: 0,
                            length: 0);
                        keySignature = new byte[keySignatureLength];
                        ds.GetBytes(
                            (int)DescribeParameterEncryptionResultSet1.KeySignature,
                            dataIndex: 0,
                            buffer: keySignature,
                            bufferIndex: 0,
                            length: keySignatureLength);
                    }

                    SqlSecurityUtility.VerifyColumnMasterKeySignature(
                        providerName,
                        keyPath,
                        isEnclaveEnabled: true,
                        keySignature,
                        _activeConnection,
                        this);

                    // Lookup the key, failing which throw an exception
                    // @TODO: Seriously, we *just* did this, why are we looking it up again??
                    if (!columnEncryptionKeyTable.TryGetValue(currentOrdinal, out SqlTceCipherInfoEntry cipherInfo))
                    {
                        throw SQL.InvalidEncryptionKeyOrdinalEnclaveMetadata(
                            currentOrdinal,
                            columnEncryptionKeyTable.Count);
                    }

                    // @TODO: 1) storing this as Command state seems fishy
                    // @TODO: 2) despite being concurrent, the usage of ContainsKey -> TryAdd is a race condition
                    // @TODO: 3) we have SqlTceCipherInfoTable, we should use it - or make it usable.
                    // @TODO: 4) even if we're supposed to store it as state, is the intention to obliterate the list each time? If so, we should probably store it locally and replace the state obj at the end.
                    if (keysToBeSentToEnclave is null)
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

        private int ReadDescribeEncryptionParameterResults2(
            SqlDataReader ds,
            _SqlRPC rpc,
            Dictionary<int, SqlTceCipherInfoEntry> columnEncryptionKeyTable)
        {
            Debug.Assert(rpc is not null, "Describe Parameter Encryption requested for non-TCE spec proc");

            int receivedMetadataCount = 0;
            int userParamCount = rpc.userParams?.Count ?? 0; // @TODO: Make this a property on _SqlRPC

            while (ds.Read())
            {
                string parameterName = ds.GetString((int)DescribeParameterEncryptionResultSet2.ParameterName);

                // When the RPC object gets reused, the parameter array has more parameters than
                // the valid params for the command. Null is used to indicate the end of the valid
                // part of the array. Refer to GetRPCObject().
                for (int index = 0; index < userParamCount; index++)
                {
                    SqlParameter sqlParameter = rpc.userParams[index];
                    Debug.Assert(sqlParameter is not null, "sqlParameter should not be null.");

                    // @TODO: And what happens if they're not in the same order?
                    // @TODO: Invert if statement based on answer to above TODO
                    if (SqlParameter.ParameterNamesEqual(sqlParameter.ParameterName, parameterName))
                    {
                        Debug.Assert(sqlParameter.CipherMetadata is not null, "param.CipherMetaData should not be null.");

                        sqlParameter.HasReceivedMetadata = true;
                        receivedMetadataCount++;

                        // Found the param, set up the encryption info.
                        byte columnEncryptionType = ds.GetByte((int)DescribeParameterEncryptionResultSet2.ColumnEncryptionType);
                        if (columnEncryptionType != (byte)SqlClientEncryptionType.PlainText)
                        {
                            byte cipherAlgorithmId = ds.GetByte(
                                (int)DescribeParameterEncryptionResultSet2.ColumnEncryptionAlgorithm);
                            int columnEncryptionKeyOrdinal = ds.GetInt32(
                                (int)DescribeParameterEncryptionResultSet2.ColumnEncryptionKeyOrdinal);
                            byte columnNormalizationRuleVersion = ds.GetByte(
                                (int)DescribeParameterEncryptionResultSet2.NormalizationRuleVersion);

                            // Lookup the key, failing which throw an exception
                            if (!columnEncryptionKeyTable.TryGetValue(columnEncryptionKeyOrdinal, out SqlTceCipherInfoEntry cipherInfoEntry))
                            {
                                throw SQL.InvalidEncryptionKeyOrdinalParameterMetadata(
                                    columnEncryptionKeyOrdinal,
                                    columnEncryptionKeyTable.Count);
                            }

                            sqlParameter.CipherMetadata = new SqlCipherMetadata(
                                sqlTceCipherInfoEntry: cipherInfoEntry,
                                ordinal: unchecked((ushort)-1),
                                cipherAlgorithmId: cipherAlgorithmId,
                                cipherAlgorithmName: null,
                                encryptionType: columnEncryptionType,
                                normalizationRuleVersion: columnNormalizationRuleVersion);

                            // Decrypt the symmetric key. This will also validate and throw if needed.
                            SqlSecurityUtility.DecryptSymmetricKey(sqlParameter.CipherMetadata, _activeConnection, this);

                            // This is effective only for _batchRPCMode even though we set it for
                            // non-_batchRPCMode also, since for non-_batchRPCMode, param options
                            // gets thrown away and reconstructed in BuildExecuteSql.
                            // @TODO: I bet we could make this a bit cleaner
                            int options = (int)(rpc.userParamMap[index] >> 32);
                            options |= TdsEnums.RPC_PARAM_ENCRYPTED;
                            rpc.userParamMap[index] = ((long)options << 32) | (long)index;
                        }

                        break;
                    }
                }
            }

            return receivedMetadataCount;
        }

        private void ReadDescribeEncryptionParameterResults3(SqlDataReader ds, bool isRetry)
        {
            bool attestationInfoRead = false;
            while (ds.Read())
            {
                if (attestationInfoRead)
                {
                    throw SQL.MultipleRowsReturnedForAttestationInfo();
                }

                int attestationInfoLength = (int)ds.GetBytes(
                    (int)DescribeParameterEncryptionResultSet3.AttestationInfo,
                    dataIndex: 0,
                    buffer: null,
                    bufferIndex: 0,
                    length: 0);
                byte[] attestationInfo = new byte[attestationInfoLength];
                ds.GetBytes(
                    (int)DescribeParameterEncryptionResultSet3.AttestationInfo,
                    dataIndex: 0,
                    buffer: attestationInfo,
                    bufferIndex: 0,
                    length: attestationInfoLength);

                SqlConnectionAttestationProtocol attestationProtocol = _activeConnection.AttestationProtocol;
                string enclaveType = _activeConnection.Parser.EnclaveType;

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
                throw SQL.AttestationInfoNotReturnedFromSqlServer(
                    _activeConnection.Parser.EnclaveType,
                    _activeConnection.EnclaveAttestationUrl);
            }
        }

        /// <summary>
        /// Resets the encryption related state of the command object and each of the parameters.
        /// BatchRPC doesn't need special handling to clean up the state of each RPC object and its
        /// parameters since a new RPC object and parameters are generated on every execution.
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
        /// Set the column encryption setting to the new one. Do not allow conflicting column
        /// encryption settings.
        /// @TODO: This basically just allows it to be set once and it cannot be changed after.
        /// </summary>
        private void SetColumnEncryptionSetting(SqlCommandColumnEncryptionSetting newColumnEncryptionSetting)
        {
            // @TODO: Why do we need a flag *and* the value itself. The value hasn't been set if it's null!
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
