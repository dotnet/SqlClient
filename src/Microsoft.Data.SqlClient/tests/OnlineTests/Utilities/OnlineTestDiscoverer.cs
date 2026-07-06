// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Data.SqlClient.OnlineTests.Utilities;

public class OnlineTestDiscoverer : TheoryDiscoverer
{
    public OnlineTestDiscoverer(IMessageSink diagnosticMessageSink)
        : base(diagnosticMessageSink)
    {
    }

    protected override IEnumerable<IXunitTestCase> CreateTestCasesForTheory(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo theoryAttribute)
    {
        return base.CreateTestCasesForTheory(discoveryOptions, testMethod, theoryAttribute);
    }
}
