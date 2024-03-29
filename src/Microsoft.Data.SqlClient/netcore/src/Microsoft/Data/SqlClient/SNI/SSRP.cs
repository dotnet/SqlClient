// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.SNI
{
    internal sealed class SSRP
    {
        private static readonly List<IPAddress> s_emptyList = new(0);
        private static readonly TimeSpan s_sendTimeout = TimeSpan.FromSeconds(1.0);
        private static readonly TimeSpan s_receiveTimeout = TimeSpan.FromSeconds(1.0);

        private const char SemicolonSeparator = ';';
        private const int SqlServerBrowserPort = 1434; //port SQL Server Browser
        private const int RecieveMAXTimeoutsForCLNT_BCAST_EX = 15000; //Default max time for response wait
        private const int RecieveTimeoutsForCLNT_BCAST_EX = 1000; //subsequent wait time for response after intial wait 
        private const int ServerResponseHeaderSizeForCLNT_BCAST_EX = 3;//(SVR_RESP + RESP_SIZE) https://docs.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/2e1560c9-5097-4023-9f5e-72b9ff1ec3b1
        private const int ValidResponseSizeForCLNT_BCAST_EX = 4096; //valid reponse size should be less than 4096
        private const int FirstTimeoutForCLNT_BCAST_EX = 5000;//wait for first response for 5 seconds
        private const int CLNT_BCAST_EX = 2;//request packet

        /// <summary>
        /// Finds instance port number for given instance name.
        /// </summary>
        /// <param name="browserHostName">SQL Server Browser hostname</param>
        /// <param name="instanceName">instance name to find port number</param>
        /// <param name="timeout">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>port number for given instance name</returns>
        internal static int GetPortByInstanceName(string browserHostName, string instanceName, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
            => GetPortByInstanceNameCore(browserHostName, instanceName, timeout, allIPsInParallel, ipPreference, false).Result;

        /// <summary>
        /// Finds instance port number for given instance name.
        /// </summary>
        /// <param name="browserHostName">SQL Server Browser hostname</param>
        /// <param name="instanceName">instance name to find port number</param>
        /// <param name="timeout">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>port number for given instance name</returns>
        internal static ValueTask<int> GetPortByInstanceNameAsync(string browserHostName, string instanceName, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
            => GetPortByInstanceNameCore(browserHostName, instanceName, timeout, allIPsInParallel, ipPreference, true);

        private static async ValueTask<int> GetPortByInstanceNameCore(string browserHostName, string instanceName, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference, bool async)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(browserHostName), "browserHostName should not be null, empty, or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");
            using (TrySNIEventScope.Create(nameof(SSRP)))
            {
                byte[] instanceInfoRequest = CreateInstanceInfoRequest(instanceName);
                byte[] responsePacket = null;
                try
                {
                    responsePacket = await SendUDPRequest(browserHostName, SqlServerBrowserPort, instanceInfoRequest, timeout, allIPsInParallel, ipPreference, async);
                }
                catch (SocketException se)
                {
                    // A SocketException is possible for an instance name that doesn't exist.
                    // If there are multiple IP addresses and one of them fails with a SocketException but
                    // others simply don't respond because the instance name is invalid, we want to return
                    // the same error as if the response was empty. The higher error suits all scenarios.
                    // But log it, just in case there is a different, underlying issue that support needs
                    // to troubleshoot.
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.ERR, "SocketException Message = {0}", args0: se?.Message);
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
            using (TrySNIEventScope.Create(nameof(SSRP)))
            {
                const byte ClntUcastInst = 0x04;
                int byteCount = Encoding.ASCII.GetByteCount(instanceName);

                byte[] requestPacket = new byte[byteCount + 1 + 1];
                requestPacket[0] = ClntUcastInst;
                Encoding.ASCII.GetBytes(instanceName, 0, instanceName.Length, requestPacket, 1);
                requestPacket[byteCount + 1] = 0;

                return requestPacket;
            }
        }

        /// <summary>
        /// Finds DAC port for given instance name.
        /// </summary>
        /// <param name="browserHostName">SQL Server Browser hostname</param>
        /// <param name="instanceName">instance name to lookup DAC port</param>
        /// <param name="timeout">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>DAC port for given instance name</returns>
        internal static int GetDacPortByInstanceName(string browserHostName, string instanceName, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
            => GetDacPortByInstanceNameCore(browserHostName, instanceName, timeout, allIPsInParallel, ipPreference, false).Result;

        /// <summary>
        /// Finds DAC port for given instance name.
        /// </summary>
        /// <param name="browserHostName">SQL Server Browser hostname</param>
        /// <param name="instanceName">instance name to lookup DAC port</param>
        /// <param name="timeout">Connection timer expiration</param>
        /// <param name="allIPsInParallel">query all resolved IP addresses in parallel</param>
        /// <param name="ipPreference">IP address preference</param>
        /// <returns>DAC port for given instance name</returns>
        internal static ValueTask<int> GetDacPortByInstanceNameAsync(string browserHostName, string instanceName, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference)
            => GetDacPortByInstanceNameCore(browserHostName, instanceName, timeout, allIPsInParallel, ipPreference, true);

        private static async ValueTask<int> GetDacPortByInstanceNameCore(string browserHostName, string instanceName, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference, bool async)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(browserHostName), "browserHostName should not be null, empty, or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");

            byte[] dacPortInfoRequest = CreateDacPortInfoRequest(instanceName);
            byte[] responsePacket = await SendUDPRequest(browserHostName, SqlServerBrowserPort, dacPortInfoRequest, timeout, allIPsInParallel, ipPreference, async);

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
            int byteCount = Encoding.ASCII.GetByteCount(instanceName);

            byte[] requestPacket = new byte[byteCount + 2 + 1];
            requestPacket[0] = ClntUcastDac;
            requestPacket[1] = ProtocolVersion;
            Encoding.ASCII.GetBytes(instanceName, 0, instanceName.Length, requestPacket, 2);
            requestPacket[2 + byteCount] = 0;

            return requestPacket;
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
        /// <param name="async">If true, this method will be run asynchronously</param>
        /// <returns>response packet from UDP server</returns>
        private static async ValueTask<byte[]> SendUDPRequest(string browserHostname, int port, byte[] requestPacket, TimeoutTimer timeout, bool allIPsInParallel, SqlConnectionIPAddressPreference ipPreference, bool async)
        {
            using (TrySNIEventScope.Create(nameof(SSRP)))
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(browserHostname), "browserhostname should not be null, empty, or whitespace");
                Debug.Assert(port >= 0 && port <= 65535, "Invalid port");
                Debug.Assert(requestPacket != null && requestPacket.Length > 0, "requestPacket should not be null or 0-length array");

                if (IPAddress.TryParse(browserHostname, out IPAddress address))
                {
                    return await SendUDPRequest(new IPAddress[] { address }, port, requestPacket, allIPsInParallel, async);
                }

                IPAddress[] ipAddresses = await (timeout.IsInfinite
                    ? SNICommon.GetDnsIpAddresses(browserHostname, async)
                    : SNICommon.GetDnsIpAddresses(browserHostname, timeout, async));

                Debug.Assert(ipAddresses.Length > 0, "DNS should throw if zero addresses resolve");
                List<IPAddress> ipv4Addresses = null;
                byte[] response4 = null;

                List<IPAddress> ipv6Addresses = null;
                byte[] response6 = null;

                Exception responseException = null;

                switch (ipPreference)
                {
                    case SqlConnectionIPAddressPreference.IPv4First:
                        {
                            SplitIPv4AndIPv6(ipAddresses, out ipv4Addresses, out ipv6Addresses);
                            
                            try
                            {
                                response4 = await SendUDPRequest(ipv4Addresses, port, requestPacket, allIPsInParallel, async).ConfigureAwait(false);

                                if (response4 != null)
                                {
                                    return response4;
                                }
                            }
                            catch(Exception e)
                            { responseException ??= e; }

                            try
                            {
                                response6 = await SendUDPRequest(ipv6Addresses, port, requestPacket, allIPsInParallel, async).ConfigureAwait(false);

                                if (response6 != null)
                                {
                                    return response6;
                                }
                            }
                            catch (Exception e)
                            { responseException ??= e; }

                            // No responses so throw first error
                            if (responseException != null)
                            {
                                throw responseException;
                            }

                            break;
                        }
                    case SqlConnectionIPAddressPreference.IPv6First:
                        {
                            SplitIPv4AndIPv6(ipAddresses, out ipv4Addresses, out ipv6Addresses);

                            try
                            {
                                response6 = await SendUDPRequest(ipv6Addresses, port, requestPacket, allIPsInParallel, async).ConfigureAwait(false);

                                if (response6 != null)
                                {
                                    return response6;
                                }
                            }
                            catch (Exception e)
                            { responseException ??= e; }

                            try
                            {
                                response4 = await SendUDPRequest(ipv4Addresses, port, requestPacket, allIPsInParallel, async).ConfigureAwait(false);

                                if (response4 != null)
                                {
                                    return response4;
                                }
                            }
                            catch (Exception e)
                            { responseException ??= e; }

                            // No responses so throw first error
                            if (responseException != null)
                            {
                                throw responseException;
                            }

                            break;
                        }
                    default:
                        return await SendUDPRequest(ipAddresses, port, requestPacket, true, async).ConfigureAwait(false); // allIPsInParallel);
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
        /// <param name="async">If true, this method will be run asynchronously</param>
        /// <returns>response packet from UDP server</returns>
        private static async ValueTask<byte[]> SendUDPRequest(IList<IPAddress> ipAddresses, int port, byte[] requestPacket, bool allIPsInParallel, bool async)
        {
            if (ipAddresses.Count == 0)
                return null;

            IPEndPoint endPoint = new IPEndPoint(ipAddresses[0], port);

            if (allIPsInParallel && ipAddresses.Count > 1) // Used for MultiSubnetFailover
            {
                List<Task<byte[]>> tasks = new(ipAddresses.Count);
                Task<byte[]> firstFailedTask = null;
                CancellationTokenSource cts = new CancellationTokenSource();
                // Cache the UdpClients for each of the address families to save disposing them
                UdpClient ipv4UdpClient = null;
                UdpClient ipv6UdpClient = null;

                for (int i = 0; i < ipAddresses.Count; i++)
                {
                    if (i > 0)
                    {
                        endPoint.Address = ipAddresses[i];
                    }

                    if (endPoint.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipv4UdpClient ??= new UdpClient(AddressFamily.InterNetwork);

                        tasks.Add(SendUDPRequest(endPoint, ipv4UdpClient, requestPacket, async, cts.Token));
                    }
                    else if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        ipv6UdpClient ??= new UdpClient(AddressFamily.InterNetworkV6);

                        tasks.Add(SendUDPRequest(endPoint, ipv4UdpClient, requestPacket, async, cts.Token));
                    }
                }

                using (ipv4UdpClient)
                using (ipv6UdpClient)
                {
                    while (tasks.Count > 0)
                    {
                        Task<byte[]> completedTask;

                        if (async)
                        {
                            completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);

                            if (completedTask.Status == TaskStatus.RanToCompletion)
                            {
                                cts.Cancel();
                                return completedTask.Result;
                            }
                        }
                        else
                        {
                            int completedTaskIndex = Task.WaitAny(tasks.ToArray());

                            completedTask = tasks[completedTaskIndex];
                            if (completedTask.Status == TaskStatus.RanToCompletion)
                            {
                                cts.Cancel();
                                return completedTask.Result;
                            }
                        }

                        if (completedTask.Status == TaskStatus.Faulted)
                        {
                            tasks.Remove(completedTask);
                            firstFailedTask ??= completedTask;
                        }
                    }

                    Debug.Assert(firstFailedTask != null, "firstFailedTask should never be null");

                    // All tasks failed. Return the error from the first failure.
                    throw firstFailedTask.Exception;
                }
            }
            else
            {
                using (UdpClient oneShotUdpClient = new UdpClient(endPoint.AddressFamily))
                {
                    // If not parallel, use the first IP address provided
                    return await SendUDPRequest(endPoint, oneShotUdpClient, requestPacket, async, CancellationToken.None);
                }
            }
        }

#if NET6_0_OR_GREATER
        private static async Task<byte[]> SendUDPRequest(IPEndPoint endPoint, UdpClient client, byte[] requestPacket, bool async, CancellationToken token)
#else
        private static Task<byte[]> SendUDPRequest(IPEndPoint endPoint, UdpClient client, byte[] requestPacket, bool async, CancellationToken token)
#endif
        {
            byte[] responsePacket = null;

            try
            {

                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO, "Waiting for UDP Client to fetch Port info.");

                using (CancellationTokenSource sendTimeoutCancellationTokenSource = new CancellationTokenSource(s_sendTimeout))
                using (CancellationTokenSource sendCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, sendTimeoutCancellationTokenSource.Token))
                {
#if NET6_0_OR_GREATER
                    ValueTask<int> sendTask = client.SendAsync(requestPacket.AsMemory(), endPoint, sendCancellationTokenSource.Token);

                    if (async)
                    {
                        await sendTask.ConfigureAwait(false);
                    }
                    else
                    {
                        if (!sendTask.IsCompleted)
                        {
                            sendTask.AsTask().Wait();
                        }
                    }
#else
                    Task<int> sendTask = client.SendAsync(requestPacket, requestPacket.Length, endPoint);

                    sendTask.Wait(sendCancellationTokenSource.Token);
#endif
                }

                UdpReceiveResult receiveResult;

                using (CancellationTokenSource receiveTimeoutCancellationTokenSource = new CancellationTokenSource(s_receiveTimeout))
                using (CancellationTokenSource receiveCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, receiveTimeoutCancellationTokenSource.Token))
                {
#if NET6_0_OR_GREATER
                    ValueTask<UdpReceiveResult> receiveTask = client.ReceiveAsync(receiveCancellationTokenSource.Token);

                    if (async)
                    {
                        receiveResult = await receiveTask.ConfigureAwait(false);
                    }
                    else
                    {
                        if (!receiveTask.IsCompleted)
                        {
                            receiveTask.AsTask().Wait();
                        }

                        receiveResult = receiveTask.Result;
                    }
#else
                    Task<UdpReceiveResult> receiveTask = client.ReceiveAsync();

                    receiveTask.Wait(receiveCancellationTokenSource.Token);
                    receiveResult = receiveTask.Result;
#endif

                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO, "Received Port info from UDP Client.");
                    responsePacket = receiveResult.Buffer;
                }

            }
            catch (OperationCanceledException)
            {
                responsePacket = null;
            }
            catch (AggregateException ae)
            {
                if (ae.InnerExceptions.Count > 0)
                {
                    Exception firstSocketException = null;

                    // Log all errors
                    foreach (Exception e in ae.InnerExceptions)
                    {
                        // Favor SocketException for returned error
                        if (e is SocketException)
                        {
                            firstSocketException = e;
                        }
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO,
                            "SendUDPRequest ({0}) resulted in exception: {1}", args0: endPoint, args1: e.Message);
                    }

                    // Throw first error if we didn't find a SocketException
                    throw firstSocketException ?? ae.InnerExceptions[0];
                }
                else
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO,
                        "SendUDPRequest ({0}) resulted in exception: {1}", args0: endPoint, args1: ae.Message);
                    throw;
                }
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO,
                    "SendUDPRequest ({0}) resulted in exception: {1}", args0: endPoint, args1: e.Message);
                throw;
            }

#if NET6_0_OR_GREATER
            return responsePacket;
#else
            return Task.FromResult(responsePacket);
#endif
        }

        private static void SplitIPv4AndIPv6(IPAddress[] input, out List<IPAddress> ipv4Addresses, out List<IPAddress> ipv6Addresses)
        {
            List<IPAddress> v4 = null;
            List<IPAddress> v6 = null;

            if (input != null && input.Length > 0)
            {
                v4 = new List<IPAddress>(1);
                v6 = new List<IPAddress>(0);

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
            }

            ipv4Addresses = v4 ?? s_emptyList;
            ipv6Addresses = v6 ?? s_emptyList;
        }
    }
}
