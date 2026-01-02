// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class XmlReaderAsyncTest
    {
        private const string CommandText =
            "SELECT * from dbo.Customers FOR XML AUTO, XMLDATA;";

        // Synapse: Parse error at line: 1, column: 29: Incorrect syntax near 'FOR'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ExecuteTest()
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommand command = new SqlCommand(CommandText, connection))
            {
                connection.Open();

                IAsyncResult result = command.BeginExecuteXmlReader();
                while (!result.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                using (XmlReader xmlReader = command.EndExecuteXmlReader(result))
                {
                    // Issue #781: Test failed here as xmlReader.Settings.Async was set to false
                    Assert.True(xmlReader.Settings.Async);
                    xmlReader.ReadToDescendant("dbo.Customers");
                    Assert.Equal("ALFKI", xmlReader["CustomerID"]);
                }
            }
        }

        // Synapse: Northwind dependency + Parse error at line: 1, column: 29: Incorrect syntax near 'FOR'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ExceptionTest()
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommand command = new SqlCommand(CommandText, connection))
            {
                connection.Open();

                //Try to execute a synchronous query on same command
                IAsyncResult result = command.BeginExecuteXmlReader();

                Assert.Throws<InvalidOperationException>(delegate
                { command.ExecuteXmlReader(); });

                while (!result.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                using (XmlReader xmlReader = command.EndExecuteXmlReader(result))
                {
                    // Issue #781: Test failed here as xmlReader.Settings.Async was set to false
                    Assert.True(xmlReader.Settings.Async);
                    xmlReader.ReadToDescendant("dbo.Customers");
                    Assert.Equal("ALFKI", xmlReader["CustomerID"]);
                }
            }
        }

        // Synapse: Parse error at line: 1, column: 29: Incorrect syntax near 'FOR'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task MoveToContentAsyncTest()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            using SqlCommand command = new SqlCommand(CommandText, connection);
            connection.Open();

            using XmlReader xmlReader = await command.ExecuteXmlReaderAsync().ConfigureAwait(false);
            // Issue #781: Test failed here as xmlReader.Settings.Async was set to false
            Assert.True(xmlReader.Settings.Async);
            xmlReader.ReadToDescendant("dbo.Customers");
            Assert.Equal("ALFKI", xmlReader["CustomerID"]);
        }
    }
}
