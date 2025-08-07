// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.StressTests.Runner;

namespace Microsoft.Data.SqlClient.StressTests.TestBase
{
    public abstract class TestAttributeBase : Attribute
    {
        protected TestAttributeBase(string title)
        {
            Title = title;
        }

        public string Title { get; set; }

        public string Description { get; set; } = "none provided";

        public string Improvement { get; set; } = "ADONETV3";

        public string Owner { get; set; } = "unknown";

        public TestPriority Priority { get; set; } = TestPriority.Bvt;

        public string Category { get; set; } = "unknown";
    }
}
