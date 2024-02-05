// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.PreLogin;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlConnectionEncryptionTests
    {
        [Fact]
        public void ConnectionTest()
        {
            using (SqlClientListener listener = new SqlClientListener())
            {
                // Happy path
                //AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

                //using TestTdsServer server = TestTdsServer.StartTestServer(false, false, 5, encryptionType: TDSPreLoginTokenEncryptionType.NotSupported);
                //SqlConnectionStringBuilder builder = new(server.ConnectionString)
                //{
                //    IntegratedSecurity = true,
                //    Encrypt = false,
                //};

                //using SqlConnection connection = new(builder.ConnectionString);
                //connection.Open();
                //Assert.Equal(ConnectionState.Open, connection.State);

                AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

                TestTdsServer server = TestTdsServer.StartTestServer(enableFedAuth: false, enableLog: false, connectionTimeout: 15,
                                        methodName: "",
                                        new X509Certificate2("localhostcert.pfx", "nopassword", X509KeyStorageFlags.UserKeySet),
                                        encryptionType: TDSPreLoginTokenEncryptionType.On);

                SqlConnectionStringBuilder builder = new(server.ConnectionString)
                {
                    Encrypt = SqlConnectionEncryptOption.Mandatory,
                    TrustServerCertificate = true,
                    IntegratedSecurity = true,
                };

                using (SqlConnection connection = new(builder.ConnectionString))
                {
                    connection.Open();
                    Assert.Equal(ConnectionState.Open, connection.State);
                }
            }
        }

        [Fact]
        public void ConnectionServerCertificateTest()
        {
            using (SqlClientListener listener = new SqlClientListener())
            {
                using (TestTdsServer server = TestTdsServer.StartTestServer(false, false, 60, "",
                new X509Certificate2("localhostcert.pfx", "nopassword", X509KeyStorageFlags.UserKeySet),
                encryptionType: TDSPreLoginTokenEncryptionType.On))
                {
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(server.ConnectionString);
                    builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
                    builder.TrustServerCertificate = false;
                    builder.ServerCertificate = "localhostcert.cer";
                    using (SqlConnection connection = new(builder.ConnectionString))
                    {
                        connection.Open();
                        Assert.Equal(ConnectionState.Open, connection.State);
                    }
                }
            }
        }
    }
    public class SqlClientListener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // Only enable events from SqlClientEventSource.
            if (eventSource.Name.Equals("Microsoft.Data.SqlClient.EventSource"))
            {
                // Use EventKeyWord 2 to capture basic application flow events.
                // See the above table for all available keywords.
                EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)2);
                //EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        // This callback runs whenever an event is written by SqlClientEventSource.
        // Event data is accessed through the EventWrittenEventArgs parameter.
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // Print event data.
            if (eventData != null && eventData.Payload != null && eventData.Payload[0].ToString() != "")
                Debug.WriteLine($"  {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss:ff")}   {eventData.Payload[0]}");
        }
    }

}

