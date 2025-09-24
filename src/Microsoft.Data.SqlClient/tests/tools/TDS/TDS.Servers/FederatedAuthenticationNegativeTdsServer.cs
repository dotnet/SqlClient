// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.FeatureExtAck;
using Microsoft.SqlServer.TDS.PreLogin;

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>
    /// TDS Server that generates invalid TDS scenarios according to the requested parameters
    /// </summary>
    public class FederatedAuthenticationNegativeTdsServer : GenericTdsServer<FederatedAuthenticationNegativeTdsServerArguments>
    {
        /// <summary>
        /// Initialization constructor
        /// </summary>
        public FederatedAuthenticationNegativeTdsServer() :
            this(new FederatedAuthenticationNegativeTdsServerArguments())
        {
        }

        /// <summary>
        /// Initialization constructor
        /// </summary>
        public FederatedAuthenticationNegativeTdsServer(FederatedAuthenticationNegativeTdsServerArguments arguments) :
            base(arguments)
        {
        }

        /// <summary>
        /// Handler for login request
        /// </summary>
        public override TDSMessageCollection OnPreLoginRequest(ITDSServerSession session, TDSMessage request)
        {
            // Get the collection from a valid On PreLogin Request
            TDSMessageCollection preLoginCollection = base.OnPreLoginRequest(session, request);

            // Find the is token carrying on TDSPreLoginToken
            TDSPreLoginToken preLoginToken = preLoginCollection.Find(message => message.Exists(packetToken => packetToken is TDSPreLoginToken)).
                Find(packetToken => packetToken is TDSPreLoginToken) as TDSPreLoginToken;

            switch (Arguments.Scenario)
            {
                case FederatedAuthenticationNegativeTdsScenarioType.NonceMissingInFedAuthPreLogin:
                    {
                        // If we have the prelogin token
                        if (preLoginToken != null && preLoginToken.Nonce != null)
                        {
                            // Nullify the nonce from the Token
                            preLoginToken.Nonce = null;
                        }

                        break;
                    }

                case FederatedAuthenticationNegativeTdsScenarioType.InvalidB_FEDAUTHREQUIREDResponse:
                    {
                        // If we have the prelogin token
                        if (preLoginToken != null)
                        {
                            // Set an illegal value for B_FEDAUTHREQURED
                            preLoginToken.FedAuthRequired = TdsPreLoginFedAuthRequiredOption.Illegal;
                        }

                        break;
                    }
            }

            // Return the collection
            return preLoginCollection;
        }

        /// <summary>
        /// Handler for login request
        /// </summary>
        public override TDSMessageCollection OnLogin7Request(ITDSServerSession session, TDSMessage request)
        {
            // Get the collection from the normal behavior On Login7 Request
            TDSMessageCollection login7Collection = base.OnLogin7Request(session, request);

            // Get the Federated Authentication ExtAck from Login 7
            TDSFeatureExtAckFederatedAuthenticationOption fedAutExtAct = GetFeatureExtAckFederatedAuthenticationOptionFromLogin7(login7Collection);

            // If not found, return the base collection intact
            if (fedAutExtAct != null)
            {
                switch (Arguments.Scenario)
                {
                    case FederatedAuthenticationNegativeTdsScenarioType.NonceMissingInFedAuthFEATUREXTACK:
                        {
                            // Delete the nonce from the Token
                            fedAutExtAct.ClientNonce = null;

                            break;
                        }
                    case FederatedAuthenticationNegativeTdsScenarioType.FedAuthMissingInFEATUREEXTACK:
                        {
                            // Remove the Fed Auth Ext Ack from the options list in the FeatureExtAckToken
                            GetFeatureExtAckTokenFromLogin7(login7Collection).Options.Remove(fedAutExtAct);

                            break;
                        }
                    case FederatedAuthenticationNegativeTdsScenarioType.SignatureMissingInFedAuthFEATUREXTACK:
                        {
                            // Delete the signature from the Token
                            fedAutExtAct.Signature = null;

                            break;
                        }
                }
            }

            // Return the collection
            return login7Collection;
        }

        private TDSFeatureExtAckToken GetFeatureExtAckTokenFromLogin7(TDSMessageCollection login7Collection)
        {
            // Find the is token carrying on TDSFeatureExtAckToken
            return login7Collection.Find(m => m.Exists(p => p is TDSFeatureExtAckToken)).
                Find(t => t is TDSFeatureExtAckToken) as TDSFeatureExtAckToken;
        }

        private TDSFeatureExtAckFederatedAuthenticationOption GetFeatureExtAckFederatedAuthenticationOptionFromLogin7(TDSMessageCollection login7Collection)
        {
            // Get the Fed Auth Ext Ack from the list of options in the feature ExtAck
            return GetFeatureExtAckTokenFromLogin7(login7Collection).Options.
                Where(o => o is TDSFeatureExtAckFederatedAuthenticationOption).FirstOrDefault() as TDSFeatureExtAckFederatedAuthenticationOption;
        }
    }
}
