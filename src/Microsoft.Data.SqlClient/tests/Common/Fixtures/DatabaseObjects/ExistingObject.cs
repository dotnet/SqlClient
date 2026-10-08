// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// Selects the constructor that adopts an object which already exists on the server, rather than
/// creating one.
/// </summary>
/// <remarks>
/// The distinction — create this object, or take ownership of one that is already there — cannot be
/// expressed in the signature, since both take the same arguments. A discriminator states it at the
/// call site, which a <c>bool</c> would not; the corresponding public entry point is the
/// <c>AdoptExisting</c> factory.
/// </remarks>
public enum ExistingObject
{
    Adopt
}
