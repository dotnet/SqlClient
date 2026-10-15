// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Resources;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Guards embedded authentication resources, English fallback, and exception metadata.
/// </summary>
public class AuthenticationResourceTests
{
    /// <summary>
    /// Unsupported authentication is rejected before network access with the complete message.
    /// </summary>
    [Fact]
    public async Task AcquireTokenAsync_UnsupportedMethod_PreservesMessage()
    {
        SqlAuthenticationParameters parameters = new(
            authenticationMethod: SqlAuthenticationMethod.SqlPassword,
            serverName: "test-server",
            databaseName: "test-database",
            resource: "https://database.windows.net/",
            authority: "https://login.microsoftonline.com/common",
            userId: "<user>",
            password: "<pwd>",
            connectionId: Guid.NewGuid(),
            connectionTimeout: 30);
        ActiveDirectoryAuthenticationProvider provider = new();

        AuthenticationException exception = await Assert.ThrowsAsync<AuthenticationException>(
            () => provider.AcquireTokenAsync(parameters));

        ResourceManager resources = new(
            "Microsoft.Data.SqlClient.Resources.Strings",
            typeof(ActiveDirectoryAuthenticationProvider).Assembly);
        string? failureMessage = resources.GetString("AuthenticationFailed");
        string? methodMessage = resources.GetString("UnsupportedAuthenticationMethod");
        Assert.NotNull(failureMessage);
        Assert.NotNull(methodMessage);
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, failureMessage!,
                parameters.AuthenticationMethod,
                string.Format(CultureInfo.CurrentCulture, methodMessage!, parameters.AuthenticationMethod)),
            exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// Every provider-owned message must be available from the Azure assembly's resources.
    /// </summary>
    [Theory]
    [InlineData("AuthenticationFailed", "Failed to acquire access token for {0}: {1}")]
    [InlineData("InvalidAuthority", "The authority '{0}' is not a valid Entra ID authority. Expected an absolute HTTPS URL containing a tenant, e.g. 'https://login.microsoftonline.com/<tenant>'.")]
    [InlineData("UnsupportedAuthenticationMethod", "Authentication method {0} not supported.")]
    [InlineData("NullAuthenticationResult", "Internal error - authentication result is null")]
    [InlineData("AzureIdentityError", "Azure.Identity error: {0}")]
    [InlineData("UnexpectedError", "Unexpected error: {0}")]
    [InlineData("InvalidParentWindow", "{0} expects the callback to return an IntPtr window handle; got {1}.")]
    [InlineData("InvalidParentWindowNetFramework", "{0} expects the callback to return an IntPtr window handle (or an IWin32Window on .NET Framework); got {1}.")]
    public void Resource_PreservesEnglishMessage(string key, string expected)
    {
        ResourceManager resources = new(
            "Microsoft.Data.SqlClient.Resources.Strings",
            typeof(ActiveDirectoryAuthenticationProvider).Assembly);

        Assert.Equal(expected, resources.GetString(key, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Both exception constructors use resources without changing retry or cause metadata;
    /// message lookup must follow the UI culture.
    /// </summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void AuthenticationException_PreservesMessageAndMetadata(string cultureName)
    {
        CultureInfo previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            const SqlAuthenticationMethod Method = SqlAuthenticationMethod.ActiveDirectoryDefault;
            const string Message = "test failure";
            ResourceManager resources = new(
                "Microsoft.Data.SqlClient.Resources.Strings",
                typeof(ActiveDirectoryAuthenticationProvider).Assembly);
            string? template = resources.GetString("AuthenticationFailed");
            Assert.NotNull(template);
            string expected = string.Format(CultureInfo.CurrentCulture, template!, Method, Message);
            InvalidOperationException cause = new(Message);

            AuthenticationException simple = new(Method, Message);
            AuthenticationException detailed = new(Method, "test-code", true, 1234, Message, cause);

            Assert.Equal(expected, simple.Message);
            Assert.Equal(SqlAuthenticationMethod.NotSpecified, simple.Method);
            Assert.Equal("Unknown", simple.FailureCode);
            Assert.False(simple.ShouldRetry);
            Assert.Equal(0, simple.RetryPeriod);
            Assert.Null(simple.InnerException);
            Assert.Equal(expected, detailed.Message);
            Assert.Equal(Method, detailed.Method);
            Assert.Equal("test-code", detailed.FailureCode);
            Assert.True(detailed.ShouldRetry);
            Assert.Equal(1234, detailed.RetryPeriod);
            Assert.Same(cause, detailed.InnerException);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
