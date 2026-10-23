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
        // A missing modern section is the only reason to consult legacy configuration.
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

    /// <summary>
    /// An incompatible modern handler is an error rather than a reason to try legacy.
    /// </summary>
    [Theory]
    [InlineData(typeof(object))]
    [InlineData(typeof(AppSettingsSection))]
    [InlineData(typeof(LegacySection))]
    public void LoadConfigurationDoesNotFallBackAfterHandlerMismatch(Type type)
    {
        // Return a present but incompatible modern handler; legacy must not mask the mistake.
        var reads = new List<string>();
        object section = Activator.CreateInstance(type)!;

        var failure = Assert.Throws<ConfigurationErrorsException>(() =>
            SqlAuthenticationConfiguration.Load(name =>
            {
                // Record the section name being read, which should be modern in this test
                // because that is the first choice.
                reads.Add(name);

                // Return a section instance whose type isn't the modern one,
                // simulating an incompatible handler.
                return section;
            }));

        // Report the incompatible handler and stop before the fallback reader is called.
        Assert.Equal(new[] { Modern }, reads);
        Assert.Contains(type.FullName!, failure.Message);
        Assert.Contains(typeof(ModernSection).FullName!,
            failure.Message);
    }

    /// <summary>Retrieval failures escape unchanged and no later section is read.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void LoadConfigurationPropagatesRetrievalFailure(bool modern, bool fatal)
    {
        // Exercise both reader positions, including a configuration wrapper around a fatal error.
        var reads = new List<string>();
        var expected = new ConfigurationErrorsException("Invalid section",
            fatal ? new OutOfMemoryException("Test fatal failure") : null);

        var actual = Assert.Throws<ConfigurationErrorsException>(() =>
            SqlAuthenticationConfiguration.Load(name =>
            {
                reads.Add(name);
                if (name == (modern ? Modern : Legacy))
                {
                    throw expected;
                }
                return null;
            }));

        // Loading must preserve the original exception and never read beyond the failed section.
        Assert.Same(expected, actual);
        Assert.Equal(modern ? new[] { Modern } : new[] { Modern, Legacy }, reads);
    }

    /// <summary>A malformed modern section must not be hidden by a valid legacy section.</summary>
    [Fact]
    public void LoadConfigurationDoesNotFallBackAfterValidationFailure()
    {
        // An invalid modern value must fail even if a valid legacy handler is available.
        var section = new ModernSection();
        section.ElementInformation.Properties["useWamBroker"].Value = "invalid";
        var reads = new List<string>();

        var failure = Assert.Throws<ConfigurationErrorsException>(() =>
            SqlAuthenticationConfiguration.Load(name =>
            {
                reads.Add(name);
                return name == Modern
                    ? section : new LegacySection();
            }));

        // Validation happens after section selection, without reopening the fallback path.
        Assert.Contains("useWamBroker", failure.Message);
        Assert.Equal(new[] { Modern }, reads);
    }

    /// <summary>
    /// Fetching accepts the expected full handler name and preserves the section instance.
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
            name, expected.GetType().FullName!, requested =>
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
                Modern, typeof(ModernSection).FullName!,
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
