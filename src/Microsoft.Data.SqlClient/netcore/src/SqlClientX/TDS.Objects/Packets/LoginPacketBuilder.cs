using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets
{
    internal class LoginPacketBuilder
    {
        private enum States
        {
            None,
            LoginInformationWritten,

        }
        private TdsWriteStream _writeStream;

        internal TdsEnums.FeatureExtension DefaultRequestedFeatures
        {
            get { 
                return TdsEnums.FeatureExtension.None  
                    | TdsEnums.FeatureExtension.GlobalTransactions
                    | TdsEnums.FeatureExtension.DataClassification
                    | TdsEnums.FeatureExtension.Tce
                    | TdsEnums.FeatureExtension.UTF8Support
                    | TdsEnums.FeatureExtension.SQLDNSCaching;
            }
        }

        internal TdsEnums.FeatureExtension RequestedFeatures { get; private set; }

        // We want to make sure that no one can write the data out of order.
        // We will use the state to make sure that each build function can be 
        // called in order that is expected.
        private States _currentState = States.None;

        public LoginPacketBuilder(TdsWriteStream writeStream,
            TdsEnums.FeatureExtension additionalFeatures = TdsEnums.FeatureExtension.None) 
        {
            _writeStream = writeStream;
            RequestedFeatures = DefaultRequestedFeatures | additionalFeatures;
        }

        
        public async ValueTask<LoginPacketBuilder> SetLoginInformation(LoginPacket packet
            , bool isAsync
            , CancellationToken ct)
        {
            Debug.Assert(_currentState == States.None, "The Set Login information cannot be called if the state is already initialized");

            return this;
        }
    }
}
