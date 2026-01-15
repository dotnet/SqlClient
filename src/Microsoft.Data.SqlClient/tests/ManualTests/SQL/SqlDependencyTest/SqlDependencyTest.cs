// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlDependencyTest
    {
        [Fact]
        public void AddCommandDependency()
        {
            SqlDependency sqlDependency = new SqlDependency();
            SqlCommand sqlCommand = new SqlCommand("command");
            sqlDependency.AddCommandDependency(sqlCommand);
        }

        [Fact]
        public void AddCommandDependencyHasChanges()
        {
            SqlDependency dep = new SqlDependency();
            SqlCommand cmd = new SqlCommand("command");
            Type sqlDependencyType = typeof(SqlDependency);
            FieldInfo dependencyFiredField = sqlDependencyType.GetField("_dependencyFired", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            dependencyFiredField.SetValue(dep, true);

            dep.AddCommandDependency(cmd);
        }

        [Fact]
        public void AddCommandDependencyNull_Throws()
        {
            SqlDependency sqlDependency = new SqlDependency();
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => sqlDependency.AddCommandDependency(null));
            Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConstructorInvalidTimeout_Throws()
        {
            SqlCommand sqlCommand = new SqlCommand("command");
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                SqlDependency sqlDependency = new SqlDependency(sqlCommand, "", -1);
            });
            Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConstructorSqlCommandWithNotification_Throws()
        {
            SqlCommand sqlCommand = new SqlCommand("command");
            sqlCommand.Notification = new Sql.SqlNotificationRequest();
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                SqlDependency sqlDependency = new SqlDependency(sqlCommand);
            });
            Assert.Contains("sqlcommand", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HasChanges()
        {
            SqlDependency sqlDependency = new SqlDependency();
            Assert.False(sqlDependency.HasChanges);
        }

        [Fact]
        public void OnChangeRemove()
        {
            SqlDependency dep = new SqlDependency();
            OnChangeEventHandler tempDelegate = delegate (object o, SqlNotificationEventArgs args)
            {
                Console.WriteLine("Notification callback. Type={0}, Info={1}, Source={2}", args.Type, args.Info, args.Source);
            };
            dep.OnChange += tempDelegate;
            dep.OnChange -= tempDelegate;
        }

        [Fact]
        public void OnChangeAddDuplicate_Throws()
        {
            SqlDependency dep = new SqlDependency();
            OnChangeEventHandler tempDelegate = delegate (object o, SqlNotificationEventArgs args)
            {
                Console.WriteLine("Notification callback. Type={0}, Info={1}, Source={2}", args.Type, args.Info, args.Source);
            };
            dep.OnChange += tempDelegate;

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                dep.OnChange += tempDelegate;
            });
            Assert.Contains("same", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void OnChangeAddHasChanges()
        {
            SqlDependency dep = new SqlDependency();
            Type sqlDependencyType = typeof(SqlDependency);
            FieldInfo dependencyFiredField = sqlDependencyType.GetField("_dependencyFired", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            dependencyFiredField.SetValue(dep, true);

            OnChangeEventHandler tempDelegate = delegate (object o, SqlNotificationEventArgs args)
            {
                Console.WriteLine("Notification callback. Type={0}, Info={1}, Source={2}", args.Type, args.Info, args.Source);
            };
            dep.OnChange += tempDelegate;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void SqlDependencyStartStopTest()
        {
            SqlDependency.Start(DataTestUtility.TCPConnectionString);
            SqlDependency.Stop(DataTestUtility.TCPConnectionString);
        }

        [Fact]
        public void SqlDepdencyStartEmptyConnectionString_Throws()
        {
            SqlDependency sqlDependency = new SqlDependency();
            ArgumentException ex = Assert.Throws<ArgumentException>(() => SqlDependency.Start("", null));
            Assert.Contains("connectionstring", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SqlDepdencyStartNullConnectionString_Throws()
        {
            SqlDependency sqlDependency = new SqlDependency();
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => SqlDependency.Start(null, null));
            Assert.Contains("connectionstring", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void SqlDependencyStartStopDefaultTest()
        {
            SqlDependency.Start(DataTestUtility.TCPConnectionString, null);
            SqlDependency.Stop(DataTestUtility.TCPConnectionString, null);
        }

        [Fact]
        public void SqlDepdencyStopEmptyConnectionString_Throws()
        {
            SqlDependency sqlDependency = new SqlDependency();
            ArgumentException ex = Assert.Throws<ArgumentException>(() => SqlDependency.Stop("", null));
            Assert.Contains("connectionstring", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SqlDepdencyStopNullConnectionString_Throws()
        {
            SqlDependency sqlDependency = new SqlDependency();
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => SqlDependency.Stop(null, null));
            Assert.Contains("connectionstring", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
