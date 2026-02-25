// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    internal sealed class SsrpClient
    {
        private const char SemicolonSeparator = ';';
        private const int SqlServerBrowserPort = 1434; //port SQL Server Browser
        private const int ReceiveMAXTimeoutsForCLNT_BCAST_EX = 15000; //Default max time for response wait
        private const int ReceiveTimeoutsForCLNT_BCAST_EX = 1000; //subsequent wait time for response after intial wait 
        private const int ServerResponseHeaderSizeForCLNT_BCAST_EX = 3;//(SVR_RESP + RESP_SIZE) https://docs.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/2e1560c9-5097-4023-9f5e-72b9ff1ec3b1
        private const int ValidResponseSizeForCLNT_BCAST_EX = 4096; //valid reponse size should be less than 4096
        private const int FirstTimeoutForCLNT_BCAST_EX = 5000;//wait for first response for 5 seconds
        private const int CLNT_BCAST_EX = 2;//request packet

        /// <summary>
        /// Finds instance port number for given instance name.
        /// </summary>
        /// <param name="browserHostName">SQL Sever Browser hostname</param>
        /// <param name="instanceName">instance name to find port number</param>
        /// <param name="timeout">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>port number for given instance name</returns>
        internal static int GetPortByInstanceName(string browserHostName, string instanceName, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(browserHostName), "browserHostName should not be null, empty, or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");
            using (SqlClientSNIEventScope.Create(nameof(SsrpClient)))
            {
                byte[] instanceInfoRequest = CreateInstanceInfoRequest(instanceName);
                byte[] responsePacket = null;
                try
                {
                    responsePacket = SendUDPRequest(browserHostName, SqlServerBrowserPort, instanceInfoRequest, timeout, allIPsInParallel, ipPreference);
                }
                catch (SocketException se)
                {
                    // A SocketException is possible for an instance name that doesn't exist.
                    // If there are multiple IP addresses and one of them fails with a SocketException but
                    // others simply don't respond because the instance name is invalid, we want to return
                    // the same error as if the response was empty. The higher error suits all scenarios.
                    // But log it, just in case there is a different, underlying issue that support needs
                    // to troubleshoot.
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SsrpClient), EventType.ERR, "SocketException Message = {0}", args0: se?.Message);
                    throw;
                }

                const byte SvrResp = 0x05;
                if (responsePacket == null || responsePacket.Length <= 3 || responsePacket[0] != SvrResp ||
                    BitConverter.ToUInt16(responsePacket, 1) != responsePacket.Length - 3)
                {
                    throw new SocketException();
                }

                string serverMessage = Encoding.ASCII.GetString(responsePacket, 3, responsePacket.Length - 3);

                string[] elements = serverMessage.Split(SemicolonSeparator);
                int tcpIndex = Array.IndexOf(elements, "tcp");
                if (tcpIndex < 0 || tcpIndex == elements.Length - 1)
                {
                    throw new SocketException();
                }

                return ushort.Parse(elements[tcpIndex + 1]);
            }
        }

        /// <summary>
        /// Creates instance port lookup request (CLNT_UCAST_INST) for given instance name.
        /// </summary>
        /// <param name="instanceName">instance name to lookup port</param>
        /// <returns>Byte array of instance port lookup request (CLNT_UCAST_INST)</returns>
        private static byte[] CreateInstanceInfoRequest(string instanceName)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");
            using (SqlClientSNIEventScope.Create(nameof(SsrpClient)))
            {
                const byte ClntUcastInst = 0x04;
                instanceName += char.MinValue;
                int byteCount = Encoding.ASCII.GetByteCount(instanceName);

                byte[] requestPacket = new byte[byteCount + 1];
                requestPacket[0] = ClntUcastInst;
                Encoding.ASCII.GetBytes(instanceName, 0, instanceName.Length, requestPacket, 1);

                return requestPacket;
            }
        }

        /// <summary>
        /// Finds DAC port for given instance name.
        /// </summary>
        /// <param name="browserHostName">SQL Sever Browser hostname</param>
        /// <param name="instanceName">instance name to lookup DAC port</param>
        /// <param name="timeout">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>DAC port for given instance name</returns>
        internal static int GetDacPortByInstanceName(string browserHostName, string instanceName, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(browserHostName), "browserHostName should not be null, empty, or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");

            byte[] dacPortInfoRequest = CreateDacPortInfoRequest(instanceName);
            byte[] responsePacket = SendUDPRequest(browserHostName, SqlServerBrowserPort, dacPortInfoRequest, timeout, allIPsInParallel, ipPreference);

            const byte SvrResp = 0x05;
            const byte ProtocolVersion = 0x01;
            const byte RespSize = 0x06;
            if (responsePacket == null || responsePacket.Length <= 4 || responsePacket[0] != SvrResp ||
                BitConverter.ToUInt16(responsePacket, 1) != RespSize || responsePacket[3] != ProtocolVersion)
            {
                throw new SocketException();
            }

            int dacPort = BitConverter.ToUInt16(responsePacket, 4);
            return dacPort;
        }

        /// <summary>
        /// Creates DAC port lookup request (CLNT_UCAST_DAC) for given instance name.
        /// </summary>
        /// <param name="instanceName">instance name to lookup DAC port</param>
        /// <returns>Byte array of DAC port lookup request (CLNT_UCAST_DAC)</returns>
        private static byte[] CreateDacPortInfoRequest(string instanceName)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");

            const byte ClntUcastDac = 0x0F;
            const byte ProtocolVersion = 0x01;
            instanceName += char.MinValue;
            int byteCount = Encoding.ASCII.GetByteCount(instanceName);

            byte[] requestPacket = new byte[byteCount + 2];
            requestPacket[0] = ClntUcastDac;
            requestPacket[1] = ProtocolVersion;
            Encoding.ASCII.GetBytes(instanceName, 0, instanceName.Length, requestPacket, 2);

            return requestPacket;
        }

        private class SsrpResult
        {
            public byte[] ResponsePacket;
            public Exception Error;
        }

        /// <summary>
        /// Sends request to server, and receives response from server by UDP.
        /// </summary>
        /// <param name="browserHostname">UDP server hostname</param>
        /// <param name="port">UDP server port</param>
        /// <param name="requestPacket">request packet</param>
        /// <param name="timeout">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>response packet from UDP server</returns>
        private static byte[] SendUDPRequest(string browserHostname, int port, byte[] requestPacket, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
        {
            using (SqlClientSNIEventScope.Create(nameof(SsrpClient)))
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(browserHostname), "browserhostname should not be null, empty, or whitespace");
                Debug.Assert(port >= 0 && port <= 65535, "Invalid port");
                Debug.Assert(requestPacket != null && requestPacket.Length > 0, "requestPacket should not be null or 0-length array");

                if (IPAddress.TryParse(browserHostname, out IPAddress address))
                {
                    SsrpResult response = SendUDPRequest(new IPAddress[] { address }, port, requestPacket, allIPsInParallel);
                    if (response != null && response.ResponsePacket != null)
                    {
                        return response.ResponsePacket;
                    }
                    else if (response != null && response.Error != null)
                    {
                        throw response.Error;
                    }
                    else
                    {
                        return null;
                    }
                }

                IPAddress[] ipAddresses = timeout.IsInfinite
                    ? SniCommon.GetDnsIpAddresses(browserHostname)
                    : SniCommon.GetDnsIpAddresses(browserHostname, timeout);

                Debug.Assert(ipAddresses.Length > 0, "DNS should throw if zero addresses resolve");
                IPAddress[] ipv4Addresses = null;
                IPAddress[] ipv6Addresses = null;
                switch (ipPreference)
                {
                    case SqlConnectionIPAddressPreference.IPv4First:
                        {
                            SplitIPv4AndIPv6(ipAddresses, out ipv4Addresses, out ipv6Addresses);

                            SsrpResult response4 = SendUDPRequest(ipv4Addresses, port, requestPacket, allIPsInParallel);
                            if (response4 != null && response4.ResponsePacket != null)
                            {
                                return response4.ResponsePacket;
                            }

                            SsrpResult response6 = SendUDPRequest(ipv6Addresses, port, requestPacket, allIPsInParallel);
                            if (response6 != null && response6.ResponsePacket != null)
                            {
                                return response6.ResponsePacket;
                            }

                            // No responses so throw first error
                            if (response4 != null && response4.Error != null)
                            {
                                throw response4.Error;
                            }
                            else if (response6 != null && response6.Error != null)
                            {
                                throw response6.Error;
                            }

                            break;
                        }
                    case SqlConnectionIPAddressPreference.IPv6First:
                        {
                            SplitIPv4AndIPv6(ipAddresses, out ipv4Addresses, out ipv6Addresses);

                            SsrpResult response6 = SendUDPRequest(ipv6Addresses, port, requestPacket, allIPsInParallel);
                            if (response6 != null && response6.ResponsePacket != null)
                            {
                                return response6.ResponsePacket;
                            }

                            SsrpResult response4 = SendUDPRequest(ipv4Addresses, port, requestPacket, allIPsInParallel);
                            if (response4 != null && response4.ResponsePacket != null)
                            {
                                return response4.ResponsePacket;
                            }

                            // No responses so throw first error
                            if (response6 != null && response6.Error != null)
                            {
                                throw response6.Error;
                            }
                            else if (response4 != null && response4.Error != null)
                            {
                                throw response4.Error;
                            }

                            break;
                        }
                    default:
                        {
                            SsrpResult response = SendUDPRequest(ipAddresses, port, requestPacket, true); // allIPsInParallel);
                            if (response != null && response.ResponsePacket != null)
                            {
                                return response.ResponsePacket;
                            }
                            else if (response != null && response.Error != null)
                            {
                                throw response.Error;
                            }

                            break;
                        }
                }

                return null;
            }
        }

        /// <summary>
        /// Sends request to server, and receives response from server by UDP.
        /// </summary>
        /// <param name="ipAddresses">IP Addresses</param>
        /// <param name="port">UDP server port</param>
        /// <param name="requestPacket">request packet</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <returns>response packet from UDP server</returns>
        private static SsrpResult SendUDPRequest(IPAddress[] ipAddresses, int port, byte[] requestPacket, bool allIPsInParallel)
        {
            if (ipAddresses.Length == 0)
            {
                return null;
            }

            if (allIPsInParallel) // Used for MultiSubnetFailover
            {
                List<Task<SsrpResult>> tasks = new(ipAddresses.Length);
                CancellationTokenSource cts = new CancellationTokenSource();
                for (int i = 0; i < ipAddresses.Length; i++)
                {
                    IPEndPoint endPoint = new IPEndPoint(ipAddresses[i], port);
                    tasks.Add(Task.Factory.StartNew<SsrpResult>(() => SendUDPRequest(endPoint, requestPacket), cts.Token));
                }

                List<Task<SsrpResult>> completedTasks = new();
                while (tasks.Count > 0)
                {
                    int first = Task.WaitAny(tasks.ToArray());
                    if (tasks[first].Result.ResponsePacket != null)
                    {
                        cts.Cancel();
                        return tasks[first].Result;
                    }
                    else
                    {
                        completedTasks.Add(tasks[first]);
                        tasks.Remove(tasks[first]);
                    }
                }

                Debug.Assert(completedTasks.Count > 0, "completedTasks should never be 0");

                // All tasks failed. Return the error from the first failure.
                return completedTasks[0].Result;
            }
            else
            {
                // If not parallel, use the first IP address provided
                IPEndPoint endPoint = new IPEndPoint(ipAddresses[0], port);
                return SendUDPRequest(endPoint, requestPacket);
            }
        }

        private static SsrpResult SendUDPRequest(IPEndPoint endPoint, byte[] requestPacket)
        {
            const int sendTimeOutMs = 1000;
            const int receiveTimeOutMs = 1000;

            SsrpResult result = new();

            try
            {
                using (UdpClient client = new UdpClient(endPoint.AddressFamily))
                {
                    Task<int> sendTask = client.SendAsync(requestPacket, requestPacket.Length, endPoint);
                    Task<UdpReceiveResult> receiveTask = null;

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SsrpClient), EventType.INFO, "Waiting for UDP Client to fetch Port info.");
                    if (sendTask.Wait(sendTimeOutMs) && (receiveTask = client.ReceiveAsync()).Wait(receiveTimeOutMs))
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SsrpClient), EventType.INFO, "Received Port info from UDP Client.");
                        result.ResponsePacket = receiveTask.Result.Buffer;
                    }
                }
            }
            catch (AggregateException ae)
            {
                if (ae.InnerExceptions.Count > 0)
                {
                    // Log all errors
                    foreach (Exception e in ae.InnerExceptions)
                    {
                        // Favor SocketException for returned error
                        if (e is SocketException)
                        {
                            result.Error = e;
                        }
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SsrpClient), EventType.INFO,
                            "SendUDPRequest ({0}) resulted in exception: {1}", args0: endPoint.ToString(), args1: e.Message);
                    }

                    // Return first error if we didn't find a SocketException
                    result.Error = result.Error == null ? ae.InnerExceptions[0] : result.Error;
                }
                else
                {
                    result.Error = ae;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SsrpClient), EventType.INFO,
                        "SendUDPRequest ({0}) resulted in exception: {1}", args0: endPoint.ToString(), args1: ae.Message);
                }
            }
            catch (Exception e)
            {
                result.Error = e;
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SsrpClient), EventType.INFO,
                    "SendUDPRequest ({0}) resulted in exception: {1}", args0: endPoint.ToString(), args1: e.Message);
            }

            return result;
        }

        /// <summary>
        /// Sends request to server, and receives response from server (SQLBrowser) on port 1434 by UDP
        /// Request (https://docs.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/a3035afa-c268-4699-b8fd-4f351e5c8e9e)
        /// Response (https://docs.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/2e1560c9-5097-4023-9f5e-72b9ff1ec3b1) 
        /// </summary>
        /// <returns>string containing list of SVR_RESP(just RESP_DATA)</returns>
        internal static string SendBroadcastUDPRequest()
        {
            StringBuilder response = new StringBuilder();
            byte[] CLNT_BCAST_EX_Request = new byte[1] { CLNT_BCAST_EX }; //0x02
            // Waits 5 seconds for the first response and every 1 second up to 15 seconds
            // https://docs.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/f2640a2d-3beb-464b-a443-f635842ebc3e#Appendix_A_3
            int currentTimeOut = FirstTimeoutForCLNT_BCAST_EX;

            using (SqlClientSNIEventScope.Create(nameof(SsrpClient)))
            {
                using (UdpClient clientListener = new UdpClient())
                {
                    Task<int> sendTask = clientListener.SendAsync(CLNT_BCAST_EX_Request, CLNT_BCAST_EX_Request.Length, new IPEndPoint(IPAddress.Broadcast, SqlServerBrowserPort));
                    Task<UdpReceiveResult> receiveTask = null;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SsrpClient), EventType.INFO, "Waiting for UDP Client to fetch list of instances.");
                    Stopwatch sw = new Stopwatch(); //for waiting until 15 sec elapsed
                    sw.Start();
                    try
                    {
                        while ((receiveTask = clientListener.ReceiveAsync()).Wait(currentTimeOut) && sw.ElapsedMilliseconds <= ReceiveMAXTimeoutsForCLNT_BCAST_EX && receiveTask != null)
                        {
                            currentTimeOut = ReceiveTimeoutsForCLNT_BCAST_EX;
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SsrpClient), EventType.INFO, "Received instnace info from UDP Client.");
                            if (receiveTask.Result.Buffer.Length < ValidResponseSizeForCLNT_BCAST_EX) //discard invalid response
                            {
                                response.Append(Encoding.ASCII.GetString(receiveTask.Result.Buffer, ServerResponseHeaderSizeForCLNT_BCAST_EX, receiveTask.Result.Buffer.Length - ServerResponseHeaderSizeForCLNT_BCAST_EX)); //RESP_DATA(VARIABLE) - 3 (RESP_SIZE + SVR_RESP)
                            }
                        }
                    }
                    finally
                    {
                        sw.Stop();
                    }
                }
            }
            return response.ToString();
        }

        private static void SplitIPv4AndIPv6(IPAddress[] input, out IPAddress[] ipv4Addresses, out IPAddress[] ipv6Addresses)
        {
            ipv4Addresses = Array.Empty<IPAddress>();
            ipv6Addresses = Array.Empty<IPAddress>();

            if (input != null && input.Length > 0)
            {
                List<IPAddress> v4 = new List<IPAddress>(1);
                List<IPAddress> v6 = new List<IPAddress>(0);

                for (int index = 0; index < input.Length; index++)
                {
                    switch (input[index].AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            v4.Add(input[index]);
                            break;
                        case AddressFamily.InterNetworkV6:
                            v6.Add(input[index]);
                            break;
                    }
                }

                if (v4.Count > 0)
                {
                    ipv4Addresses = v4.ToArray();
                }

                if (v6.Count > 0)
                {
                    ipv6Addresses = v6.ToArray();
                }
            }
        }
    }
}

#endif
