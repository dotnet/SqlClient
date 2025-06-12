// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.EnvChange;
using Microsoft.SqlServer.TDS.Error;
using Microsoft.SqlServer.TDS.FeatureExtAck;
using Microsoft.SqlServer.TDS.Login7;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>
    /// TDS Server that authenticates clients according to the requested parameters
    /// </summary>
    public class TransientFaultTDSServer : GenericTDSServer, IDisposable
    {
        private static int RequestCounter = 0;

        public int Port { get; set; }

        public string ConnectionString { get; private set; }

        public void SetErrorBehavior(bool isEnabledTransientFault, uint errorNumber, string message)
        {
            if (Arguments is TransientFaultTDSServerArguments ServerArguments)
            {
                ServerArguments.IsEnabledTransientError = isEnabledTransientFault;
                ServerArguments.Number = errorNumber;
                ServerArguments.Message = message;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public TransientFaultTDSServer() => new TransientFaultTDSServer(new TransientFaultTDSServerArguments());

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="arguments"></param>
        public TransientFaultTDSServer(TransientFaultTDSServerArguments arguments) :
            base(arguments)
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="args"></param>
        public TransientFaultTDSServer(QueryEngine engine, TransientFaultTDSServerArguments args) : base(args)
        {
            Engine = engine;
        }

        private TDSServerEndPoint _endpoint = null;

        private static string GetErrorMessage(uint errorNumber)
        {
            switch (errorNumber)
            {
                case 40613:
                    return "Database on server is not currently available. Please retry the connection later. " +
                        "If the problem persists, contact customer support, and provide them the session tracing ID.";
                case 42108:
                    return "Can not connect to the SQL pool since it is paused. Please resume the SQL pool and try again.";
                case 42109:
                    return "The SQL pool is warming up. Please try again.";
            }
            return "Unknown server error occurred";
        }

        /// <summary>
        /// Handler for login request
        /// </summary>
        public override TDSMessageCollection OnLogin7Request(ITDSServerSession session, TDSMessage request)
        {
            // Inflate login7 request from the message
            TDSLogin7Token loginRequest = request[0] as TDSLogin7Token;

            // Check if arguments are of the transient fault TDS server
            if (Arguments is TransientFaultTDSServerArguments)
            {
                // Cast to transient fault TDS server arguments
                TransientFaultTDSServerArguments ServerArguments = Arguments as TransientFaultTDSServerArguments;

                // Check if we're still going to raise transient error
                if (ServerArguments.IsEnabledTransientError && RequestCounter < 1) // Fail first time, then connect
                {
                    uint errorNumber = ServerArguments.Number;
                    string errorMessage = ServerArguments.Message;

                    // Log request to which we're about to send a failure
                    TDSUtilities.Log(Arguments.Log, "Request", loginRequest);

                    // Prepare ERROR token with the denial details
                    TDSErrorToken errorToken = new TDSErrorToken(errorNumber, 1, 20, errorMessage);

                    // Log response
                    TDSUtilities.Log(Arguments.Log, "Response", errorToken);

                    // Serialize the error token into the response packet
                    TDSMessage responseMessage = new TDSMessage(TDSMessageType.Response, errorToken);

                    // Create DONE token
                    TDSDoneToken doneToken = new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Error);

                    // Log response
                    TDSUtilities.Log(Arguments.Log, "Response", doneToken);

                    // Serialize DONE token into the response packet
                    responseMessage.Add(doneToken);

                    RequestCounter++;

                    // Put a single message into the collection and return it
                    return new TDSMessageCollection(responseMessage);
                }
            }

            // Return login response from the base class
            return base.OnLogin7Request(session, request);
        }

        /// <summary>
        /// Complete login sequence
        /// </summary>
        protected override TDSMessageCollection OnAuthenticationCompleted(ITDSServerSession session)
        {
            // Delegate to the base class
            TDSMessageCollection responseMessageCollection = base.OnAuthenticationCompleted(session);

            // Check if arguments are of routing server
            if (Arguments is TransientFaultTDSServerArguments)
            {
                // Cast to transient fault TDS server arguments
                TransientFaultTDSServerArguments serverArguments = Arguments as TransientFaultTDSServerArguments;

                if (serverArguments.FailoverPartner == "")
                {
                    return responseMessageCollection;
                } 

                var envChangeToken = new TDSEnvChangeToken(TDSEnvChangeTokenType.RealTimeLogShipping, serverArguments.FailoverPartner);

                // Log response
                TDSUtilities.Log(Arguments.Log, "Response", envChangeToken);

                // Get the first message
                TDSMessage targetMessage = responseMessageCollection[0];

                // Index at which to insert the routing token
                int insertIndex = targetMessage.Count - 1;

                // VSTS# 1021027 - Read-Only Routing yields TDS protocol error
                // Resolution: Send TDS FeatureExtAct token before TDS ENVCHANGE token with routing information
                TDSPacketToken featureExtAckToken = targetMessage.Find(t => t is TDSFeatureExtAckToken);

                // Check if found
                if (featureExtAckToken != null)
                {
                    // Find token position
                    insertIndex = targetMessage.IndexOf(featureExtAckToken);
                }

                // Insert right before the done token
                targetMessage.Insert(insertIndex, envChangeToken);
                
            }

            return responseMessageCollection;
        }

        public static TransientFaultTDSServer StartTestServer(bool isEnabledTransientFault, bool enableLog, uint errorNumber, string failoverPartner = "", [CallerMemberName] string methodName = "")
         => StartServerWithQueryEngine(null, isEnabledTransientFault, enableLog, errorNumber, failoverPartner, methodName);

        public static TransientFaultTDSServer StartServerWithQueryEngine(QueryEngine engine, bool isEnabledTransientFault, bool enableLog, uint errorNumber, string failoverPartner = "", [CallerMemberName] string methodName = "")
        {
            TransientFaultTDSServerArguments args = new TransientFaultTDSServerArguments()
            {
                Log = enableLog ? Console.Out : null,
                IsEnabledTransientError = isEnabledTransientFault,
                Number = errorNumber,
                Message = GetErrorMessage(errorNumber),
                FailoverPartner = failoverPartner
            };

            TransientFaultTDSServer server = engine == null ? new TransientFaultTDSServer(args) : new TransientFaultTDSServer(engine, args);
            server._endpoint = new TDSServerEndPoint(server) { ServerEndPoint = new IPEndPoint(IPAddress.Any, 0) };
            server._endpoint.EndpointName = methodName;

            // The server EventLog should be enabled as it logs the exceptions.
            server._endpoint.EventLog = enableLog ? Console.Out : null;
            server._endpoint.Start();

            server.Port = server._endpoint.ServerEndPoint.Port;
            return server;
        }

        public void Dispose() => Dispose(true);

        private void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                _endpoint?.Stop();
                RequestCounter = 0;
            }
        }
    }
}
