// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

namespace Microsoft.Data.SqlClient.Server
{
    // Enum representing the type of execution requested
    internal enum SmiExecuteType
    {
        NonQuery = 0,
        Reader = 1,
        ToPipe = 2,
    }
}

#endif
