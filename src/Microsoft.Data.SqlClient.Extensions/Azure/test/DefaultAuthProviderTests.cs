// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/39233):
// Enable this file once the MDS Azure files have been removed.
#if false

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

public class DefaultAuthProviderTests
{
    // Verify that our auth provider has been installed for all AAD/Entra
    // authentication methods, and not for any other methods.
    //
    // Note that this isn't testing anything in the Azure package.  It actually
    // tests the static constructor of the SqlAuthenticationProviderManager
    // class in the MDS package and the static GetProvider() and SetProvider()
    // methods of the SqlAuthenticationProvider class in the Abstractions
    // package.  We're testing this here because this test project uses both of
    // those packages, and this is a convenient place to put such a test.
    //
    // TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/41888):
    // Move this test to a more appropriate location once we have one.
    //
    [Fact]
    public void AuthProviderInstalled()
    {
        // Iterate over all authentication methods rather than specifying them
        // via Theory data so that we detect any new methods that don't meet
        // our expectations.
        #if NET
        var methods = Enum.GetValues<SqlAuthenticationMethod>()
        #else
        var methods = Enum.GetValues(typeof(SqlAuthenticationMethod)).Cast<SqlAuthenticationMethod>()
        #endif

        foreach (var method in methods)
        {
            SqlAuthenticationProvider? provider =
                SqlAuthenticationProvider.GetProvider(method);

            switch (method)
            {
                #pragma warning disable 0618 // Type or member is obsolete
                case SqlAuthenticationMethod.ActiveDirectoryPassword:
                #pragma warning restore 0618 // Type or member is obsolete
                case SqlAuthenticationMethod.ActiveDirectoryIntegrated:
                case SqlAuthenticationMethod.ActiveDirectoryInteractive:
                case SqlAuthenticationMethod.ActiveDirectoryServicePrincipal:
                case SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow:
                case SqlAuthenticationMethod.ActiveDirectoryManagedIdentity:
                case SqlAuthenticationMethod.ActiveDirectoryMSI:
                case SqlAuthenticationMethod.ActiveDirectoryDefault:
                case SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity:
                    Assert.NotNull(provider);
                    Assert.IsType<ActiveDirectoryAuthenticationProvider>(provider);
                    break;

                default:
                    // There is either no provider installed, or it is not ours.
                    if (provider is not null)
                    {
                        Assert.IsNotType<ActiveDirectoryAuthenticationProvider>(provider);
                    }
                    break;
            }
        }
    }
}

#endif
