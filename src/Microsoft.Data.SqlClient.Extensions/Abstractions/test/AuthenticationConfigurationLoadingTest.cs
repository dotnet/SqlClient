// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;
using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>
/// Verifies section selection and loading failures without changing process configuration.
/// </summary>
public class AuthenticationConfigurationLoadingTest
{
    private const string Modern = SqlClientAuthenticationProviderConfigurationSection.Name;
    private const string Legacy = SqlAuthenticationProviderConfigurationSection.Name;

    /// <summary>
    /// Only a lookup that finds neither section reports missing configuration.
    /// </summary>
    [Fact]
    public void MissingSectionTraceRequiresLoadingAbsentSections()
    {
        using var listener = new MissingSectionTraceListener();

        _ = new SqlAuthenticationConfiguration();
        Assert.Equal(0, listener.Count);

        _ = SqlAuthenticationConfiguration.Load(_ => new SqlClientAuthenticationProviderConfigurationSection());
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
    /// The preferred handler is applied, with legacy read only when modern is absent.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoadConfigurationAppliesSelectedSection(bool modern)
    {
        var registry = new SqlAuthenticationProviderRegistry();
        SqlAuthenticationProviderConfigurationSection section = modern
            ? new SqlClientAuthenticationProviderConfigurationSection()
            : new SqlAuthenticationProviderConfigurationSection();
        section.Providers.Add(new ProviderSettings(
            "Active Directory Integrated", typeof(Provider).AssemblyQualifiedName));
        var reads = new List<string>();

        registry.LoadConfiguration(name =>
        {
            reads.Add(name);
            if (name == (modern ? Modern : Legacy))
            {
                return section;
            }
            Assert.False(modern, "A present modern section must prevent legacy reads.");
            return null;
        });

        Assert.Equal(modern ? new[] { Modern } : new[] { Modern, Legacy }, reads);
        Assert.False(registry.Register(SqlAuthenticationMethod.ActiveDirectoryIntegrated,
            new Provider()));
    }

    /// <summary>Absent sections are optional and do not prevent application registration.</summary>
    [Fact]
    public void LoadConfigurationAllowsBothSectionsToBeAbsent()
    {
        var registry = new SqlAuthenticationProviderRegistry();
        var reads = new List<string>();

        registry.LoadConfiguration(name =>
        {
            reads.Add(name);
            return null;
        });

        Assert.Equal(new[] { Modern, Legacy }, reads);
        Assert.True(registry.Register(SqlAuthenticationMethod.ActiveDirectoryIntegrated,
            new Provider()));
    }

    /// <summary>
    /// An incompatible modern handler is an error rather than a reason to try legacy.
    /// </summary>
    [Theory]
    [InlineData(typeof(object))]
    [InlineData(typeof(AppSettingsSection))]
    [InlineData(typeof(SqlAuthenticationProviderConfigurationSection))]
    public void LoadConfigurationDoesNotFallBackAfterHandlerMismatch(Type type)
    {
        var reads = new List<string>();
        object section = Activator.CreateInstance(type)!;

        var failure = Assert.Throws<ConfigurationErrorsException>(() =>
            new SqlAuthenticationProviderRegistry().LoadConfiguration(name =>
            {
                reads.Add(name);
                return section;
            }));

        Assert.Equal(new[] { Modern }, reads);
        Assert.Contains(type.FullName!, failure.Message);
        Assert.Contains(typeof(SqlClientAuthenticationProviderConfigurationSection).FullName!,
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
        var reads = new List<string>();
        var expected = new ConfigurationErrorsException("Invalid section",
            fatal ? new OutOfMemoryException("Test fatal failure") : null);

        var actual = Assert.Throws<ConfigurationErrorsException>(() =>
            new SqlAuthenticationProviderRegistry().LoadConfiguration(name =>
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

    /// <summary>A malformed modern section must not be hidden by a valid legacy section.</summary>
    [Fact]
    public void LoadConfigurationDoesNotFallBackAfterApplicationFailure()
    {
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["useWamBroker"].Value = "invalid";
        var reads = new List<string>();

        var failure = Assert.Throws<ConfigurationErrorsException>(() =>
            new SqlAuthenticationProviderRegistry().LoadConfiguration(name =>
            {
                reads.Add(name);
                return name == Modern
                    ? section : new SqlAuthenticationProviderConfigurationSection();
            }));

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
        ConfigurationSection expected = modern
            ? new SqlClientAuthenticationProviderConfigurationSection()
            : new SqlAuthenticationProviderConfigurationSection();
        string name = modern ? Modern : Legacy;

        var actual = SqlAuthenticationProviderRegistry.FetchConfigurationSection(
            name, expected.GetType().FullName!, requested =>
            {
                Assert.Equal(name, requested);
                return expected;
            });

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// Section readers' non-configuration failures are not replaced or suppressed.
    /// </summary>
    [Fact]
    public void FetchConfigurationSectionPropagatesReaderFailure()
    {
        var expected = new InvalidOperationException("Test section reader failure");

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() =>
            SqlAuthenticationProviderRegistry.FetchConfigurationSection(
                Modern, typeof(SqlClientAuthenticationProviderConfigurationSection).FullName!,
                _ => throw expected)));
    }

    /// <summary>
    /// Supplies a constructible configured provider without a SqlClient dependency.
    /// </summary>
    public sealed class Provider : SqlAuthenticationProvider
    {
        public override bool IsSupported(SqlAuthenticationMethod method) => true;

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }
}
