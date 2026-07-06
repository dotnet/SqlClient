// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Data.SqlClient.OnlineTests.Utilities;

public sealed class OnlineTestSkippedTestCase : XunitTestCase, IXunitTestCase
{
    private string? _skipReason;

    #pragma warning disable CS0618 // Required by xUnit for test case deserialization.
    public OnlineTestSkippedTestCase()
    {
    }
    #pragma warning restore CS0618

    public OnlineTestSkippedTestCase(
        IMessageSink diagnosticMessageSink,
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        string skipReason,
        object[]? testMethodArguments = null)
        : base(
            diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod,
            testMethodArguments ?? [])
    {
        _skipReason = skipReason;
    }

    string ITestCase.SkipReason => _skipReason ?? string.Empty;

    public override void Deserialize(IXunitSerializationInfo data)
    {
        base.Deserialize(data);

        _skipReason = data.GetValue<string>(nameof(SkipReason));
    }

    public override Task<RunSummary> RunAsync(
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        object[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
        => new XunitTestCaseRunner(
            this,
            DisplayName,
            _skipReason,
            constructorArguments,
            TestMethodArguments,
            messageBus,
            aggregator,
            cancellationTokenSource).RunAsync();

    public override void Serialize(IXunitSerializationInfo data)
    {
        base.Serialize(data);

        data.AddValue(nameof(SkipReason), _skipReason);
    }
}

