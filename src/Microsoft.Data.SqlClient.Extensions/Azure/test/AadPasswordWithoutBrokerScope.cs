// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Per-test <c>using</c> scope that swaps the registered <see cref="SqlAuthenticationProvider"/>
/// for <see cref="SqlAuthenticationMethod.ActiveDirectoryPassword"/> with a non-broker instance,
/// then restores the original provider on dispose. Use this only in tests that exercise a real
/// MSAL password flow on Windows CI; do NOT promote it to a class fixture, because other tests
/// in the same class may rely on the default (broker-enabled) provider.
///
/// WAM requires an active interactive Windows user session, which non-interactive CI agents
/// cannot satisfy; routing the SQL driver's password flow through WAM in that environment
/// fails with <c>unknown_broker_error</c> (<c>0x80070520 ERROR_NO_SUCH_LOGON_SESSION</c>).
/// See https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/desktop-mobile/wam#integration-best-practices.
/// </summary>
/// <remarks>
/// Consumers MUST live in a test class tagged with <c>[Collection("SqlAuthenticationProvider")]</c>
/// so this swap is serialized against other tests that mutate the global provider registry.
/// </remarks>
internal sealed class AadPasswordWithoutBrokerScope : IDisposable
{
#pragma warning disable CS0618 // Type or member is obsolete (ActiveDirectoryPassword)
    private const SqlAuthenticationMethod Method = SqlAuthenticationMethod.ActiveDirectoryPassword;
#pragma warning restore CS0618

    private readonly SqlAuthenticationProvider? _original;

    public AadPasswordWithoutBrokerScope()
    {
        _original = SqlAuthenticationProvider.GetProvider(Method);
        SqlAuthenticationProvider.SetProvider(
            Method,
            ActiveDirectoryAuthenticationProvider.CreateForTestsWithoutBroker());
    }

    public void Dispose()
    {
        if (_original is not null)
        {
            SqlAuthenticationProvider.SetProvider(Method, _original);
        }
    }
}
