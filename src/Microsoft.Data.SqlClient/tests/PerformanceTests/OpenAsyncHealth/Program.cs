// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Data.SqlClient.PerformanceTests;

if (args.Length != 3 || args[0] != "--openasync-health")
{
    Console.Error.WriteLine("Usage: OpenAsyncHealth --openasync-health <case.json> <result.json>");
    return 2;
}

try
{
    return OpenAsyncHealthExperiment.Run(args[1], args[2]);
}
catch (Exception error)
{
    // Configuration/SQL errors can contain credentials. Persist only their type.
    Console.Error.WriteLine($"OpenAsync health harness failed: {error.GetType().FullName}");
    return 2;
}
