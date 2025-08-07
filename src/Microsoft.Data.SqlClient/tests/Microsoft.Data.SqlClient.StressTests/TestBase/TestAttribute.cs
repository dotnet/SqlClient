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
        private int _warmupIterations = 0;
        private int _testIterations = 1;

        public TestAttribute(string title) : base(title)
        {
        }

        public int WarmupIterations
        {
            get
            {
                string propName = "WarmupIterations";

                if (TestMetrics.Overrides.ContainsKey(propName))
                {
                    return int.Parse(TestMetrics.Overrides[propName]);
                }
                else
                {
                    return _warmupIterations;
                }
            }
            set { _warmupIterations = value; }
        }

        public int TestIterations
        {
            get
            {
                string propName = "TestIterations";

                if (TestMetrics.Overrides.ContainsKey(propName))
                {
                    return int.Parse(TestMetrics.Overrides[propName]);
                }
                else
                {
                    return _testIterations;
                }
            }
            set { _testIterations = value; }
        }
    }
}
