// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlBulkCopyTest
    {
        [Fact]
        public void ConstructorNotNull1()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlBulkCopy((SqlConnection)null));
        }

        [Fact]
        public void ConstructorNotNull2()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlBulkCopy((string)null));
        }

        [Fact]
        public void ConstructorNotNull3()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlBulkCopy((SqlConnection)null));
        }

        [Fact]
        public void ConstructorNotNull4()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlBulkCopy((SqlConnection)null));
        }

        [Fact]
        public void ConstructorNotNull5()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlBulkCopy((SqlConnection)null));
        }

        [Fact]
        public void ConstructorNotNull6()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlBulkCopy((SqlConnection)null));
        }
    }
}
