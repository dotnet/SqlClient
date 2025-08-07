// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.StressTests.Runner;

namespace Microsoft.Data.SqlClient.StressTests.TestBase
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ThreadPoolTestAttribute : TestAttributeBase
    {
        private int _warmupDuration = 60;
        private int _testDuration = 60;
        private int _threads = 64;

        public ThreadPoolTestAttribute(string title)
            : base(title)
        {
        }

        public int WarmupDuration
        {
            get
            {
                string propName = "WarmupDuration";

                if (TestMetrics.Overrides.ContainsKey(propName))
                {
                    return int.Parse(TestMetrics.Overrides[propName]);
                }
                else
                {
                    return _warmupDuration;
                }
            }
            set { _warmupDuration = value; }
        }

        public int TestDuration
        {
            get
            {
                string propName = "TestDuration";

                if (TestMetrics.Overrides.ContainsKey(propName))
                {
                    return int.Parse(TestMetrics.Overrides[propName]);
                }
                else
                {
                    return _testDuration;
                }
            }
            set { _testDuration = value; }
        }

        public int Threads
        {
            get
            {
                string propName = "Threads";

                if (TestMetrics.Overrides.ContainsKey(propName))
                {
                    return int.Parse(TestMetrics.Overrides[propName]);
                }
                else
                {
                    return _threads;
                }
            }
            set { _threads = value; }
        }
    }
}
