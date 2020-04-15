// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Encryption types supported in TCE
    /// </summary>
    internal enum SqlClientEncryptionType
    {
        PlainText = 0,
        Deterministic,
        Randomized
    }
}
