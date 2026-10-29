// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;
using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient.Internal;

// The section class names are long and confusingly similar, so we alias them for clarity.
using LegacySection = Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection;
using ModernSection = Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>
/// Verifies section selection and loading failures without changing process configuration.
/// </summary>
public class AuthenticationConfigurationLoadingTest
{
    private const string Modern = ModernSection.Name;
    private const string Legacy = LegacySection.Name;
    /// <summary>
    /// Only a lookup that finds neither section reports missing configuration.
    /// </summary>
    [Fact]
    public void MissingSectionTraceRequiresLoadingAbsentSections()
    {
        using var listener = new MissingSectionTraceListener();

        _ = new SqlAuthenticationConfiguration();
        Assert.Equal(0, listener.Count);

        _ = SqlAuthenticationConfiguration.Load(_ => new ModernSection());
        Assert.Equal(0, listener.Count);

        _ = SqlAuthenticationConfiguration.Load(_ => null);
        Assert.Equal(1, listener.Count);
    }

    /// <summary>Counts missing-section traces on the current test thread to isolate concurrent tests.</summary>
    private sealed class MissingSectionTraceListener : EventListener
    {
        private readonly int _threadId = Environment.CurrentManagedThreadId;

        internal int Count { get; private set; }

        internal MissingSectionTraceListener()
        {
            EnableEvents(SqlClientEventSource.Log, EventLevel.Verbose, EventKeywords.All);
        }

