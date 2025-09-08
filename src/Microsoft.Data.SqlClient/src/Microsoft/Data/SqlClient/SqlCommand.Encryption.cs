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
    }
}
