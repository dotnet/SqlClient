using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SqlClientX.TDS
{
    internal class TdsTokenPipeline
    {

        internal bool HasMoreData { get; private set; }

        internal async ValueTask ReadOnce(byte expectedTokenOnSingleRead,
            bool isAsync,
            CancellationToken ct)
        {
            await ValueTask.CompletedTask;
            throw new NotImplementedException();
        }

        internal async ValueTask ReadUntilDoneToken(bool isAsync, CancellationToken ct)
        {
            await ValueTask.CompletedTask;
            throw new NotImplementedException();
        }

        internal async ValueTask<bool> PeekNextToken(bool isAsync, CancellationToken ct)
        {
            await ValueTask.CompletedTask;
            throw new NotImplementedException();
        }
    }
}
