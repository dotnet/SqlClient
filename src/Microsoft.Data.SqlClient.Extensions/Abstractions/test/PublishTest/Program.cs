// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;

if (SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is not null)
{
    throw new InvalidOperationException("Trimmed/AOT discovery must not install an Azure provider.");
}
var provider = new TestProvider();
if (!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, provider) ||
    !ReferenceEquals(provider,
        SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)))
{
    throw new InvalidOperationException("Explicit provider registration did not round-trip.");
}
if (AppDomain.CurrentDomain.GetAssemblies().Any(a =>
    a.GetName().Name == "Microsoft.Data.SqlClient.Extensions.Azure"))
{
    throw new InvalidOperationException("Azure discovery loaded the optional extension.");
}
Console.WriteLine("PASS: discovery absent; explicit registration succeeds.");

internal sealed class TestProvider : SqlAuthenticationProvider
{
    public override bool IsSupported(SqlAuthenticationMethod method) => true;
    public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
        throw new NotSupportedException();
}
