// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Serializes tests that mutate process-wide cached AppContext switch values so other test
/// collections cannot observe temporary settings.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppContextSwitchTestCollection
{
    public const string Name = "AppContextSwitchTests";
}

/// <summary>
/// Serializes simulated-server tests with all other collections because some scenarios mutate
/// process-wide cached AppContext switch values while exercising connections.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SimulatedServerTestCollection
{
    public const string Name = "SimulatedServerTests";
}
