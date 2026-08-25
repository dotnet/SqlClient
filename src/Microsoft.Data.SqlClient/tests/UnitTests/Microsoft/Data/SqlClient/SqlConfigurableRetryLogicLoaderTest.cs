// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Unit tests validating that <see cref="SqlConfigurableRetryLogicLoader"/> never leaves a
/// process-wide assembly resolving handler attached to
/// <see cref="AssemblyLoadContext.Default"/>.
/// </summary>
/// <remarks>
/// A handler left attached there participates in resolution of every assembly the host
/// application fails to find, not just the retry logic assembly the loader was interested in.
/// That silently changes assembly loading behaviour for code that never opted in to this
/// feature, and can serve unrelated assemblies out of this component's probing directory.
/// </remarks>
public class SqlConfigurableRetryLogicLoaderTest
{
    /// <summary>
    /// The default code path: no configuration at all. The loader must not subscribe to the
    /// default load context.
    /// </summary>
    [Fact]
    public void Constructor_WithNoConfiguration_DoesNotLeaveAssemblyProbingEnabled()
    {
        _ = new SqlConfigurableRetryLogicLoader(null, null);

        AssertNoAssemblyProbingHandlerInstalled();
    }

    /// <summary>
    /// A configuration that supplies only a retry method, which is the documented way to select
    /// one of the built-in retry providers. No custom type is being requested, so no assembly
    /// resolution is required and no handler may be subscribed - not even transiently.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_WithoutRetryLogicType_DoesNotLeaveAssemblyProbingEnabled(string? retryLogicType)
    {
        TestRetryConnectionSection section = CreateSection(retryLogicType);

        SqlConfigurableRetryLogicLoader loader = new(section, null);

        // The built-in factory still resolves the requested method.
        Assert.NotNull(loader.ConnectionProvider);
        AssertNoAssemblyProbingHandlerInstalled();
    }

    /// <summary>
    /// A configuration that requests a custom retry logic type legitimately needs assembly
    /// resolution, but the handler must be removed again once type resolution has finished.
    /// </summary>
    [Fact]
    public void Constructor_WithUnresolvableRetryLogicType_DoesNotLeaveAssemblyProbingEnabled()
    {
        TestRetryConnectionSection section =
            CreateSection("Some.Namespace.NoSuchType, Some.Assembly.That.Does.Not.Exist");

        SqlConfigurableRetryLogicLoader loader = new(section, null);

        // Resolution fails and falls back to the built-in factory rather than throwing.
        Assert.NotNull(loader.ConnectionProvider);
        AssertNoAssemblyProbingHandlerInstalled();
    }

    private static TestRetryConnectionSection CreateSection(string? retryLogicType) =>
        new()
        {
            RetryLogicType = retryLogicType!,
            RetryMethod = nameof(SqlConfigurableRetryFactory.CreateFixedRetryProvider),
            NumberOfTries = 2,
            DeltaTime = TimeSpan.FromSeconds(1),
            MinTimeInterval = TimeSpan.Zero,
            MaxTimeInterval = TimeSpan.FromSeconds(10),
        };

    /// <summary>
    /// Asserts that a failed assembly load is not served out of the loader's probing directory,
    /// which can only happen while a resolving handler installed by
    /// <see cref="SqlConfigurableRetryLogicLoader"/> is subscribed to
    /// <see cref="AssemblyLoadContext.Default"/>.
    /// </summary>
    /// <remarks>
    /// A file that is not a valid assembly is planted in the probing directory under a name no
    /// other component could be asking for. If a handler is still subscribed it finds that file
    /// and tries to load it, which surfaces as <see cref="BadImageFormatException"/>. With no
    /// handler subscribed the runtime never looks there and reports the assembly as simply not
    /// found. This asserts the behaviour that actually matters to a host application rather than
    /// inspecting loader or runtime internals.
    /// </remarks>
    private static void AssertNoAssemblyProbingHandlerInstalled()
    {
        // The probing directory is the application base directory, which for a test run is the
        // directory the test assembly was loaded from.
        string assemblySimpleName = "MdsProbeAssembly_" + Guid.NewGuid().ToString("N");
        string plantedFile = Path.Combine(AppContext.BaseDirectory, assemblySimpleName + ".dll");

        File.WriteAllText(plantedFile, "not an assembly");
        try
        {
            Assert.Throws<FileNotFoundException>(
                () => Assembly.Load(new AssemblyName(assemblySimpleName)));
        }
        finally
        {
            try
            {
                File.Delete(plantedFile);
            }
            catch (IOException)
            {
                // Best effort cleanup.
            }
        }
    }

    private sealed class TestRetryConnectionSection : ISqlConfigurableRetryConnectionSection
    {
        public TimeSpan DeltaTime { get; set; }

        public TimeSpan MaxTimeInterval { get; set; }

        public TimeSpan MinTimeInterval { get; set; }

        public int NumberOfTries { get; set; }

        public string RetryLogicType { get; set; } = string.Empty;

        public string RetryMethod { get; set; } = string.Empty;

        public string TransientErrors { get; set; } = string.Empty;
    }
}

#endif
