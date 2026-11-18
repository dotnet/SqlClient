// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.Data.SqlClient.NativeSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Guards against eager Windows native SNI initialization on managed-SNI platforms.
/// </summary>
public class NativeSniInitializationTests
{
    /// <summary>
    /// Requires precise type initialization so AOT runtimes cannot load Windows native SNI
    /// merely because the managed networking factory references its singleton.
    /// </summary>
    [Fact]
    public void SNILoadHandle_DoesNotAllowEarlyStaticInitialization()
    {
        Assert.Equal(
            (TypeAttributes)0,
            typeof(SNILoadHandle).Attributes & TypeAttributes.BeforeFieldInit);
    }
}
