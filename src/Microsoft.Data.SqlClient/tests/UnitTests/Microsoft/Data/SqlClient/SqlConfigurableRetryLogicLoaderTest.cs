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

    /// <summary>
    /// The path a custom retry logic type is actually resolved on. This is the only path that
    /// legitimately installs the probing handler, and it must be removed again once resolution
    /// has finished.
    /// </summary>
    /// <remarks>
    /// The retry logic type is resolved out of this test assembly, which is reached through the
    /// loader's probing directory rather than through normal assembly resolution. The invocation
    /// counter confirms the configured type really was resolved and used, so this is exercising
    /// the successful branch of type resolution rather than silently falling back to the built-in
    /// factory.
    /// </remarks>
    [Fact]
    public void Constructor_WithResolvableRetryLogicType_DoesNotLeaveAssemblyProbingEnabled()
    {
        Assembly testAssembly = typeof(SqlConfigurableRetryLogicLoaderTest).Assembly;
        string assemblySimpleName = testAssembly.GetName().Name!;

        // The loader probes for '<simple name>.dll'. The test assembly's file name does not
        // necessarily match its simple name, so make a copy that does.
        string probePath = Path.Combine(AppContext.BaseDirectory, assemblySimpleName + ".dll");
        bool copied = false;
        if (!File.Exists(probePath))
        {
            File.Copy(testAssembly.Location, probePath);
            copied = true;
        }

        try
        {
            TestRetryConnectionSection section = CreateSection(
                $"{typeof(ProbedRetryLogicFactory).FullName}, {assemblySimpleName}");
            section.RetryMethod = nameof(ProbedRetryLogicFactory.CreateProbedRetryProvider);

            ProbedRetryLogicFactory.InvocationCount = 0;

            SqlConfigurableRetryLogicLoader loader = new(section, null);

            Assert.Equal(1, ProbedRetryLogicFactory.InvocationCount);
            Assert.NotNull(loader.ConnectionProvider);
            AssertNoAssemblyProbingHandlerInstalled();
        }
        finally
        {
            if (copied)
            {
                DeleteProbeFile(probePath);
            }
        }
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
        string assemblySimpleName = NewProbeAssemblyName();
        string plantedFile = PlantProbeFile(assemblySimpleName);

        try
        {
            Assert.False(
                IsProbingHandlerInstalled(assemblySimpleName),
                "A resolving handler that probes the loader's probing directory is subscribed to " +
                "the default assembly load context.");
        }
        finally
        {
            DeleteProbeFile(plantedFile);
        }
    }

    /// <summary>
    /// Reports whether a resolving handler that serves assemblies out of the loader's probing
    /// directory is currently subscribed to <see cref="AssemblyLoadContext.Default"/>.
    /// </summary>
    /// <remarks>
    /// This distinguishes the two states using only public behaviour, which is what actually
    /// matters to a host application. With such a handler subscribed the planted file is found
    /// and an attempt is made to load it, which fails as
    /// <see cref="BadImageFormatException"/> because it is not a valid assembly. With no such
    /// handler subscribed the runtime never looks in that directory and reports the assembly as
    /// simply not found.
    /// </remarks>
    private static bool IsProbingHandlerInstalled(string assemblySimpleName)
    {
        try
        {
            Assembly.Load(new AssemblyName(assemblySimpleName));

            // Unreachable: the planted file is deliberately not a valid assembly, so a handler
            // that found it cannot have loaded it successfully.
            return true;
        }
        catch (BadImageFormatException)
        {
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static string NewProbeAssemblyName() =>
        "MdsProbeAssembly_" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Plants a file that is not a valid assembly in the loader's probing directory, under a
    /// name no other component could be asking for.
    /// </summary>
    private static string PlantProbeFile(string assemblySimpleName)
    {
        // The probing directory is the application base directory, which for a test run is the
        // directory the test assembly was loaded from.
        string plantedFile = Path.Combine(AppContext.BaseDirectory, assemblySimpleName + ".dll");

        File.WriteAllText(plantedFile, "not an assembly");

        return plantedFile;
    }

    /// <summary>
    /// Removes a planted probe file. Cleanup failures are ignored so that a test reports on
    /// product behaviour rather than on the state of the file system.
    /// </summary>
    private static void DeleteProbeFile(string plantedFile)
    {
        try
        {
            File.Delete(plantedFile);
        }
        catch (IOException)
        {
            // The file is in use, most likely because it was successfully loaded as an assembly
            // and is therefore locked for the lifetime of the process.
        }
        catch (UnauthorizedAccessException)
        {
            // The file is read only, or the caller lacks permission to delete it.
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

/// <summary>
/// A retry logic factory that is resolved through the loader's probing directory rather than
/// through normal assembly resolution, so tests can tell a successful custom type resolution
/// apart from a silent fallback to the built-in factory.
/// </summary>
/// <remarks>
/// This has to be a public, non-nested type because the loader discovers candidates by
/// enumerating the resolved assembly's exported types.
/// </remarks>
public static class ProbedRetryLogicFactory
{
    internal static int InvocationCount;

    public static SqlRetryLogicBaseProvider CreateProbedRetryProvider(SqlRetryLogicOption option)
    {
        InvocationCount++;

        return SqlConfigurableRetryFactory.CreateFixedRetryProvider(option);
    }
}

#endif
