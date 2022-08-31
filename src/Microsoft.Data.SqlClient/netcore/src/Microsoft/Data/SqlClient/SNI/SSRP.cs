// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    internal class SSRP
    {
        private const char SemicolonSeparator = ';';
        private const int SqlServerBrowserPort = 1434;

        /// <summary>
        /// Finds instance port number for given instance name.
        /// </summary>
        /// <param name="browserHostName">SQL Sever Browser hostname</param>
        /// <param name="instanceName">instance name to find port number</param>
        /// <param name="timerExpire">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>port number for given instance name</returns>
        internal static int GetPortByInstanceName(string browserHostName, string instanceName, long timerExpire, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(browserHostName), "browserHostName should not be null, empty, or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");
            using (TrySNIEventScope.Create(nameof(SSRP)))
            {
                byte[] instanceInfoRequest = CreateInstanceInfoRequest(instanceName);
                byte[] responsePacket = null;
                try
                {
                    responsePacket = SendUDPRequest(browserHostName, SqlServerBrowserPort, instanceInfoRequest, timerExpire, allIPsInParallel, ipPreference);
                }
                catch (SocketException se)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.ERR, "SocketException Message = {0}", args0: se?.Message);
                    throw new Exception(SQLMessage.SqlServerBrowserNotAccessible(), se);
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
            using (TrySNIEventScope.Create(nameof(SSRP)))
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
        /// <param name="timerExpire">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>DAC port for given instance name</returns>
        internal static int GetDacPortByInstanceName(string browserHostName, string instanceName, long timerExpire, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(browserHostName), "browserHostName should not be null, empty, or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");

            byte[] dacPortInfoRequest = CreateDacPortInfoRequest(instanceName);
            byte[] responsePacket = SendUDPRequest(browserHostName, SqlServerBrowserPort, dacPortInfoRequest, timerExpire, allIPsInParallel, ipPreference);

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
        /// <param name="timerExpire">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>response packet from UDP server</returns>
        private static byte[] SendUDPRequest(string browserHostname, int port, byte[] requestPacket, long timerExpire, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
        {
            using (TrySNIEventScope.Create(nameof(SSRP)))
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(browserHostname), "browserhostname should not be null, empty, or whitespace");
                Debug.Assert(port >= 0 && port <= 65535, "Invalid port");
                Debug.Assert(requestPacket != null && requestPacket.Length > 0, "requestPacket should not be null or 0-length array");

                if (IPAddress.TryParse(browserHostname, out IPAddress address))
                {
                    SsrpResult response = SendUDPRequest(new IPAddress[] { address }, port, requestPacket, allIPsInParallel);
                    if (response != null && response.ResponsePacket != null)
                        return response.ResponsePacket;
                    else if (response != null && response.Error != null)
                        throw response.Error;
                    else
                        return null;
                }

                TimeSpan ts = default;
                // In case the Timeout is Infinite, we will receive the max value of Int64 as the tick count
                // The infinite Timeout is a function of ConnectionString Timeout=0
                if (long.MaxValue != timerExpire)
                {
                    ts = DateTime.FromFileTime(timerExpire) - DateTime.Now;
                    ts = ts.Ticks < 0 ? TimeSpan.FromTicks(0) : ts;
                }

                IPAddress[] ipAddresses = SNICommon.GetDnsIpAddresses(browserHostname);
                Debug.Assert(ipAddresses.Length > 0, "DNS should throw if zero addresses resolve");

                switch (ipPreference)
                {
                    case SqlConnectionIPAddressPreference.IPv4First:
                        {
                            SsrpResult response4 = SendUDPRequest(ipAddresses.Where(i => i.AddressFamily == AddressFamily.InterNetwork).ToArray(), port, requestPacket, allIPsInParallel);
                            if (response4 != null && response4.ResponsePacket != null)
                                return response4.ResponsePacket;

                            SsrpResult response6 = SendUDPRequest(ipAddresses.Where(i => i.AddressFamily == AddressFamily.InterNetworkV6).ToArray(), port, requestPacket, allIPsInParallel);
                            if (response6 != null && response6.ResponsePacket != null)
                                return response6.ResponsePacket;

                            // No responses so throw first error
                            if (response4 != null && response4.Error != null)
                                throw response4.Error;
                            else if (response6 != null && response6.Error != null)
                                throw response6.Error;

                            break;
                        }
                    case SqlConnectionIPAddressPreference.IPv6First:
                        {
                            SsrpResult response6 = SendUDPRequest(ipAddresses.Where(i => i.AddressFamily == AddressFamily.InterNetworkV6).ToArray(), port, requestPacket, allIPsInParallel);
                            if (response6 != null && response6.ResponsePacket != null)
                                return response6.ResponsePacket;

                            SsrpResult response4 = SendUDPRequest(ipAddresses.Where(i => i.AddressFamily == AddressFamily.InterNetwork).ToArray(), port, requestPacket, allIPsInParallel);
                            if (response4 != null && response4.ResponsePacket != null)
                                return response4.ResponsePacket;

                            // No responses so throw first error
                            if (response6 != null && response6.Error != null)
                                throw response6.Error;
                            else if (response4 != null && response4.Error != null)
                                throw response4.Error;

                            break;
                        }
                    default:
                        {
                            SsrpResult response = SendUDPRequest(ipAddresses, port, requestPacket, true); // allIPsInParallel);
                            if (response != null && response.ResponsePacket != null)
                                return response.ResponsePacket;
                            else if (response != null && response.Error != null)
                                throw response.Error;

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
                return null;

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

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO, "Waiting for UDP Client to fetch Port info.");
                    if (sendTask.Wait(sendTimeOutMs) && (receiveTask = client.ReceiveAsync()).Wait(receiveTimeOutMs))
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO, "Received Port info from UDP Client.");
                        result.ResponsePacket = receiveTask.Result.Buffer;
                    }
                }
            }
            catch (Exception e)
            {
                result.Error = e;
            }

            return result;
        }
    }
}