        /// <summary>Records only the missing-section diagnostic from this test's synchronous calls.</summary>
        /// <param name="eventData">The emitted event and its payload.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (Environment.CurrentManagedThreadId == _threadId &&
                eventData.Payload?.Contains(
                    "SqlAuthenticationConfiguration | Neither SqlClientAuthenticationProviders " +
                    "nor SqlAuthenticationProviders configuration section found.") == true)
            {
                Count++;
            }
        }
    }

    /// <summary>
    /// The preferred handler is loaded, with legacy read only when modern is absent.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoadSelectsExpectedSectionWithoutActivation(bool modern)
    {
        // Use a provider that throws on construction to keep loading separate from activation.
        ConfigurationSection section = modern
            ? new ModernSection()
            : new LegacySection();
        var providers = Assert.IsType<ProviderSettingsCollection>(
            section.ElementInformation.Properties["providers"].Value);
        providers.Add(new ProviderSettings(
            "Active Directory Integrated", typeof(Provider).AssemblyQualifiedName));
        var reads = new List<string>();

        var configuration = SqlAuthenticationConfiguration.Load(name =>
        {
            reads.Add(name);
            if (name == (modern ? Modern : Legacy))
            {
                return section;
            }
            Assert.False(modern, "A present modern section must prevent legacy reads.");
            return null;
        });

        // Selection must preserve the entry without ever constructing its provider.
        Assert.Equal(modern ? new[] { Modern } : new[] { Modern, Legacy }, reads);
        ProviderSettings provider = Assert.Single(configuration.Providers);
        Assert.Equal("Active Directory Integrated", provider.Name);
        Assert.Equal(typeof(Provider).AssemblyQualifiedName, provider.Type);
    }

    /// <summary>Absent sections produce empty settings without explicit option overrides.</summary>
    [Fact]
    public void LoadConfigurationAllowsBothSectionsToBeAbsent()
    {
        // With neither section present, both names must be consulted.
        var reads = new List<string>();

        var configuration = SqlAuthenticationConfiguration.Load(name =>
        {
            reads.Add(name);
            return null;
        });

        // No section means no overrides, not an explicit false broker setting.
        Assert.Equal(new[] { Modern, Legacy }, reads);
        Assert.Null(configuration.ApplicationClientId);
        Assert.Null(configuration.UseWamBroker);
        Assert.Empty(configuration.InitializerType);
        Assert.Empty(configuration.Providers);
    }

    /// <summary>Mismatched modern handlers are ignored so legacy settings can be read, as on main.</summary>
    [Theory]
    [InlineData(typeof(object))]
    [InlineData(typeof(AppSettingsSection))]
    [InlineData(typeof(LegacySection))]
    public void LoadFallsBackAfterHandlerMismatch(Type type)
    {
        var legacy = new LegacySection();
        legacy.ElementInformation.Properties["applicationClientId"].Value = "legacy-client";
        var reads = new List<string>();

        var configuration = SqlAuthenticationConfiguration.Load(name =>
        {
            reads.Add(name);
            return name == Modern ? Activator.CreateInstance(type) : legacy;
        });

        Assert.Equal(new[] { Modern, Legacy }, reads);
        Assert.Equal("legacy-client", configuration.ApplicationClientId);
    }

    /// <summary>A legacy handler owned by another provider is ignored, as on main.</summary>
    [Fact]
    public void LoadIgnoresMismatchedLegacyHandler()
    {
        var configuration = SqlAuthenticationConfiguration.Load(
            name => name == Modern ? null : new AppSettingsSection());

        Assert.Null(configuration.ApplicationClientId);
        Assert.Null(configuration.UseWamBroker);
        Assert.Empty(configuration.InitializerType);
        Assert.Empty(configuration.Providers);
    }

    /// <summary>All configuration-reading errors yield empty settings without fallback, as on main.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void LoadConfigurationHandlesRetrievalFailure(bool modern, bool fatal)
    {
        // Exercise both reader positions, including a configuration wrapper around a fatal error.
        var reads = new List<string>();
        var expected = new ConfigurationErrorsException("Invalid section",
            fatal ? new OutOfMemoryException("Test fatal failure") : null);

        SqlAuthenticationConfiguration Load() =>
            SqlAuthenticationConfiguration.Load(name =>
            {
                reads.Add(name);
                if (name == (modern ? Modern : Legacy))
                {
                    throw expected;
                }
                return null;
            });

        var configuration = Load();
        Assert.Null(configuration.ApplicationClientId);
        Assert.Null(configuration.UseWamBroker);
        Assert.Empty(configuration.InitializerType);
        Assert.Empty(configuration.Providers);
        Assert.Equal(modern ? new[] { Modern } : new[] { Modern, Legacy }, reads);
    }

    /// <summary>Non-configuration failures propagate unchanged from either reader position, as on main.</summary>
    [Theory]
    [InlineData(true, typeof(InvalidOperationException))]
    [InlineData(false, typeof(InvalidOperationException))]
    [InlineData(true, typeof(OutOfMemoryException))]
    [InlineData(false, typeof(OutOfMemoryException))]
    public void LoadPropagatesNonConfigurationFailure(bool modern, Type exceptionType)
    {
        var expected = (Exception)Activator.CreateInstance(exceptionType)!;
        var reads = new List<string>();

        var actual = Record.Exception(() => SqlAuthenticationConfiguration.Load(name =>
        {
            reads.Add(name);
            if (name == (modern ? Modern : Legacy))
            {
                throw expected;
            }
            return null;
        }));

        Assert.Same(expected, actual);
        Assert.Equal(modern ? new[] { Modern } : new[] { Modern, Legacy }, reads);
    }

    /// <summary>An invalid modern broker value is ignored without selecting legacy settings.</summary>
    [Fact]
    public void LoadConfigurationDoesNotFallBackAfterInvalidBrokerValue()
    {
        // An invalid broker value does not invalidate the selected modern section.
        var section = new ModernSection();
        section.ElementInformation.Properties["useWamBroker"].Value = "invalid";
        var reads = new List<string>();

        var configuration = SqlAuthenticationConfiguration.Load(name =>
            {
                reads.Add(name);
                return name == Modern
                    ? section : new LegacySection();
            });

        // Validation happens after section selection, without reopening the fallback path.
        Assert.Null(configuration.UseWamBroker);
        Assert.Equal(new[] { Modern }, reads);
    }

    /// <summary>
    /// Fetching accepts the expected handler type and preserves the section instance.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FetchConfigurationSectionAcceptsExpectedHandler(bool modern)
    {
        // Keep handler identity checks independent of process-wide ConfigurationManager state.
        ConfigurationSection expected = modern
            ? new ModernSection()
            : new LegacySection();
        string name = modern ? Modern : Legacy;

        var actual = SqlAuthenticationConfiguration.FetchConfigurationSection(
            name, expected.GetType(), requested =>
            {
                Assert.Equal(name, requested);
                return expected;
            });

        // Fetching validates the handler, but must not replace or deserialize the supplied object.
        Assert.Same(expected, actual);
    }

    /// <summary>
    /// Section readers' non-configuration failures are not replaced or suppressed.
    /// </summary>
    [Fact]
    public void FetchConfigurationSectionPropagatesReaderFailure()
    {
        // Non-configuration reader failures must escape just as they do during production loading.
        var expected = new InvalidOperationException("Test section reader failure");

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            SqlAuthenticationConfiguration.FetchConfigurationSection(
                Modern, typeof(ModernSection),
                _ => throw expected)));
    }

    /// <summary>
    /// Fails if configuration loading incorrectly activates a configured provider.
    /// </summary>
    public sealed class Provider : SqlAuthenticationProvider
    {
        public Provider() =>
            throw new InvalidOperationException(
                "Loading configuration must not activate providers.");

        public override bool IsSupported(SqlAuthenticationMethod method) => true;

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }
}
