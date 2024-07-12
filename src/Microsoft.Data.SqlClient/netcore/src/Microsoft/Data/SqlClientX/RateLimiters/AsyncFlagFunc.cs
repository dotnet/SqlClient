using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.RateLimiters
{
    internal delegate TResult AsyncFlagFunc<out TResult>(bool isAsync, CancellationToken cancellationToken);
}
