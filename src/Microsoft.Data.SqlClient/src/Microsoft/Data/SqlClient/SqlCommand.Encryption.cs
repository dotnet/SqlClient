// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
    }
}
