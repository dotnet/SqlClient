// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.StressTests.Runner;

namespace Microsoft.Data.SqlClient.StressTests.TestBase
{
    public class TestAttributeBase : Attribute
    {
        private string _title;
        private string _description = "none provided";
        private string _applicationName = "unknown";
        private string _improvement = "ADONETV3";
        private string _owner = "unknown";
        private string _category = "unknown";
        private TestPriority _priority = TestPriority.Bvt;

        public TestAttributeBase(string title)
        {
            _title = title;
        }

        public string Title
        {
            get { return _title; }
            set { _title = value; }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        public string Improvement
        {
            get { return _improvement; }
            set { _improvement = value; }
        }

        public string Owner
        {
            get { return _owner; }
            set { _owner = value; }
        }

        public string ApplicationName
        {
            get { return _applicationName; }
            set { _applicationName = value; }
        }

        public TestPriority Priority
        {
            get { return _priority; }
            set { _priority = value; }
        }

        public string Category
        {
            get { return _category; }
            set { _category = value; }
        }
    }
}
