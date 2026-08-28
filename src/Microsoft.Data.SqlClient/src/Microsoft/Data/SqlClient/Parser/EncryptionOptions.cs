// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.Parser;

internal enum EncryptionOptions
{
    OFF,
    ON,
    NOT_SUP,
    REQ,
    LOGIN,
    OPTIONS_MASK = 0x3f,

    #if NETFRAMEWORK
    CTAIP = 0x40,
    CLIENT_CERT = 0x80,
    #endif
}
