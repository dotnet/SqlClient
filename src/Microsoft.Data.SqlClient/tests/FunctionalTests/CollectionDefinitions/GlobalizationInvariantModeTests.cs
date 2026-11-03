// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.Tests.CollectionDefinitions;

/// <summary>
/// Groups and serializes all tests which change the process-wide globalization invariant mode.
/// </summary>
[CollectionDefinition(nameof(GlobalizationInvariantModeTests), DisableParallelization = true)]
public class GlobalizationInvariantModeTests
{
}
