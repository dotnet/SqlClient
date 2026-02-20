// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.SqlServer.TDS.Servers;

/// <summary>
/// An xunit test fixture that manages the lifecycle of a TdsServer.
/// </summary>
public class TdsServerFixture : IDisposable
{
    public TdsServerFixture()
    {
        TdsServer = new TdsServer();
        TdsServer.Start();
    }

    public void Dispose()
    {
        TdsServer.Dispose();
    }

    public TdsServer TdsServer { get; private set; }
}
