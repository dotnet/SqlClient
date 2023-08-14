// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal class LocalDB
    {
        internal static string GetLocalDBConnectionString(string localDbInstance)
        {
            throw ADP.LocalDBNotSUpportedException();
        }

        internal static string GetLocalDBDataSource(string fullServerName, TimeoutTimer timeout)
        {
            throw ADP.LocalDBNotSUpportedException();
        }
    }
}
