// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Dummy authentication provider registered via app.config for .NET Framework
/// unit tests. Returns a dummy token and only supports ActiveDirectoryInteractive.
/// </summary>
public class DummySqlAuthenticationProvider : SqlAuthenticationProvider
{
    public const string DummyAccessToken = "dummy_access_token";

    public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        => Task.FromResult(new SqlAuthenticationToken(DummyAccessToken, new DateTimeOffset(DateTime.Now.AddHours(2))));

    public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
        => authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive;
}
