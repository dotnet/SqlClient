// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.StressTests.Runner;

namespace Microsoft.Data.SqlClient.StressTests.TestBase
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class MultithreadedTestAttribute : TestAttributeBase
    {
        #region Constants

        private const string OverridePropertyNameTestDuration = "TestDuration";
        private const string OverridePropertyNameThreads = "Threads";
        private const string OverridePropertyNameWarmupDuration = "WarmupDuration";

        #endregion

        #region Member Variables

        private int _warmupDuration = 60;
        private int _testDuration = 60;
        private int _threads = 16;

        #endregion

        public MultithreadedTestAttribute(string title)
            : base(title)
        {
        }

        // @TODO: Move override checking to the test itself, that way we can make TestMetrics not static

        #region Properties

        public int TestDuration
        {
            get => TestMetrics.GetOverrideOrFallback(OverridePropertyNameTestDuration, _testDuration);
            set => _testDuration = value;
        }

        public int Threads
        {
            get => TestMetrics.GetOverrideOrFallback(OverridePropertyNameThreads, _threads);
            set => _threads = value;
        }

        public int WarmupDuration
        {
            get => TestMetrics.GetOverrideOrFallback(OverridePropertyNameWarmupDuration, _warmupDuration);
            set => _warmupDuration = value;
        }

        #endregion
    }
}
