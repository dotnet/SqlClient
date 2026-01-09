// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
