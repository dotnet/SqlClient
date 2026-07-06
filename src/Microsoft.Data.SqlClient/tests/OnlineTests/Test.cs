// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.OnlineTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.OnlineTests;

public class Test
{
    [Fact]
    public void TestyTest()
    {
        Console.WriteLine("Yo.");
    }

    [OnlineTest]
    public void TestyTestRunOnAll(string connectionString)
    {
        Console.WriteLine("This should run everywhere.");
    }

    [OnlineTest(requiredTraits:[ConnectionTraits.SupportsVector])]
    public void TestyTestRequireVector(string connectionString)
    {
        Console.WriteLine("This should be skipped.");
    }

    [OnlineTest(blockedTraits: [ConnectionTraits.SupportsVector])]
    public void TestyTestBlockOnVector(string connectionString)
    {
        Console.WriteLine("This should run everywhere.");
    }
}
