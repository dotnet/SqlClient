// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Data.SqlClient.OnlineTests.Utilities;

public class OnlineTestDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink _diagnosticMessageSink;

    public OnlineTestDiscoverer(IMessageSink diagnosticMessageSink)
    {
        _diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        // Make sure the test method supports a connection string being passed to it
        if (testMethod.Method.GetParameters().FirstOrDefault()?.ParameterType.ToRuntimeType() != typeof(string))
        {
            return
            [
                new OnlineTestFailedTestCase(
                    _diagnosticMessageSink,
                    discoveryOptions,
                    testMethod,
                    "Test method does not have connection string as first parameter.")
            ];
        }

        // Extract the required and blocked traits from the theory attribute
        ConnectionTraits[] requiredTraits =
            factAttribute.GetNamedArgument<ConnectionTraits[]>(nameof(OnlineTestAttribute.RequiredTraits)) ?? [];
        ConnectionTraits[] blockedTraits =
            factAttribute.GetNamedArgument<ConnectionTraits[]>(nameof(OnlineTestAttribute.BlockedTraits)) ?? [];

        try
        {
            // Filter connections
            // NOTE: This will initialize the configuration manager, so we want to capture any
            //    exceptions here to fail the test.
            List<IXunitTestCase> testCases = new List<IXunitTestCase>();
            foreach (ConnectionMetadata connection in ConfigurationManager.Instance.Configuration.Connections)
            {
                // Skip processing traits if there are no trait requirements
                if (requiredTraits.Length > 0 || blockedTraits.Length > 0)
                {
                    ConnectionTraits[] missingRequired = GetMissingRequiredTraits(connection, requiredTraits);
                    ConnectionTraits[] presentBlocked = GetPresentBlockedTraits(connection, blockedTraits);

                    if (missingRequired.Length > 0 || presentBlocked.Length > 0)
                    {
                        // Connection is missing a required trait or has a trait that is blocked.
                        // We will skip it.
                        // @TODO: Only return a skipped test if config setting allows it.
                        IXunitTestCase skipCase = new OnlineTestSkippedTestCase(
                            _diagnosticMessageSink,
                            discoveryOptions,
                            testMethod,
                            GetSkipReason(connection, missingRequired, presentBlocked),
                            [connection.ConnectionString]);
                        testCases.Add(skipCase);

                        continue;
                    }
                }

                // Case meets all the requirements. Let's run it.
                IXunitTestCase testCase = new XunitTestCase(
                    _diagnosticMessageSink,
                    discoveryOptions.MethodDisplayOrDefault(),
                    discoveryOptions.MethodDisplayOptionsOrDefault(),
                    testMethod,
                    [connection.ConnectionString]);
                testCases.Add(testCase);
            }

            // Return *something* if nothing was generated.
            if (testCases.Count == 0)
            {
                // @TODO: Allow configuration to switch between failure and skip?
                IXunitTestCase noTestCases = new OnlineTestSkippedTestCase(
                    _diagnosticMessageSink,
                    discoveryOptions,
                    testMethod,
                    "No configured connections match test requirements.");
                testCases.Add(noTestCases);
            }

            return testCases;
        }
        catch (Exception ex)
        {
            return
            [
                new OnlineTestFailedTestCase(
                    _diagnosticMessageSink,
                    discoveryOptions,
                    testMethod,
                    ex.ToString())
            ];
        }
    }

    private ConnectionTraits[] GetMissingRequiredTraits(
        ConnectionMetadata connection,
        ConnectionTraits[] requiredTraits)
    {
        return requiredTraits.Where(t => !connection.HasTrait(t)).ToArray();
    }

    private ConnectionTraits[] GetPresentBlockedTraits(
        ConnectionMetadata connection,
        ConnectionTraits[] blockedTraits)
    {
        return blockedTraits.Where(t => connection.HasTrait(t)).ToArray();
    }

    private string GetSkipReason(
        ConnectionMetadata connectionMetadata,
        ConnectionTraits[] missingRequired,
        ConnectionTraits[] presentBlocked)
    {
        List<string> skipReasons = new();

        if (missingRequired.Length > 0)
        {
            IEnumerable<string> traitNames = missingRequired.Select(t => t.ToString());
            skipReasons.Add($"Missing Required: {string.Join(", ", traitNames)}");
        }

        if (presentBlocked.Length > 0)
        {
            IEnumerable<string> traitNames = presentBlocked.Select(t => t.ToString());
            skipReasons.Add($"Present Blocked: {string.Join(", ", traitNames)}");
        }

        return $"Connection \"{connectionMetadata.Name}\" does not meet requirements. " +
               string.Join("; ", skipReasons);
    }
}
