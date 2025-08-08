// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.StressTests.TestBase
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class StressTestAttribute : Attribute
    {
        public StressTestAttribute(string title)
        {
            Title = title;
        }

        public string Category { get; set; } = "unknown";

        public string Description { get; set; } = "none provided";

        public string Improvement { get; set; } = "ADONETV3";

        public string Owner { get; set; } = "unknown";

        public TestPriority Priority { get; set; } = TestPriority.Bvt;

        public string Title { get; set; }

        public int Weight { get; set; } = 1;
    }
}
