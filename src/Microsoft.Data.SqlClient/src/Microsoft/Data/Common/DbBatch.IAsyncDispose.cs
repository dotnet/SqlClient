// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Data.Common
{
    public partial class DbBatch : IAsyncDisposable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/DisposeAsync/*'/>
        public virtual ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
}
