// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;

namespace Microsoft.Data.SqlClient;

/// <summary>Exercises the actual published registry rather than untrimmed capability properties.</summary>
internal static class AuthenticationPublishTest
{
    private static void Main()
    {
#if ENABLE_REFLECTION
        using var listener = new TraceListener();
        if (SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated)
                ?.GetType() != typeof(ConfiguredProvider))
        {
            throw new InvalidOperationException("Published configuration activation failed.");
        }
        if (!ConfiguredInitializer.Invoked)
        {
            throw new InvalidOperationException("Published initializer activation failed.");
        }
        if (SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)
                is not ActiveDirectoryAuthenticationProvider)
        {
            throw new InvalidOperationException("Published Azure discovery failed.");
        }
        Console.WriteLine("PASS: retained configuration and Azure discovery");
#else
        var provider = new ConfiguredProvider();
        if (SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)
                is not null ||
            !SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDefault, provider) ||
            !ReferenceEquals(provider, SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDefault)))
        {
            throw new InvalidOperationException("Published explicit registration failed.");
        }
        Console.WriteLine("PASS: explicit registration without reflective bootstrap");
#endif
    }

#if ENABLE_REFLECTION
    /// <summary>Reports only the fixture's synthetic configuration and discovery diagnostics.</summary>
    private sealed class TraceListener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                EnableEvents(source, EventLevel.Informational, (EventKeywords)2);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs args)
        {
            if (args.Payload?[0] is string message)
            {
                Console.WriteLine(message);
            }
        }
    }
#endif
}

/// <summary>A rootable configuration contract for trimmed-JIT execution.</summary>
public sealed class ConfiguredInitializer : SqlAuthenticationInitializer
{
    internal static bool Invoked;
    public override void Initialize() => Invoked = true;
}

/// <summary>A directly constructed or configuration-activated provider with no token dependencies.</summary>
public class ConfiguredProvider : SqlAuthenticationProvider
{
    public override bool IsSupported(SqlAuthenticationMethod method) => true;
    public override Task<SqlAuthenticationToken> AcquireTokenAsync(
        SqlAuthenticationParameters parameters) => throw new NotSupportedException();
}

/// <summary>The optional Azure contract is intentionally minimal for publish validation.</summary>
public sealed class ActiveDirectoryAuthenticationProvider(
    ActiveDirectoryAuthenticationProviderOptions options) : ConfiguredProvider
{
    public ActiveDirectoryAuthenticationProviderOptions Options { get; } = options;
}

/// <summary>Options activated and set by name during the discovery publish test.</summary>
public sealed class ActiveDirectoryAuthenticationProviderOptions
{
    public string? ApplicationClientId { get; set; }
    public bool UseWamBroker { get; set; }
}
