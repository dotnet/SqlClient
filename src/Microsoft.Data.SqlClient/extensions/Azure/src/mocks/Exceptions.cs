// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure;

internal static class SQL
{
    public static TimeoutException ActiveDirectoryInteractiveTimeout()
    {
        return new TimeoutException("Active Directory Interactive authentication timed out.");
    }
    public static TimeoutException ActiveDirectoryDeviceFlowTimeout()
    {
        return new TimeoutException("Active Directory Device Flow authentication timed out.");
    }
    public static InvalidOperationException UnsupportedAuthenticationSpecified(SqlAuthenticationMethod method)
    {
        return new InvalidOperationException($"Unsupported authentication method={method} specified.");
    }
}
