// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.LocalDb
{
    internal static class LocalDbApi
    {
        internal static string GetLocalDbInstanceNameFromServerName(string serverName) =>
            null;

        internal static string GetLocalDbMessage(int hrCode) =>
            throw new PlatformNotSupportedException(Strings.LocalDBNotSupported); // LocalDB is not available for Unix and hence it cannot be supported.
    }
}
