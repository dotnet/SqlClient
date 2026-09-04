// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

/// <summary>
/// Distinguishes a constructor that takes a caller-supplied name verbatim from the prefix-based
/// one, which would otherwise have an identical signature.
/// </summary>
/// <remarks>
/// The two overloads differ only in how the string is interpreted — as a name or as a prefix to
/// generate one from — which the type system cannot express. This makes the private constructor
/// unambiguous without inventing a parameter that carries no meaning; the corresponding public
/// entry point is the <c>WithName</c> factory, where the distinction is stated in the name.
/// </remarks>
internal enum NameIsVerbatim
{
    Yes
}
