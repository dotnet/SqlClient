// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.SqlClient.OnlineTests.Utilities;

[AttributeUsage(AttributeTargets.Method)]
[XunitTestCaseDiscoverer(
    "Microsoft.Data.SqlClient.OnlineTests.Utilities.OnlineTestDiscoverer",
    "Microsoft.Data.SqlClient.OnlineTests")]
public class OnlineTestAttribute : TheoryAttribute
{
    public OnlineTestAttribute(
        ConnectionTraits[]? requiredTraits = null,
        ConnectionTraits[]? blockedTraits = null)
    {
        BlockedTraits = blockedTraits ?? [];
        RequiredTraits = requiredTraits ?? [];
    }

    public ConnectionTraits[] BlockedTraits { get; }

    public ConnectionTraits[] RequiredTraits { get; }
}
