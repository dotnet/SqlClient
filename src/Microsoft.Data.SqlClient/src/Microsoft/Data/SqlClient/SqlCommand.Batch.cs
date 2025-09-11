// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    // @TODO: There's a good question here - should this be a separate type of SqlCommand?
    public sealed partial class SqlCommand
    {
        #region Internal Methods

        internal SqlBatchCommand GetBatchCommand(int index) =>
            _RPCList[index].batchCommand;

        // @TODO: This should be a property.
        internal SqlBatchCommand GetCurrentBatchCommand()
        {
            return _batchRPCMode
                ? _RPCList[_currentlyExecutingBatch].batchCommand
                : _rpcArrayOf1?[0].batchCommand;
        }

        // @TODO: 1) This should be a property
        // @TODO: 2) This could be a `int?`
        internal int GetCurrentBatchIndex() =>
            _batchRPCMode ? _currentlyExecutingBatch : -1;

        // @TODO: Indicate this is for batch RPC usage
        internal SqlException GetErrors(int commandIndex)
        {
            SqlException result = null;

            _SqlRPC rpc = _RPCList[commandIndex];
            int length = rpc.errorsIndexEnd - rpc.errorsIndexStart;
            if (length > 0)
            {
                SqlErrorCollection errors = new SqlErrorCollection();
                for (int i = rpc.errorsIndexStart; i < rpc.errorsIndexEnd; i++)
                {
                    errors.Add(rpc.errors[i]);
                }
                for (int i = rpc.warningsIndexStart; i < rpc.warningsIndexEnd; i++)
                {
                    errors.Add(rpc.warnings[i]);
                }

                result = SqlException.CreateException(
                    errors,
                    _activeConnection.ServerVersion,
                    _activeConnection.ClientConnectionId,
                    innerException: null,
                    batchCommand: null);
            }

            return result;
        }

        // @TODO: Should be renamed to indicate only applies to batch RPC mode
        internal int? GetRecordsAffected(int commandIndex)
        {
            Debug.Assert(_batchRPCMode, "Command is not in batch RPC mode");
            Debug.Assert(_RPCList is not null, "Batch commands have been cleared");
            return _RPCList[commandIndex].recordsAffected;
        }

        #endregion
    }
}
