using System;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
    {
        private Func<object> _parentActivityOrWindowFunc = null;

        private Func<object> ParentActivityOrWindow => _parentActivityOrWindowFunc;
    }
}
