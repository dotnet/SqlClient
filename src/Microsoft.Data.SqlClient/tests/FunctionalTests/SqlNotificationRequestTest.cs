// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Sql;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlNotificationRequestTest
    {
        [Fact]
        public void SetOptions_OutOfRangeValue_Throws()
        {
            SqlNotificationRequest sqlNotification = new();
            string outOfRangeValue = new string('a', ushort.MaxValue + 1);
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => sqlNotification.Options = outOfRangeValue);
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.ParamName);
            Assert.True(ex.ParamName.IndexOf("Options", StringComparison.OrdinalIgnoreCase) != -1);
        }

        [Fact]
        public void SetUserData_OutOfRangeValue_Throws()
        {
            SqlNotificationRequest sqlNotification = new();
            string outOfRangeValue = new string('a', ushort.MaxValue + 1);
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => sqlNotification.UserData = outOfRangeValue);
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.ParamName);
            Assert.True(ex.ParamName.IndexOf("UserData", StringComparison.OrdinalIgnoreCase) != -1);
        }

        [Fact]
        public void SetTimeout_OutOfRangeValue_Throws()
        {
            SqlNotificationRequest sqlNotification = new();
            int outOfRangeValue = -1;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => sqlNotification.Timeout = outOfRangeValue);
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.ParamName);
            Assert.True(ex.ParamName.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) != -1);
        }
    }
}
