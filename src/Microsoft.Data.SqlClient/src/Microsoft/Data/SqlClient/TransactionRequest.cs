// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Enum representing all of the possible transaction operations.
/// Only used internally, mapped to <see cref="TdsEnums.TransactionManagerRequestType"/> for transmission
/// over the wire.
/// </summary>
internal enum TransactionRequest
{
    Begin,
    Promote,
    Commit,
    Rollback,
    // Mapped to Rollback; server completion notifications make a conditional rollback unnecessary.
    IfRollback,
    Save
}
