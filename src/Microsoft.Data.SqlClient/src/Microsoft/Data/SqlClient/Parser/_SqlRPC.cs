// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser;

internal sealed class _SqlRPC
{
    internal string rpcName;
    internal ushort ProcID;       // Used instead of name
    internal ushort options;

    internal SqlParameter[] systemParams;
    internal byte[] systemParamOptions;
    internal int systemParamCount;

    internal SqlParameterCollection userParams;
    internal long[] userParamMap;
    internal int userParamCount;

    internal int? recordsAffected;
    internal int cumulativeRecordsAffected;

    internal int errorsIndexStart;
    internal int errorsIndexEnd;
    internal SqlErrorCollection errors;

    internal int warningsIndexStart;
    internal int warningsIndexEnd;
    internal SqlErrorCollection warnings;

    internal bool needsFetchParameterEncryptionMetadata;

    internal SqlBatchCommand batchCommand;

    internal string GetCommandTextOrRpcName()
    {
        if (TdsEnums.RPC_PROCID_EXECUTESQL == ProcID)
        {
            // Param 0 is the actual sql executing
            return (string)systemParams[0].Value;
        }
        else
        {
            return rpcName;
        }
    }

    internal SqlParameter GetParameterByIndex(int index, out byte options)
    {
        SqlParameter retval;

        if (index < systemParamCount)
        {
            retval = systemParams[index];
            options = systemParamOptions[index];
        }
        else
        {
            long data = userParamMap[index - systemParamCount];
            int paramIndex = (int)(data & int.MaxValue);
            options = (byte)((data >> 32) & 0xFF);
            retval = userParams[paramIndex];
        }
        return retval;
    }
}
