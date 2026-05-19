// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

public class WamBrokerTests
{
    [Fact]
    public void SetParentActivityOrWindow_NullArgument_ThrowsArgumentNullException()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        Assert.Throws<ArgumentNullException>("parentActivityOrWindowFunc",
            () => provider.SetParentActivityOrWindow(null!));
    }

    [Fact]
    public void SetParentActivityOrWindow_ValidFunc_DoesNotThrow()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindow(() => IntPtr.Zero);
    }

    [Fact]
    public void SetParentActivityOrWindow_CanBeCalledMultipleTimes()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindow(() => IntPtr.Zero);
        provider.SetParentActivityOrWindow(() => new IntPtr(12345));
    }
}
