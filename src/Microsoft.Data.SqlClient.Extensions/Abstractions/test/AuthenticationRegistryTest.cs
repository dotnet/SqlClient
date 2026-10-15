// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Validates errors and callback compatibility of the direct provider registry.</summary>
public class AuthenticationRegistryTest
{
    /// <summary>Unsupported registrations preserve the false result and explain how to fix the provider.</summary>
    [Fact]
    public void UnsupportedProviderReturnsFalseAndLogs()
    {
        using var listener = new AuthenticationTraceListener();
        Assert.False(SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.NotSpecified, new Provider(false)));
        Assert.Contains(listener.Messages, message =>
            message.Contains("SetProvider") && message.Contains("NotSpecified") &&
            message.Contains("returning false") && message.Contains("supports"));
    }

    /// <summary>Null registrations preserve the false result and explain the required argument.</summary>
    [Fact]
    public void NullProviderReturnsFalseAndLogs()
    {
        using var listener = new AuthenticationTraceListener();
        Assert.False(SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.NotSpecified, null!));
        Assert.Contains(listener.Messages, message =>
            message.Contains("SetProvider") && message.Contains("returning false") &&
            message.Contains("non-null"));
    }

    /// <summary>Provider callback failures are logged without replacing the existing registration.</summary>
    [Theory]
    [InlineData("IsSupported")]
    [InlineData("BeforeLoad")]
    [InlineData("BeforeUnload")]
    public void CallbackFailureReturnsFalseAndPreservesProvider(string callback)
    {
        using var listener = new AuthenticationTraceListener();
        var first = new ThrowingProvider(callback == "BeforeUnload" ? callback : null);
        var replacement = new ThrowingProvider(callback == "BeforeUnload" ? null : callback);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, first));

        Assert.False(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, replacement));

        Assert.Same(first, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.NotSpecified));
        Assert.Contains(listener.Messages, message =>
            message.Contains("SetProvider") && message.Contains(callback) &&
            message.Contains("returning false") && message.Contains("callbacks"));
        first.Throw = false;
        var valid = new Provider(true);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, valid));
        Assert.Same(valid, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.NotSpecified));
    }

    /// <summary>The registry preserves initial insertion and replacement callback behavior.</summary>
    [Fact]
    public void ReplacementInvokesCallbacks()
    {
        var first = new Provider(true);
        var second = new Provider(true);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, first));
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, second));
        Assert.Equal(1, first.Unloads);
        Assert.Equal(1, second.Loads);
        Assert.Same(second, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.SqlPassword));
    }

    /// <summary>Fatal failures in later aggregate entries propagate from every registration callback without replacing the provider.</summary>
    [Theory]
    [InlineData("IsSupported")]
    [InlineData("BeforeLoad")]
    [InlineData("BeforeUnload")]
    public void AggregateCallbackFailurePropagatesAndPreservesProvider(string callback)
    {
        var failure = new AggregateException(
            new InvalidOperationException("Ordinary failure"), new OutOfMemoryException("Test fatal failure"));
        var first = new ThrowingProvider(callback == "BeforeUnload" ? callback : null, failure);
        var replacement = new ThrowingProvider(callback == "BeforeUnload" ? null : callback, failure);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, first));

        try
        {
            Assert.Same(failure, Assert.Throws<AggregateException>(() =>
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, replacement)));
            Assert.Same(first, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.NotSpecified));
        }
        finally
        {
            first.Throw = false;
            Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, new Provider(true)));
        }
    }

    /// <summary>Application-provider support checks can wait for another thread to look up the existing provider.</summary>
    [Fact]
    public void SupportValidationAllowsCrossThreadLookup()
    {
        var first = new Provider(true);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, first));
        var replacement = new LookupProvider();

        bool registered = SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, replacement);

        Assert.NotNull(replacement.Lookup);
        Assert.True(replacement.Lookup.Wait(TimeSpan.FromSeconds(10)), "Cross-thread lookup did not complete.");
        Assert.True(registered, "Support validation blocked a cross-thread registry lookup.");
        Assert.Same(first, replacement.Lookup.Result);
        Assert.Equal(1, replacement.SupportChecks);
        Assert.Same(replacement, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.SqlPassword));
    }

    private sealed class Provider(bool supported) : SqlAuthenticationProvider
    {
        internal int Loads;
        internal int Unloads;
        public override bool IsSupported(SqlAuthenticationMethod method) => supported;
        public override void BeforeLoad(SqlAuthenticationMethod method) => Loads++;
        public override void BeforeUnload(SqlAuthenticationMethod method) => Unloads++;
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingProvider(string? callback, Exception? failure = null) : SqlAuthenticationProvider
    {
        internal bool Throw { get; set; } = true;

        public override bool IsSupported(SqlAuthenticationMethod method)
        {
            ThrowIfRequested(nameof(IsSupported));
            return true;
        }

        public override void BeforeLoad(SqlAuthenticationMethod method) =>
            ThrowIfRequested(nameof(BeforeLoad));

        public override void BeforeUnload(SqlAuthenticationMethod method) =>
            ThrowIfRequested(nameof(BeforeUnload));

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
            throw new NotSupportedException();

        private void ThrowIfRequested(string name)
        {
            if (Throw && callback == name)
            {
                throw failure ?? new InvalidOperationException("Test callback failure: " + name);
            }
        }
    }

    private sealed class LookupProvider : SqlAuthenticationProvider
    {
        internal Task<SqlAuthenticationProvider?>? Lookup { get; private set; }
        internal int SupportChecks { get; private set; }

        public override bool IsSupported(SqlAuthenticationMethod method)
        {
            SupportChecks++;
            Lookup = Task.Run(() => SqlAuthenticationProvider.GetProvider(method));
            return Lookup.Wait(TimeSpan.FromSeconds(10));
        }

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
            throw new NotSupportedException();
    }

    private sealed class AuthenticationTraceListener : EventListener
    {
        internal ConcurrentQueue<string> Messages { get; } = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)2);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventId == 3 && eventData.Payload?[0] is string message)
            {
                Messages.Enqueue(message);
            }
        }
    }
}
