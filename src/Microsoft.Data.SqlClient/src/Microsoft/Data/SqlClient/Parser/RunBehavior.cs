// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.Parser;

internal enum RunBehavior
{
    UntilDone = 1, // 0001 binary
    ReturnImmediately = 2, // 0010 binary
    Clean = 5, // 0101 binary - Clean AND UntilDone
    Attention = 13  // 1101 binary - Clean AND UntilDone AND Attention
}
