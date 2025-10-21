// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SqlAuthenticationMethodTest
{
    // Verify the number of expected enum members.
    [Fact]
    public void Member_Count()
    {
#if NET
        Assert.Equal(11, Enum.GetNames<SqlAuthenticationMethod>().Length);
#else
        Assert.Equal(11, Enum.GetNames(typeof(SqlAuthenticationMethod)).Length);
#endif
    }

    // Verify each of the enum member numeric values.
    [Fact]
    public void Member_Values()
    {
        Assert.Equal(0, (int)SqlAuthenticationMethod.NotSpecified);
        Assert.Equal(1, (int)SqlAuthenticationMethod.SqlPassword);
        #pragma warning disable 0618 // Type or member is obsolete
        Assert.Equal(2, (int)SqlAuthenticationMethod.ActiveDirectoryPassword);
        #pragma warning restore 0618 // Type or member is obsolete
        Assert.Equal(3, (int)SqlAuthenticationMethod.ActiveDirectoryIntegrated);
        Assert.Equal(4, (int)SqlAuthenticationMethod.ActiveDirectoryInteractive);
        Assert.Equal(5, (int)SqlAuthenticationMethod.ActiveDirectoryServicePrincipal);
        Assert.Equal(6, (int)SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow);
        Assert.Equal(7, (int)SqlAuthenticationMethod.ActiveDirectoryManagedIdentity);
        Assert.Equal(8, (int)SqlAuthenticationMethod.ActiveDirectoryMSI);
        Assert.Equal(9, (int)SqlAuthenticationMethod.ActiveDirectoryDefault);
        Assert.Equal(10, (int)SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity);
    }
}
