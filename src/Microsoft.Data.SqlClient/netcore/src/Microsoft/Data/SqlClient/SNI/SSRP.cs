// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    internal sealed class SSRP
    {
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
        /// <param name="browserHostName">SQL Sever Browser hostname</param>
        /// <param name="instanceName">instance name to find port number</param>
        /// <returns>port number for given instance name</returns>
        internal static int GetPortByInstanceName(string browserHostName, string instanceName)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(browserHostName), "browserHostName should not be null, empty, or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");
            using (TrySNIEventScope.Create(nameof(SSRP)))
            {
                byte[] instanceInfoRequest = CreateInstanceInfoRequest(instanceName);
                byte[] responsePacket = null;
                try
                {
                    responsePacket = SendUDPRequest(browserHostName, SqlServerBrowserPort, instanceInfoRequest);
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
                int byteCount = Encoding.UTF7.GetByteCount(instanceName);

                byte[] requestPacket = new byte[byteCount + 1];
                requestPacket[0] = ClntUcastInst;
                Encoding.UTF7.GetBytes(instanceName, 0, instanceName.Length, requestPacket, 1);

                return requestPacket;
            }
        }

        /// <summary>
        /// Finds DAC port for given instance name.
        /// </summary>
        /// <param name="browserHostName">SQL Sever Browser hostname</param>
        /// <param name="instanceName">instance name to lookup DAC port</param>
        /// <returns>DAC port for given instance name</returns>
        internal static int GetDacPortByInstanceName(string browserHostName, string instanceName)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(browserHostName), "browserHostName should not be null, empty, or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instanceName), "instanceName should not be null, empty, or whitespace");

            byte[] dacPortInfoRequest = CreateDacPortInfoRequest(instanceName);
            byte[] responsePacket = SendUDPRequest(browserHostName, SqlServerBrowserPort, dacPortInfoRequest);

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
            int byteCount = Encoding.UTF7.GetByteCount(instanceName);

            byte[] requestPacket = new byte[byteCount + 2];
            requestPacket[0] = ClntUcastDac;
            requestPacket[1] = ProtocolVersion;
            Encoding.UTF7.GetBytes(instanceName, 0, instanceName.Length, requestPacket, 2);

            return requestPacket;
        }

        /// <summary>
        /// Sends request to server, and receives response from server by UDP.
        /// </summary>
        /// <param name="browserHostname">UDP server hostname</param>
        /// <param name="port">UDP server port</param>
        /// <param name="requestPacket">request packet</param>
        /// <returns>response packet from UDP server</returns>
        private static byte[] SendUDPRequest(string browserHostname, int port, byte[] requestPacket)
        {
            using (TrySNIEventScope.Create(nameof(SSRP)))
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(browserHostname), "browserhostname should not be null, empty, or whitespace");
                Debug.Assert(port >= 0 && port <= 65535, "Invalid port");
                Debug.Assert(requestPacket != null && requestPacket.Length > 0, "requestPacket should not be null or 0-length array");

                const int sendTimeOutMs = 1000;
                const int receiveTimeOutMs = 1000;

                bool isIpAddress = IPAddress.TryParse(browserHostname, out IPAddress address);

                byte[] responsePacket = null;
                using (UdpClient client = new UdpClient(!isIpAddress ? AddressFamily.InterNetwork : address.AddressFamily))
                {
                    Task<int> sendTask = client.SendAsync(requestPacket, requestPacket.Length, browserHostname, port);
                    Task<UdpReceiveResult> receiveTask = null;
                    
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO, "Waiting for UDP Client to fetch Port info.");
                    if (sendTask.Wait(sendTimeOutMs) && (receiveTask = client.ReceiveAsync()).Wait(receiveTimeOutMs))
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO, "Received Port info from UDP Client.");
                        responsePacket = receiveTask.Result.Buffer;
                    }
                }
                return responsePacket;
            }
        }

        /// <summary>
        /// Sends request to server, and recieves response from server (SQLBrowser) on port 1434 by UDP
        /// Request (https://docs.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/a3035afa-c268-4699-b8fd-4f351e5c8e9e)
        /// Response (https://docs.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/2e1560c9-5097-4023-9f5e-72b9ff1ec3b1) 
        /// </summary>
        /// <returns>string constaning list of SVR_RESP(just RESP_DATA)</returns>
        internal static string SendBroadcastUDPRequest()
        {
            StringBuilder response = new StringBuilder();
            byte[] CLNT_BCAST_EX_Request = new byte[1] { CLNT_BCAST_EX }; //0x02
            // Waits 5 seconds for the first response and every 1 second up to 15 seconds
            // https://docs.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/f2640a2d-3beb-464b-a443-f635842ebc3e#Appendix_A_3
            int currentTimeOut = FirstTimeoutForCLNT_BCAST_EX; 

            using (TrySNIEventScope.Create(nameof(SSRP)))
            {
                using (UdpClient clientListener = new UdpClient())
                {
                    Task<int> sendTask = clientListener.SendAsync(CLNT_BCAST_EX_Request, CLNT_BCAST_EX_Request.Length, new IPEndPoint(IPAddress.Broadcast, SqlServerBrowserPort));
                    Task<UdpReceiveResult> receiveTask = null;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO, "Waiting for UDP Client to fetch list of instances.");
                    Stopwatch sw = new Stopwatch(); //for waiting until 15 sec elapsed
                    sw.Start();
                    try
                    { 
                        while ((receiveTask = clientListener.ReceiveAsync()).Wait(currentTimeOut) && sw.ElapsedMilliseconds <= RecieveMAXTimeoutsForCLNT_BCAST_EX && receiveTask != null)
                        {
                            currentTimeOut = RecieveTimeoutsForCLNT_BCAST_EX;
                            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SSRP), EventType.INFO, "Received instnace info from UDP Client.");
                            if (receiveTask.Result.Buffer.Length < ValidResponseSizeForCLNT_BCAST_EX) //discard invalid response
                            {
                                response.Append(Encoding.UTF7.GetString(receiveTask.Result.Buffer, ServerResponseHeaderSizeForCLNT_BCAST_EX, receiveTask.Result.Buffer.Length - ServerResponseHeaderSizeForCLNT_BCAST_EX)); //RESP_DATA(VARIABLE) - 3 (RESP_SIZE + SVR_RESP)
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
    }
}
