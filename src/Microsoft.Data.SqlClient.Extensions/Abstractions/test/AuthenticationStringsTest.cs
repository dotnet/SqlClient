// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Configuration;
using System.Diagnostics.Tracing;
using System.Globalization;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies authentication exception factories, resource lookup and safe formatting.</summary>
public class AuthenticationStringsTest
{
    /// <summary>
    /// Exception factories preserve their types and causes and forward arguments to the formatter
    /// without depending on resource availability or translated wording.
    /// </summary>
    [Theory]
    [InlineData("SQL_InvalidAuthenticationProviderCollection",
        typeof(ConfigurationErrorsException))]
    [InlineData("SQL_InvalidAuthenticationBrokerValue",
        typeof(ConfigurationErrorsException), "invalid-value")]
    [InlineData("SQL_MissingAuthenticationConfigurationProperty",
        typeof(ConfigurationErrorsException), "property-name")]
    [InlineData("SQL_InvalidAuthenticationConfigurationPropertyType",
        typeof(ConfigurationErrorsException), "property-name")]
    [InlineData("SQL_InvalidAuthenticationConfigurationHandler",
        typeof(ConfigurationErrorsException), "section-name", "expected-type", "actual-type")]
    [InlineData("SQL_InvalidAuthenticationInitializerInstance",
        typeof(InvalidOperationException))]
    [InlineData("SQL_InvalidAuthenticationProviderInstance",
        typeof(InvalidOperationException))]
    [InlineData("SQL_CannotRegisterAuthProvider",
        typeof(InvalidOperationException), "authentication", "provider-type")]
    [InlineData("SQL_CannotCreateAuthProvider",
        typeof(ArgumentException), "authentication", "provider-type")]
    [InlineData("SQL_CannotCreateAuthInitializer",
        typeof(ArgumentException), "initializer-type")]
    [InlineData("SQL_UnsupportedAuthenticationByProvider",
        typeof(NotSupportedException), "provider-type", "authentication")]
    [InlineData("SQL_UnsupportedAuthentication", typeof(NotSupportedException), "authentication")]
    [InlineData("SQL_UseWamBrokerRequiresAzureExtensionUpgrade", typeof(InvalidOperationException))]
    public void ExceptionFactoriesPreserveTypeArgumentsAndCause(
        string key, Type expectedType, params string[] args)
    {
        var cause = new InvalidOperationException("Construction failed.");
        Exception error = key switch
        {
            "SQL_InvalidAuthenticationProviderCollection" =>
                AuthenticationStrings.InvalidAuthenticationProviderCollection(),
            "SQL_InvalidAuthenticationBrokerValue" =>
                AuthenticationStrings.InvalidAuthenticationBrokerValue(args[0]),
            "SQL_MissingAuthenticationConfigurationProperty" =>
                AuthenticationStrings.MissingAuthenticationConfigurationProperty(args[0]),
            "SQL_InvalidAuthenticationConfigurationPropertyType" =>
                AuthenticationStrings.InvalidAuthenticationConfigurationPropertyType(args[0]),
            "SQL_InvalidAuthenticationConfigurationHandler" =>
                AuthenticationStrings.InvalidAuthenticationConfigurationHandler(args[0], args[1], args[2]),
            "SQL_InvalidAuthenticationInitializerInstance" =>
                AuthenticationStrings.InvalidAuthenticationInitializerInstance(),
            "SQL_InvalidAuthenticationProviderInstance" =>
                AuthenticationStrings.InvalidAuthenticationProviderInstance(),
            "SQL_CannotRegisterAuthProvider" =>
                AuthenticationStrings.CannotRegisterAuthProvider(args[0], args[1]),
            "SQL_CannotCreateAuthProvider" =>
                AuthenticationStrings.CannotCreateAuthProvider(args[0], args[1], cause),
            "SQL_CannotCreateAuthInitializer" =>
                AuthenticationStrings.CannotCreateSqlAuthInitializer(args[0], cause),
            "SQL_UnsupportedAuthenticationByProvider" =>
                AuthenticationStrings.UnsupportedAuthenticationByProvider(args[1], args[0]),
            "SQL_UnsupportedAuthentication" =>
                AuthenticationStrings.UnsupportedAuthentication(args[0]),
            "SQL_UseWamBrokerRequiresAzureExtensionUpgrade" =>
                AuthenticationStrings.UseWamBrokerRequiresAzureExtensionUpgrade(),
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };

        Assert.Equal(expectedType, error.GetType());
        Assert.Equal(AuthenticationStrings.Format(key, args), error.Message);
        Assert.Same(key is "SQL_CannotCreateAuthProvider" or "SQL_CannotCreateAuthInitializer"
            ? cause : null, error.InnerException);
    }

