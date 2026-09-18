// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.AlwaysEncrypted
{
    /// <summary>
    /// Encryption types supported in TCE. Corresponds to EncryptionAlgoType in MS-TDS.
    /// </summary>
    /// <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-tds/7091f6f6-b83d-4ed2-afeb-ba5013dfb18f"/>
    internal enum EncryptionType
    {
        PlainText = 0x00,
        Deterministic = 0x01,
        Randomized = 0x02
    }
}
