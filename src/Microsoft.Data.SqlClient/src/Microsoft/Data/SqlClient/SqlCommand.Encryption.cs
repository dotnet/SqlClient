// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
                // @TODO: RowsAffected++;

                string parameterName = ds.GetString((int)DescribeParameterEncryptionResultSet2.ParameterName);

                // When the RPC object gets reused, the parameter array has more parameters than
                // the valid params for the command. Null is used to indicate the end of the valid
                // part of the array. Refer to GetRPCObject().
                for (int index = 0; index < userParamCount; index++)
                {
                    SqlParameter sqlParameter = rpc.userParams[index];
                    Debug.Assert(sqlParameter is not null, "sqlParameter should not be null.");

                    // @TODO: And what happens if they're not in the same order?
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
                            bool cipherInfoEntryFound = columnEncryptionKeyTable.TryGetValue(
                                columnEncryptionKeyOrdinal,
                                out SqlTceCipherInfoEntry cipherInfoEntry);
                            if (!cipherInfoEntryFound)
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
    }
}
