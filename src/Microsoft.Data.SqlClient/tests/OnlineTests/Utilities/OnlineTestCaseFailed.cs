// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Data.SqlClient.OnlineTests.Utilities;

public sealed class OnlineTestFailedTestCase : ExecutionErrorTestCase
{
    #pragma warning disable CS0618 // Required by xUnit for test case deserialization.
    public OnlineTestFailedTestCase()
    {
    }
    #pragma warning restore CS0618

    public OnlineTestFailedTestCase(
        IMessageSink diagnosticMessageSink,
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        string errorMessage)
        : base(
            diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod,
            errorMessage)
    {
    }
}
