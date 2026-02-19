// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/39072):
// TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/39073):
// This file has intentionally not been tidied up or modernized.  Its content will be absorbed into
// new unit and/or integration tests in the future.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

// These tests were moved from MDS FunctionalTests AADAuthenticationTests.cs.
public class AADAuthenticationTests
{
    [Fact]
    public void CustomActiveDirectoryProviderTest()
    {
        SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(static (result) => Task.CompletedTask);
        SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
        Assert.Same(authProvider, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
    }

    [Fact]
    public void CustomActiveDirectoryProviderTest_AppClientId()
    {
        SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(Guid.NewGuid().ToString());
        SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
        Assert.Same(authProvider, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
    }

    [Fact]
    public void CustomActiveDirectoryProviderTest_AppClientId_DeviceFlowCallback()
    {
        SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(static (result) => Task.CompletedTask, Guid.NewGuid().ToString());
        SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
        Assert.Same(authProvider, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
    }
}
