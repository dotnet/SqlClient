// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Server
{

    // enum representing the type of execution requested
    internal enum SmiExecuteType
    {
        NonQuery = 0,
        Reader = 1,
        ToPipe = 2,
    }
}

