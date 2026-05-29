using Xunit;

namespace Microsoft.Data.SqlClient.Tools.PackageCompatibility.Tests;

/// <summary>
/// xUnit collection definition used by tests that temporarily redirect global console streams.
/// Parallelization is disabled so tests that mutate <see cref="System.Console.Out"/> and
/// <see cref="System.Console.Error"/> cannot interfere with one another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCollection : ICollectionFixture<ConsoleCollectionFixture>
{
    /// <summary>
    /// Stable collection name referenced by console-mutating test classes.
    /// </summary>
    public const string Name = "Console";
}

/// <summary>
/// Fixture type associated with <see cref="ConsoleCollection"/>.
/// Present to satisfy xUnit collection fixture wiring; individual test classes in this collection
/// capture and restore console streams in their own constructor/Dispose lifecycle to keep each
/// test isolated.
/// </summary>
public sealed class ConsoleCollectionFixture
{
}