    /// <summary>
    /// French is the stable satellite used to verify direct lookup, parent-culture lookup and
    /// neutral-English fallback for an untranslated culture.
    /// </summary>
    [Theory]
    // Expect the French translation for both "fr" and "fr-FR" cultures.
    [InlineData("fr", "L'authentification 'unsupported' n'est pas prise en charge.")]
    [InlineData("fr-FR", "L'authentification 'unsupported' n'est pas prise en charge.")]
    // Expect the English fallback for the Swedish culture, which we don't localize.
    [InlineData("sv-SE", "The authentication 'unsupported' is not supported.")]
    public void FormatSelectsResourceUsingUICulture(string cultureName, string expected)
    {
        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

            Assert.Equal(expected,
                AuthenticationStrings.Format("SQL_UnsupportedAuthentication", "unsupported"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    /// <summary>
    /// Resource selection follows the French UI culture while numeric formatting follows the
    /// distinct current culture, with both cultures restored after the assertion.
    /// </summary>
    [Fact]
    public void FormatUsesSeparateUICultureAndFormattingCulture()
    {
        CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            // French uses a comma as the decimal separator, but the formatting culture is "en-US"
            // which uses a period.
            Assert.Equal("L'authentification '1234.5' n'est pas prise en charge.",
                AuthenticationStrings.Format("SQL_UnsupportedAuthentication", 1234.5m));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUICulture;
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>Missing keys produce a diagnostic message with culture-formatted arguments.</summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public void MissingResourceIncludesArgumentsAndLogs(string cultureName)
    {
        using var listener = new AuthenticationTraceListener();
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            decimal value = 1234.5m;
            string message = AuthenticationStrings.Format("MissingAuthenticationResource",
                "provider-type", value, null!);

            Assert.Equal("MissingAuthenticationResource: [provider-type, " +
                value.ToString(CultureInfo.CurrentCulture) + ", <null>]", message);
            Assert.Contains(listener.Messages, trace =>
                trace.Contains("AuthenticationStrings.Format") &&
                trace.Contains("MissingAuthenticationResource"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>Incorrect placeholder counts fall back without replacing the intended exception.</summary>
    [Fact]
    public void FormattingFailurePreservesIntendedException()
    {
        using var listener = new AuthenticationTraceListener();
        var cause = new InvalidOperationException("Provider construction failed.");
        var error = new ArgumentException(
            AuthenticationStrings.Format("SQL_CannotCreateAuthProvider", "authentication"), cause);

        Assert.Equal("SQL_CannotCreateAuthProvider: [authentication]", error.Message);
        Assert.Same(cause, error.InnerException);
        Assert.Contains(listener.Messages, trace => trace.Contains(nameof(FormatException)));
    }

    /// <summary>Lookup failures and missing argument arrays still produce a usable message.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("SQL_UnsupportedAuthentication")]
    public void NullKeyAndArgumentsProduceFallback(string? key)
    {
        Assert.Equal((key ?? "<null>") + ": []", AuthenticationStrings.Format(key!, null!));
    }

    /// <summary>An argument that cannot format must not prevent rendering the remaining values.</summary>
    [Theory]
    [InlineData("MissingAuthenticationResource")]
    [InlineData("SQL_UnsupportedAuthentication")]
    public void ThrowingArgumentDoesNotEscape(string key)
    {
        using var listener = new AuthenticationTraceListener();
        string message = AuthenticationStrings.Format(key, new ThrowingArgument(), "provider-type");

        Assert.Equal(key + ": [<unformattable: " + typeof(ThrowingArgument).FullName +
            ">, provider-type]", message);
        Assert.Contains(listener.Messages, trace =>
            trace.Contains("AuthenticationStrings.Format") &&
            trace.Contains(nameof(InvalidOperationException)));
    }

    /// <summary>Null and string arguments retain their defined representations without resource lookup.</summary>
    [Theory]
    [InlineData(null, "<null>")]
    [InlineData("", "")]
    [InlineData("provider-type", "provider-type")]
    public void FormatArgumentPreservesNullAndStrings(object? argument, string expected)
    {
        Assert.Equal(expected, AuthenticationStrings.FormatArgument(argument));
    }

    /// <summary>Numeric argument formatting follows the current culture rather than an invariant default.</summary>
    [Theory]
    [InlineData("en-US", "1234.5")]
    [InlineData("fr-FR", "1234,5")]
    public void FormatArgumentUsesCurrentCulture(string cultureName, string expected)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.Equal(expected, AuthenticationStrings.FormatArgument(1234.5m));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>Custom formattable arguments receive the current culture and the default format.</summary>
    [Fact]
    public void FormatArgumentUsesIFormattable()
    {
        var argument = new FormattableArgument();

        Assert.Equal("formatted", AuthenticationStrings.FormatArgument(argument));
        Assert.Same(CultureInfo.CurrentCulture, argument.Provider);
        Assert.Null(argument.Format);
    }

    /// <summary>Recoverable argument failures return a type-name placeholder and emit a diagnostic.</summary>
    [Fact]
    public void FormatArgumentFailureReturnsPlaceholderAndLogs()
    {
        using var listener = new AuthenticationTraceListener();

        Assert.Equal("<unformattable: " + typeof(ThrowingArgument).FullName + ">",
            AuthenticationStrings.FormatArgument(new ThrowingArgument()));
        Assert.Contains(listener.Messages, trace =>
            trace.Contains("Argument of type") &&
            trace.Contains(typeof(ThrowingArgument).FullName!) &&
            trace.Contains(nameof(InvalidOperationException)));
    }

    /// <summary>Fatal argument failures propagate unchanged instead of being hidden by a placeholder.</summary>
    [Fact]
    public void FormatArgumentFatalFailurePropagates()
    {
        var cause = new OutOfMemoryException("Simulated formatting failure.");

        Assert.Same(cause, Assert.Throws<OutOfMemoryException>(() =>
            AuthenticationStrings.FormatArgument(new ThrowingArgument(cause))));
    }

    private sealed class ThrowingArgument(Exception? exception = null)
    {
        /// <summary>Simulates a failure while producing an argument's textual value.</summary>
        public override string ToString() =>
            throw exception ?? new InvalidOperationException("Cannot format argument.");
    }

    private sealed class FormattableArgument : IFormattable
    {
        internal string? Format { get; private set; }
        internal IFormatProvider? Provider { get; private set; }

        /// <summary>Records the requested format and culture to verify composite formatting.</summary>
        /// <param name="format">The requested format string.</param>
        /// <param name="formatProvider">The culture used for formatting.</param>
        /// <returns>A recognizable formatted value.</returns>
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            Format = format;
            Provider = formatProvider;
            return "formatted";
        }

        /// <summary>Rejects ordinary formatting so tests require the IFormattable path.</summary>
        public override string ToString() => throw new InvalidOperationException("Use IFormattable.");
    }

    /// <summary>Captures message-generation diagnostics without altering the registry.</summary>
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
