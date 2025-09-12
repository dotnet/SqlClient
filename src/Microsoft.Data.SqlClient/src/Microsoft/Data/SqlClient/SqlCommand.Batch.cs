// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    // @TODO: There's a good question here - should this be a separate type of SqlCommand?
    public sealed partial class SqlCommand
    {
        #region Internal Methods

        internal void AddBatchCommand(SqlBatchCommand batchCommand)
        {
            Debug.Assert(_batchRPCMode, "Command is not in batch RPC Mode");
            Debug.Assert(_RPCList is not null);

            _SqlRPC rpc = new _SqlRPC { batchCommand = batchCommand };
            string commandText = batchCommand.CommandText;
            CommandType cmdType = batchCommand.CommandType;

            CommandText = commandText;
            CommandType = cmdType;

            SetColumnEncryptionSetting(batchCommand.ColumnEncryptionSetting);

            // @TODO: Hmm, maybe we could have get/put become a IDisposable thing
            GetStateObject();
            if (cmdType is CommandType.StoredProcedure)
            {
                BuildRPC(inSchema: false, batchCommand.Parameters, ref rpc);
            }
            else
            {
                // All batch sql statements must be executed inside sp_executesql, including those
                // without parameters
                BuildExecuteSql(CommandBehavior.Default, commandText, batchCommand.Parameters, ref rpc);
            }

            _RPCList.Add(rpc);
            ReliablePutStateObject();
        }

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

        // @TODO: Rename to match naming conventions
        internal void SetBatchRPCMode(bool value, int commandCount = 1)
        {
            _batchRPCMode = value;
            ClearBatchCommand();
            if (_batchRPCMode)
            {
                if (_RPCList is null)
                {
                    // @TODO: Could this be done with an array?
                    _RPCList = new List<_SqlRPC>(commandCount);
                }
                else
                {
                    _RPCList.Capacity = commandCount;
                }
            }
        }

        // @TODO: Rename to match naming conventions
        internal void SetBatchRPCModeReadyToExecute()
        {
            Debug.Assert(_batchRPCMode, "Command is not in batch RPC Mode");
            Debug.Assert(_RPCList is not null, "No batch commands specified");

            _currentlyExecutingBatch = 0;
        }

        #endregion

        #region Private Methods

        private void ClearBatchCommand()
        {
            _RPCList?.Clear();
            _currentlyExecutingBatch = 0;
        }

        #endregion
    }
}
