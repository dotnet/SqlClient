// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

public class DefaultProviderTests
{
    // Verify that our provider has been installed for all AAD/Entra
    // authentication methods, and not for any other methods.
    //
    // Note that this isn't testing anything in the Azure package.  It actually
    // tests the static constructor of SqlAuthenticationProviderManager in the
    // MDS package.
    [Fact]
    public void OurProviderInstalled()
    {
        foreach (var method in
            #if NET
            Enum.GetValues<SqlAuthenticationMethod>()
            #else
            Enum.GetValues(typeof(SqlAuthenticationMethod)).Cast<SqlAuthenticationMethod>()
            #endif
        )
        {
            SqlAuthenticationProvider? provider =
                SqlAuthenticationProviderManager.GetProvider(method);

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
