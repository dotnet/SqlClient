// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Linq;
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
    public void Constructor_WithNoConfiguration_DoesNotSubscribeToDefaultLoadContext()
    {
        _ = new SqlConfigurableRetryLogicLoader(null, null);

        AssertNoLoaderResolvingHandlerAttached();
    }

    /// <summary>
    /// A configuration that supplies only a retry method, which is the documented way to select
    /// one of the built-in retry providers. No custom type is being requested, so no assembly
    /// resolution is required and no handler may be subscribed - not even transiently.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_WithoutRetryLogicType_DoesNotSubscribeToDefaultLoadContext(string? retryLogicType)
    {
        TestRetryConnectionSection section = CreateSection(retryLogicType);

        SqlConfigurableRetryLogicLoader loader = new(section, null);

        // The built-in factory still resolves the requested method.
        Assert.NotNull(loader.ConnectionProvider);
        AssertNoLoaderResolvingHandlerAttached();
    }

    /// <summary>
    /// A configuration that requests a custom retry logic type legitimately needs assembly
    /// resolution, but the handler must be removed again once type resolution has finished.
    /// </summary>
    [Fact]
    public void Constructor_WithUnresolvableRetryLogicType_DoesNotLeaveHandlerSubscribed()
    {
        TestRetryConnectionSection section =
            CreateSection("Some.Namespace.NoSuchType, Some.Assembly.That.Does.Not.Exist");

        SqlConfigurableRetryLogicLoader loader = new(section, null);

        // Resolution fails and falls back to the built-in factory rather than throwing.
        Assert.NotNull(loader.ConnectionProvider);
        AssertNoLoaderResolvingHandlerAttached();
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
    /// Asserts that no delegate declared by <see cref="SqlConfigurableRetryLogicLoader"/> is
    /// subscribed to the <see cref="AssemblyLoadContext.Default"/> resolving event.
    /// </summary>
    /// <remarks>
    /// The event exposes only add/remove accessors, so its backing field is read reflectively.
    /// If the runtime ever renames that field this assertion fails loudly rather than silently
    /// passing, which is the desired behaviour for a regression test.
    /// </remarks>
    private static void AssertNoLoaderResolvingHandlerAttached()
    {
        FieldInfo? resolvingField = typeof(AssemblyLoadContext).GetField(
            "_resolving",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.True(
            resolvingField is not null,
            "Could not locate the backing field of AssemblyLoadContext.Resolving. This test needs " +
            "updating for the current runtime.");

        Delegate? resolving = (Delegate?)resolvingField!.GetValue(AssemblyLoadContext.Default);

        string[] offenders = resolving is null
            ? []
            : resolving.GetInvocationList()
                .Where(handler => handler.Method.DeclaringType == typeof(SqlConfigurableRetryLogicLoader))
                .Select(handler => handler.Method.Name)
                .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"SqlConfigurableRetryLogicLoader left {offenders.Length} handler(s) subscribed to " +
            $"AssemblyLoadContext.Default.Resolving: {string.Join(", ", offenders)}. A process-wide " +
            "handler changes assembly resolution for the entire application.");
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
