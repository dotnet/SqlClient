// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.StressTests.Runner;

namespace Microsoft.Data.SqlClient.StressTests.TestBase
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class TestAttribute : TestAttributeBase
    {
        private const string OverridePropertyNameTestIterations = "TestIterations";
        private const string OverridePropertyNameWarmupIterations = "WarmupIterations";

        private int _warmupIterations = 0;
        private int _testIterations = 1;

        public TestAttribute(string title)
            : base(title)
        {
        }

        // @TODO: Move override checking to the test itself, that way we can make TestMetrics not static

        public int TestIterations
        {
            get => TestMetrics.GetOverrideOrFallback(OverridePropertyNameTestIterations, _testIterations);
            set => _testIterations = value;
        }

        public int WarmupIterations
        {
            get => TestMetrics.GetOverrideOrFallback(OverridePropertyNameWarmupIterations, _warmupIterations);
            set => _warmupIterations = value;
        }
    }
}
